using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PFE_Back.models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;
        [StringLength(100)]
        public string? LastName { get; set; } = string.Empty;
        [StringLength(500)]
        public string? ProfilePictureUrl { get; set; }
        [Required]
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? DeletedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? LastLoginDate { get; set; }

        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}