using System.ComponentModel.DataAnnotations;

namespace PFE_Back.DTOs.auth_DTOs
{
    public class GoogleAuthDto
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        public string? Picture { get; set; }
    }
}