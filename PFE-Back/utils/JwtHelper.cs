using Microsoft.IdentityModel.Tokens;
using PFE_Back.models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PFE_Back.utils
{
    public static class JwtHelper
    {
        private static readonly ILogger _logger;

        static JwtHelper()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            _logger = loggerFactory.CreateLogger(typeof(JwtHelper));
        }

        public static string GenerateJwtToken(ApplicationUser user, string role, IConfiguration configuration)
        {
            try
            {
                _logger.LogInformation("Starting JWT token generation for user {UserId} with role {Role}", user.Id, role);
                
                var jwtSettings = configuration.GetSection("JwtSettings");
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                
                _logger.LogInformation("Created signing credentials with key length: {KeyLength}", jwtSettings["Key"]?.Length ?? 0);

                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(ClaimTypes.Role, role),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                _logger.LogInformation("Created claims for user {UserId}: {ClaimsCount} claims", user.Id, claims.Length);
                foreach (var claim in claims)
                {
                    _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
                }

                var tokenLifetimeMinutes = double.Parse(jwtSettings["TokenLifetimeMinutes"] ?? "60");
                var token = new JwtSecurityToken(
                    issuer: jwtSettings["Issuer"],
                    audience: jwtSettings["Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(tokenLifetimeMinutes),
                    signingCredentials: credentials
                );

                _logger.LogInformation("Created JWT token for user {UserId}. Expires in {Minutes} minutes", 
                    user.Id, tokenLifetimeMinutes);

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                _logger.LogInformation("Successfully generated JWT token for user {UserId}", user.Id);
                
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token for user {UserId}", user.Id);
                throw;
            }
        }

        public static string GenerateRefreshToken()
        {
            try
            {
                _logger.LogInformation("Generating refresh token");
                var randomNumber = new byte[32];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(randomNumber);
                var token = Convert.ToBase64String(randomNumber);
                _logger.LogInformation("Successfully generated refresh token");
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating refresh token");
                throw;
            }
        }
    }
}