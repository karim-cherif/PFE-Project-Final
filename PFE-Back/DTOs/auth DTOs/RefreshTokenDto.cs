using System.ComponentModel.DataAnnotations;

namespace PFE_Back.DTOs.auth_DTOs
{
    public class RefreshTokenDto
    {
        [Required]
        public string RefreshToken { get; set; }
    }
}