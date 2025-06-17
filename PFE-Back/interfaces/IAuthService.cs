using PFE_Back.DTOs.auth_DTOs;

namespace PFE_Back.interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto, Guid? createdBy = null);
        Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(string token, Guid? updatedBy = null);
        Task ForgotPasswordAsync(PasswordResetDto passwordResetDto);
        Task ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
        Task ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto);
        Task<AuthResponseDto> GoogleAuthAsync(GoogleAuthDto googleAuthDto);
    }
}