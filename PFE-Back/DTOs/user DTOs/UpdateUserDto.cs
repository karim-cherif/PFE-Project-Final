using System.ComponentModel.DataAnnotations;

namespace PFE_Back.DTOs.user_DTOs
{
    public class UpdateUserDto
    {
        [StringLength(100)]
        public string FirstName { get; set; }
        [StringLength(100)]
        public string LastName { get; set; }
        [EmailAddress]
        public string Email { get; set; }
        public string ProfilePictureUrl { get; set; }
    }
}