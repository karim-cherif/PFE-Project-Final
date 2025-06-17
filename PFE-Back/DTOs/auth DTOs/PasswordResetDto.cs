using System.ComponentModel.DataAnnotations;

namespace PFE_Back.DTOs.auth_DTOs
{
    public class PasswordResetDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}