using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PFE_Back.DTOs.auth_DTOs;
using PFE_Back.interfaces;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PFE_Back.data;

namespace PFE_Back.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context;

        public AuthController(IAuthService authService, ApplicationDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        [HttpGet("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                // Try to connect to the database
                await _context.Database.CanConnectAsync();
                return Ok(new { Success = true, Message = "Database connection successful." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = $"Database connection failed: {ex.Message}" });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                var response = await _authService.RegisterAsync(registerDto);
                return Ok(new { Success = true, Message = "User registered successfully.", Data = response });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = "Registration failed. Please try again." });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var response = await _authService.LoginAsync(loginDto);
                return Ok(new { Success = true, Message = "Login successful.", Data = response });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = "Login failed. Please try again." });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            try
            {
                var response = await _authService.RefreshTokenAsync(refreshTokenDto.RefreshToken);
                return Ok(new { Success = true, Message = "Token refreshed successfully.", Data = response });
            }
            catch (SecurityTokenException ex)
            {
                return Unauthorized(new { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = "Token refresh failed. Please try again." });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                await _authService.LogoutAsync(token);
                return Ok(new { Success = true, Message = "Logout successful." });
            }
            catch (SecurityTokenException ex)
            {
                return Unauthorized(new { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = "Logout failed. Please try again." });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] PasswordResetDto passwordResetDto)
        {
            try
            {
                await _authService.ForgotPasswordAsync(passwordResetDto);
                return Ok(new { Success = true, Message = "Password reset email sent successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = "Failed to send password reset email. Please try again." });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            try
            {
                await _authService.ResetPasswordAsync(resetPasswordDto);
                return Ok(new { Success = true, Message = "Password reset successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = "Password reset failed. Please try again." });
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                await _authService.ChangePasswordAsync(userId, changePasswordDto);
                return Ok(new { Success = true, Message = "Password changed successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = "Password change failed. Please try again." });
            }
        }

        [HttpPost("google-auth")]
        public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthDto googleAuthDto)
        {
            try
            {
                // Add detailed logging for debugging
                Console.WriteLine($"[GoogleAuth] Received request with data:");
                Console.WriteLine($"[GoogleAuth] IdToken: {googleAuthDto?.IdToken?.Substring(0, Math.Min(50, googleAuthDto.IdToken?.Length ?? 0))}...");
                Console.WriteLine($"[GoogleAuth] Email: {googleAuthDto?.Email}");
                Console.WriteLine($"[GoogleAuth] FirstName: {googleAuthDto?.FirstName}");
                Console.WriteLine($"[GoogleAuth] LastName: {googleAuthDto?.LastName}");
                Console.WriteLine($"[GoogleAuth] Picture: {googleAuthDto?.Picture}");

                // Validate the DTO
                if (googleAuthDto == null)
                {
                    Console.WriteLine("[GoogleAuth] ERROR: googleAuthDto is null");
                    return BadRequest(new { Success = false, Message = "Invalid request data." });
                }

                if (string.IsNullOrEmpty(googleAuthDto.IdToken))
                {
                    Console.WriteLine("[GoogleAuth] ERROR: IdToken is null or empty");
                    return BadRequest(new { Success = false, Message = "IdToken is required." });
                }

                if (string.IsNullOrEmpty(googleAuthDto.Email))
                {
                    Console.WriteLine("[GoogleAuth] ERROR: Email is null or empty");
                    return BadRequest(new { Success = false, Message = "Email is required." });
                }

                if (string.IsNullOrEmpty(googleAuthDto.FirstName))
                {
                    Console.WriteLine("[GoogleAuth] ERROR: FirstName is null or empty");
                    return BadRequest(new { Success = false, Message = "FirstName is required." });
                }

                Console.WriteLine("[GoogleAuth] Validation passed, calling AuthService...");
                var response = await _authService.GoogleAuthAsync(googleAuthDto);
                Console.WriteLine("[GoogleAuth] AuthService completed successfully");

                return Ok(new { Success = true, Message = "Google authentication successful.", Data = response });
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[GoogleAuth] UnauthorizedAccessException: {ex.Message}");
                return Unauthorized(new { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleAuth] Exception: {ex.Message}");
                Console.WriteLine($"[GoogleAuth] StackTrace: {ex.StackTrace}");
                return BadRequest(new { Success = false, Message = $"Google authentication failed: {ex.Message}" });
            }
        }
    }
}