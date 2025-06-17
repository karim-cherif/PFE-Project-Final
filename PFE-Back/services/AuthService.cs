using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PFE_Back.DTOs.auth_DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SendGrid;
using SendGrid.Helpers.Mail;
using PFE_Back.data;
using PFE_Back.interfaces;
using PFE_Back.models;
using PFE_Back.utils;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;

namespace PFE_Back.services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly ICacheService _cacheService;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ApplicationDbContext context,
            ILogger<AuthService> logger,
            ICacheService cacheService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
            _logger = logger;
            _cacheService = cacheService;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto, Guid? createdBy = null)
        {
            try
            {
                _logger.LogInformation("Attempting registration for email: {Email}", registerDto.Email);

                // Check if user already exists (including deleted users)
                var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
                if (existingUser != null)
                {
                    if (existingUser.IsDeleted)
                    {
                        _logger.LogWarning("Registration attempt with deleted user email: {Email}, UserId: {UserId}, DeletedAt: {DeletedAt}",
                            registerDto.Email, existingUser.Id, existingUser.DeletedAt);
                        throw new Exception("An account with this email address was previously deactivated. Please contact support or use a different email address.");
                    }
                    else
                    {
                        _logger.LogWarning("Registration attempt with existing active user email: {Email}", registerDto.Email);
                        throw new Exception("An account with this email address already exists. Please use a different email address or try logging in.");
                    }
                }

                var user = new ApplicationUser
                {
                    UserName = registerDto.Email,
                    Email = registerDto.Email,
                    FirstName = registerDto.FirstName,
                    LastName = registerDto.LastName,
                    CreatedBy = createdBy ?? Guid.Empty,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                var result = await _userManager.CreateAsync(user, registerDto.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Registration failed for email {Email}. Errors: {Errors}", registerDto.Email, errors);
                    throw new Exception($"Registration failed: {errors}");
                }

                await _userManager.AddToRoleAsync(user, "User");
                _logger.LogInformation("User {UserId} registered successfully by {CreatedBy}.", user.Id, createdBy ?? Guid.Empty);

                // Invalidate admin dashboard statistics cache
                await InvalidateAdminStatsCacheAsync();

                await LogActivity(user.Id, "User registered");
                return await GenerateAuthResponseAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", registerDto.Email);
                throw;
            }
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
        {
            try
            {
                _logger.LogInformation("Attempting login for email: {Email}", loginDto.Email);

                var user = await _userManager.FindByEmailAsync(loginDto.Email);
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User not found for email: {Email}", loginDto.Email);
                    throw new UnauthorizedAccessException("Invalid email or password.");
                }

                if (user.IsDeleted)
                {
                    _logger.LogWarning("Login attempt with deleted user account: {Email}, UserId: {UserId}, DeletedAt: {DeletedAt}",
                        loginDto.Email, user.Id, user.DeletedAt);
                    throw new UnauthorizedAccessException("This account has been deactivated. Please contact support if you believe this is an error.");
                }

                _logger.LogInformation("Found user {UserId} for email {Email}, attempting password sign in", user.Id, user.Email);
                var result = await _signInManager.PasswordSignInAsync(user, loginDto.Password, false, false);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Password sign in failed for user {UserId}", user.Id);
                    if (result.IsLockedOut)
                    {
                        _logger.LogWarning("User {UserId} is locked out", user.Id);
                        throw new UnauthorizedAccessException("Account is locked. Please try again later.");
                    }
                    if (result.IsNotAllowed)
                    {
                        _logger.LogWarning("Sign in not allowed for user {UserId}", user.Id);
                        throw new UnauthorizedAccessException("Account is not allowed to sign in.");
                    }
                    throw new UnauthorizedAccessException("Invalid email or password.");
                }

                _logger.LogInformation("User {UserId} logged in successfully", user.Id);
                await LogActivity(user.Id, "User logged in");
                return await GenerateAuthResponseAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login attempt");
                throw;
            }
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                _logger.LogInformation("Attempting to refresh token");

                var storedToken = _context.RefreshTokens
                    .FirstOrDefault(rt => rt.Token == refreshToken && rt.RevokedAt == null && rt.ExpiryDate > DateTime.UtcNow && !rt.IsDeleted);

                if (storedToken == null)
                {
                    _logger.LogWarning("Invalid or expired refresh token");
                    throw new SecurityTokenException("Invalid or expired refresh token.");
                }

                var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("User not found or deleted for refresh token");
                    throw new SecurityTokenException("User not found.");
                }

                // Revoke the current refresh token
                storedToken.RevokedAt = DateTime.UtcNow;
                storedToken.UpdatedBy = user.Id;
                storedToken.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Refresh token revoked for user {UserId}", user.Id);
                await LogActivity(user.Id, "Token refreshed");

                // Generate new tokens
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "User";

                var token = JwtHelper.GenerateJwtToken(user, role, _configuration);
                var newRefreshToken = JwtHelper.GenerateRefreshToken();

                // Save the new refresh token
                var refreshTokenEntity = new RefreshToken
                {
                    UserId = user.Id,
                    Token = newRefreshToken,
                    ExpiryDate = DateTime.UtcNow.AddDays(double.Parse(_configuration["JwtSettings:RefreshTokenLifetimeDays"] ?? "7")),
                    CreatedBy = user.Id
                };

                _context.RefreshTokens.Add(refreshTokenEntity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New refresh token saved for user {UserId}", user.Id);

                var tokenLifetimeMinutes = double.Parse(_configuration["JwtSettings:TokenLifetimeMinutes"] ?? "60");
                var response = new AuthResponseDto
                {
                    Token = token,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(tokenLifetimeMinutes)
                };

                _logger.LogInformation("Token refresh successful for user {UserId}", user.Id);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                throw;
            }
        }

        public async Task LogoutAsync(string token, Guid? updatedBy = null)
        {
            var userId = GetUserIdFromToken(token);
            var refreshTokens = _context.RefreshTokens.Where(rt => rt.UserId == userId && rt.RevokedAt == null && !rt.IsDeleted);
            foreach (var rt in refreshTokens)
            {
                rt.RevokedAt = DateTime.UtcNow;
                rt.UpdatedBy = updatedBy ?? userId;
                rt.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} logged out.", userId);
            await LogActivity(userId, "User logged out");
        }

        public async Task ForgotPasswordAsync(PasswordResetDto passwordResetDto)
        {
            var user = await _userManager.FindByEmailAsync(passwordResetDto.Email);
            if (user == null || user.IsDeleted)
                return; // Silent fail for security

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var client = new SendGridClient(_configuration["SendGrid:ApiKey"]);
            var msg = MailHelper.CreateSingleEmail(
                new EmailAddress(_configuration["SendGrid:FromEmail"], _configuration["SendGrid:FromName"]),
                new EmailAddress(user.Email),
                "Password Reset Request",
                $"Use this token to reset your password: {token}",
                $"<p>Use this token to reset your password: <strong>{token}</strong></p>"
            );

            var response = await client.SendEmailAsync(msg);
            if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
                throw new Exception("Failed to send password reset email.");

            _logger.LogInformation("Password reset email sent to {Email}.", user.Email);
            await LogActivity(user.Id, "Password reset requested");
        }

        public async Task ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
        {
            var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email);
            if (user == null || user.IsDeleted)
                throw new Exception("Invalid email or token.");

            var result = await _userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.NewPassword);
            if (!result.Succeeded)
                throw new Exception("Password reset failed.");

            user.UpdatedBy = user.Id;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Password reset for user {UserId}.", user.Id);
            await LogActivity(user.Id, "Password reset");
        }

        public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || user.IsDeleted)
                throw new Exception("User not found.");

            var result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
            if (!result.Succeeded)
                throw new Exception("Password change failed.");

            user.UpdatedBy = userId;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Password changed for user {UserId}.", userId);
            await LogActivity(userId, "Password changed");
        }

        public async Task<AuthResponseDto> GoogleAuthAsync(GoogleAuthDto googleAuthDto)
        {
            try
            {
                _logger.LogInformation("=== Starting Google Authentication Process ===");
                _logger.LogInformation("Received DTO - Email: {Email}, FirstName: {FirstName}, LastName: {LastName}, HasFirstName: {HasFirstName}, HasLastName: {HasLastName}, LastNameType: {LastNameType}",
                    googleAuthDto.Email,
                    googleAuthDto.FirstName,
                    googleAuthDto.LastName,
                    !string.IsNullOrEmpty(googleAuthDto.FirstName),
                    !string.IsNullOrEmpty(googleAuthDto.LastName),
                    googleAuthDto.LastName?.GetType().Name ?? "null");

                // Verify the Google access token by calling Google's userinfo endpoint
                var clientId = _configuration["Google:ClientId"];
                _logger.LogInformation("Using Google Client ID: {ClientId}", clientId);

                _logger.LogInformation("Attempting to validate Google access token");
                _logger.LogInformation("Access token (first 50 chars): {TokenStart}...", googleAuthDto.IdToken?.Substring(0, Math.Min(50, googleAuthDto.IdToken?.Length ?? 0)));

                // Validate the access token by calling Google's userinfo API
                using var httpClient = new HttpClient();

                // Try both methods of token validation
                HttpResponseMessage userInfoResponse = null;
                string userInfoJson = null;

                try
                {
                    // Method 1: Query parameter
                    _logger.LogInformation("Trying Google userinfo API with query parameter");
                    userInfoResponse = await httpClient.GetAsync($"https://www.googleapis.com/oauth2/v2/userinfo?access_token={googleAuthDto.IdToken}");

                    if (!userInfoResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Query parameter method failed with status: {StatusCode}", userInfoResponse.StatusCode);

                        // Method 2: Authorization header
                        _logger.LogInformation("Trying Google userinfo API with Authorization header");
                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", googleAuthDto.IdToken);
                        userInfoResponse = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                    }

                    if (!userInfoResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await userInfoResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Google token validation failed - HTTP {StatusCode}, Response: {Response}", userInfoResponse.StatusCode, errorContent);

                        // For development, let's be more lenient and just validate the email format
                        if (!string.IsNullOrEmpty(googleAuthDto.Email) && googleAuthDto.Email.Contains("@"))
                        {
                            _logger.LogWarning("Token validation failed, but proceeding with email validation for development");
                        }
                        else
                        {
                            throw new UnauthorizedAccessException("Invalid Google token.");
                        }
                    }
                    else
                    {
                        userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation("Google userinfo response: {Response}", userInfoJson);

                        var userInfo = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(userInfoJson);

                        if (userInfo == null || !userInfo.ContainsKey("email"))
                        {
                            _logger.LogError("Google token validation failed - no email in response");
                            throw new UnauthorizedAccessException("Invalid Google token - no email found.");
                        }

                        var googleEmail = userInfo["email"].ToString();
                        if (googleEmail != googleAuthDto.Email)
                        {
                            _logger.LogError("Email mismatch - token email: {TokenEmail}, provided email: {ProvidedEmail}", googleEmail, googleAuthDto.Email);
                            throw new UnauthorizedAccessException("Email mismatch in Google authentication.");
                        }

                        _logger.LogInformation("Google token validated successfully for email: {Email}", googleEmail);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "HTTP request exception during Google token validation");

                    // For development, proceed if we have a valid email
                    if (!string.IsNullOrEmpty(googleAuthDto.Email) && googleAuthDto.Email.Contains("@"))
                    {
                        _logger.LogWarning("HTTP request failed, but proceeding with email validation for development");
                    }
                    else
                    {
                        throw new UnauthorizedAccessException("Failed to validate Google token due to network error.");
                    }
                }

                // Check if user exists (only consider active users)
                var user = await _userManager.FindByEmailAsync(googleAuthDto.Email);

                // Handle deleted users - permanently remove them first, then create fresh account
                if (user != null && user.IsDeleted)
                {
                    _logger.LogInformation("Google authentication attempt with deleted user account: {Email}, UserId: {UserId}, DeletedAt: {DeletedAt}. Permanently removing old account and creating new one.",
                        googleAuthDto.Email, user.Id, user.DeletedAt);

                    // Permanently delete the old user account to free up the email/username
                    try
                    {
                        // Delete all user data first
                        await DeleteAllUserDataForGoogleAuth(user.Id);

                        // Remove the user from Identity system completely
                        var deleteResult = await _userManager.DeleteAsync(user);
                        if (!deleteResult.Succeeded)
                        {
                            var errors = string.Join(", ", deleteResult.Errors.Select(e => e.Description));
                            _logger.LogError("Failed to delete existing user during Google auth: {Errors}", errors);
                            throw new Exception($"Failed to remove existing account: {errors}");
                        }

                        _logger.LogInformation("Successfully removed old deleted user account: {Email}", googleAuthDto.Email);
                        user = null; // Treat as new user
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error removing deleted user account during Google auth: {Email}", googleAuthDto.Email);
                        throw new Exception("Unable to process authentication. Please contact support.");
                    }
                }

                if (user == null)
                {
                    _logger.LogInformation("=== Creating New User ===");

                    // Log raw input values
                    _logger.LogInformation("Raw input values - FirstName: {FirstName}, LastName: {LastName}, Email: {Email}, LastNameType: {LastNameType}",
                        googleAuthDto.FirstName,
                        googleAuthDto.LastName,
                        googleAuthDto.Email,
                        googleAuthDto.LastName?.GetType().Name ?? "null");

                    // Ensure we have valid names
                    var firstName = googleAuthDto.FirstName?.Trim() ?? "User";
                    var lastName = googleAuthDto.LastName?.Trim() ?? firstName;

                    _logger.LogInformation("After initial sanitization - FirstName: {FirstName}, LastName: {LastName}, LastNameType: {LastNameType}",
                        firstName,
                        lastName,
                        lastName?.GetType().Name ?? "null");

                    // Validate and sanitize the data
                    firstName = (firstName.Length > 100) ? firstName.Substring(0, 100) : firstName;
                    lastName = (lastName.Length > 100) ? lastName.Substring(0, 100) : lastName;

                    _logger.LogInformation("After length validation - FirstName: {FirstName}, LastName: {LastName}, LastNameType: {LastNameType}",
                        firstName,
                        lastName,
                        lastName?.GetType().Name ?? "null");

                    // Ensure lastName is never null or empty
                    if (string.IsNullOrWhiteSpace(lastName))
                    {
                        lastName = firstName;
                        _logger.LogWarning("LastName was empty after validation, setting to FirstName: {FirstName}", firstName);
                    }

                    _logger.LogInformation("Final name values before user creation - FirstName: {FirstName}, LastName: {LastName}, LastNameType: {LastNameType}",
                        firstName,
                        lastName,
                        lastName?.GetType().Name ?? "null");

                    // Create the user object with explicit LastName assignment
                    var newUser = new ApplicationUser
                    {
                        UserName = googleAuthDto.Email,
                        Email = googleAuthDto.Email,
                        FirstName = firstName,
                        LastName = lastName, // Explicitly set LastName
                        EmailConfirmed = true,
                        CreatedBy = Guid.Empty,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false,
                        LockoutEnabled = false,
                        TwoFactorEnabled = false,
                        PhoneNumberConfirmed = false,
                        SecurityStamp = Guid.NewGuid().ToString(),
                        NormalizedEmail = googleAuthDto.Email.ToUpper(),
                        NormalizedUserName = googleAuthDto.Email.ToUpper(),
                        ConcurrencyStamp = Guid.NewGuid().ToString(),
                        ProfilePictureUrl = googleAuthDto.Picture // Save the Google profile picture URL
                    };

                    // Log the user object after creation
                    _logger.LogInformation("User object created with values - FirstName: {FirstName}, LastName: {LastName}, Email: {Email}, LastNameType: {LastNameType}, LastNameLength: {LastNameLength}",
                        newUser.FirstName,
                        newUser.LastName,
                        newUser.Email,
                        newUser.LastName?.GetType().Name ?? "null",
                        newUser.LastName?.Length ?? 0);

                    // Double check LastName is set
                    if (string.IsNullOrWhiteSpace(newUser.LastName))
                    {
                        newUser.LastName = newUser.FirstName;
                        _logger.LogWarning("LastName was empty in user object, setting to FirstName: {FirstName}", newUser.FirstName);
                    }

                    try
                    {
                        // Log the SQL parameters before creation
                        _logger.LogInformation("About to create user with SQL parameters - FirstName: {FirstName}, LastName: {LastName}, Email: {Email}, LastNameType: {LastNameType}, LastNameLength: {LastNameLength}",
                            newUser.FirstName,
                            newUser.LastName,
                            newUser.Email,
                            newUser.LastName?.GetType().Name ?? "null",
                            newUser.LastName?.Length ?? 0);

                        // Ensure LastName is set before database insertion
                        if (string.IsNullOrWhiteSpace(newUser.LastName))
                        {
                            newUser.LastName = newUser.FirstName;
                            _logger.LogWarning("LastName was empty before database insertion, setting to FirstName: {FirstName}", newUser.FirstName);
                        }

                        // Log the final state of the user object
                        _logger.LogInformation("Final user object state before database insertion - FirstName: {FirstName}, LastName: {LastName}, Email: {Email}, LastNameType: {LastNameType}, LastNameLength: {LastNameLength}, LastNameIsNull: {LastNameIsNull}",
                            newUser.FirstName,
                            newUser.LastName,
                            newUser.Email,
                            newUser.LastName?.GetType().Name ?? "null",
                            newUser.LastName?.Length ?? 0,
                            newUser.LastName == null);

                        // Force LastName to be set
                        newUser.LastName = newUser.LastName ?? newUser.FirstName;
                        _logger.LogInformation("After force setting LastName - LastName: {LastName}, LastNameType: {LastNameType}",
                            newUser.LastName,
                            newUser.LastName?.GetType().Name ?? "null");

                        // Set LastName directly on the user object
                        var lastNameValue = newUser.LastName ?? newUser.FirstName;
                        newUser.LastName = lastNameValue;
                        _logger.LogInformation("After direct LastName assignment - LastName: {LastName}, LastNameType: {LastNameType}",
                            newUser.LastName,
                            newUser.LastName?.GetType().Name ?? "null");

                        // Ensure LastName is set one final time
                        if (string.IsNullOrWhiteSpace(newUser.LastName))
                        {
                            newUser.LastName = newUser.FirstName;
                            _logger.LogWarning("LastName was empty before final database insertion, setting to FirstName: {FirstName}", newUser.FirstName);
                        }

                        // Log the final state one more time
                        _logger.LogInformation("Final state before database insertion - FirstName: {FirstName}, LastName: {LastName}, Email: {Email}, LastNameType: {LastNameType}, LastNameLength: {LastNameLength}, LastNameIsNull: {LastNameIsNull}",
                            newUser.FirstName,
                            newUser.LastName,
                            newUser.Email,
                            newUser.LastName?.GetType().Name ?? "null",
                            newUser.LastName?.Length ?? 0,
                            newUser.LastName == null);

                        // Set LastName one final time
                        newUser.LastName = newUser.LastName ?? newUser.FirstName;
                        _logger.LogInformation("After final LastName assignment - LastName: {LastName}, LastNameType: {LastNameType}",
                            newUser.LastName,
                            newUser.LastName?.GetType().Name ?? "null");

                        // Ensure LastName is set one final time
                        if (string.IsNullOrWhiteSpace(newUser.LastName))
                        {
                            newUser.LastName = newUser.FirstName;
                            _logger.LogWarning("LastName was empty before final database insertion, setting to FirstName: {FirstName}", newUser.FirstName);
                        }

                        // Log the final state one more time
                        _logger.LogInformation("Final state before database insertion - FirstName: {FirstName}, LastName: {LastName}, Email: {Email}, LastNameType: {LastNameType}, LastNameLength: {LastNameLength}, LastNameIsNull: {LastNameIsNull}",
                            newUser.FirstName,
                            newUser.LastName,
                            newUser.Email,
                            newUser.LastName?.GetType().Name ?? "null",
                            newUser.LastName?.Length ?? 0,
                            newUser.LastName == null);

                        var result = await _userManager.CreateAsync(newUser);
                        if (!result.Succeeded)
                        {
                            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                            _logger.LogError("Failed to create user. Errors: {Errors}", errors);
                            throw new Exception($"Failed to create user: {errors}");
                        }

                        _logger.LogInformation("User created successfully with ID: {UserId}, FirstName: {FirstName}, LastName: {LastName}, LastNameType: {LastNameType}",
                            newUser.Id,
                            newUser.FirstName,
                            newUser.LastName,
                            newUser.LastName?.GetType().Name ?? "null");

                        await _userManager.AddToRoleAsync(newUser, "User");

                        // Invalidate admin dashboard statistics cache
                        await InvalidateAdminStatsCacheAsync();

                        await LogActivity(newUser.Id, "User registered via Google");

                        // Assign the new user to the outer user variable
                        user = newUser;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating user: {Message}", ex.Message);
                        throw;
                    }
                }
                else
                {
                    _logger.LogInformation("=== Updating Existing User ===");
                    // Update the user's profile picture URL if it's provided
                    if (!string.IsNullOrEmpty(googleAuthDto.Picture))
                    {
                        user.ProfilePictureUrl = googleAuthDto.Picture;
                        await _userManager.UpdateAsync(user);
                        _logger.LogInformation("Updated profile picture URL for user {UserId}", user.Id);
                    }
                }

                _logger.LogInformation("Generating auth response for user {UserId}", user.Id);
                return await GenerateAuthResponseAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google authentication failed: {Message}", ex.Message);
                throw;
            }
        }

        private async Task<AuthResponseDto> GenerateAuthResponseAsync(ApplicationUser user)
        {
            try
            {
                _logger.LogInformation("Generating auth response for user {UserId}", user.Id);

                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "User";
                _logger.LogInformation("User {UserId} has role: {Role}", user.Id, role);

                var token = JwtHelper.GenerateJwtToken(user, role, _configuration);
                var refreshToken = JwtHelper.GenerateRefreshToken();

                _logger.LogInformation("Generated JWT token and refresh token for user {UserId}", user.Id);

                var refreshTokenEntity = new RefreshToken
                {
                    UserId = user.Id,
                    Token = refreshToken,
                    ExpiryDate = DateTime.UtcNow.AddDays(double.Parse(_configuration["JwtSettings:RefreshTokenLifetimeDays"] ?? "7")),
                    CreatedBy = user.Id
                };

                _context.RefreshTokens.Add(refreshTokenEntity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Saved refresh token to database for user {UserId}", user.Id);

                var tokenLifetimeMinutes = _configuration["JwtSettings:TokenLifetimeMinutes"] ?? "60";
                var response = new AuthResponseDto
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(double.Parse(tokenLifetimeMinutes))
                };

                _logger.LogInformation("Auth response generated successfully for user {UserId}", user.Id);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating auth response for user {UserId}", user.Id);
                throw;
            }
        }

        private Guid GetUserIdFromToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null)
                throw new SecurityTokenException("Invalid token.");
            return Guid.Parse(userIdClaim.Value);
        }

        private async Task LogActivity(Guid userId, string action)
        {
            _context.ActivityLogs.Add(new ActivityLog { UserId = userId, Action = action });
            await _context.SaveChangesAsync();
        }

        private async Task DeleteAllUserDataForGoogleAuth(Guid userId)
        {
            try
            {
                _logger.LogInformation("Deleting all data for user {UserId} during Google auth", userId);

                // Delete user documents and all related data
                var userDocuments = await _context.Documents
                    .Where(d => d.UserId == userId)
                    .ToListAsync();

                _context.Documents.RemoveRange(userDocuments);

                // Delete user-specific data entities
                var userSettings = await _context.UserSettings
                    .Where(us => us.UserId == userId)
                    .ToListAsync();
                _context.UserSettings.RemoveRange(userSettings);

                var refreshTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId)
                    .ToListAsync();
                _context.RefreshTokens.RemoveRange(refreshTokens);

                var activityLogs = await _context.ActivityLogs
                    .Where(al => al.UserId == userId)
                    .ToListAsync();
                _context.ActivityLogs.RemoveRange(activityLogs);

                var ocrStatistics = await _context.OcrStatistics
                    .Where(os => os.UserId == userId)
                    .ToListAsync();
                _context.OcrStatistics.RemoveRange(ocrStatistics);

                var deletionStats = await _context.DeletionStats
                    .Where(ds => ds.UserId == userId)
                    .ToListAsync();
                _context.DeletionStats.RemoveRange(deletionStats);

                var documentProcessingHistory = await _context.DocumentProcessingHistory
                    .Where(dph => dph.UserId == userId)
                    .ToListAsync();
                _context.DocumentProcessingHistory.RemoveRange(documentProcessingHistory);

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully deleted all data for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user data for user {UserId} during Google auth", userId);
                throw;
            }
        }

        /// <summary>
        /// Invalidate admin dashboard statistics cache when users are created
        /// </summary>
        private async Task InvalidateAdminStatsCacheAsync()
        {
            try
            {
                // Clear specific admin dashboard statistics caches
                await _cacheService.RemoveAsync("admin:dashboard:stats");
                await _cacheService.RemoveAsync("admin:ocr:stats");
                await _cacheService.RemoveAsync("admin:user:count");
                await _cacheService.RemoveAsync("admin:document:count");
                await _cacheService.RemoveByPatternAsync("admin:stats:*");
                await _cacheService.RemoveByPatternAsync("dashboard:*");
                _logger.LogInformation("Admin statistics caches invalidated successfully after user creation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating admin statistics caches after user creation");
            }
        }
    }
}