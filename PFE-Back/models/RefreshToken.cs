using System.ComponentModel.DataAnnotations;

namespace PFE_Back.models
{
    public class RefreshToken : AuditableEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public string Token { get; set; } = string.Empty;
        [Required]
        public DateTime ExpiryDate { get; set; }
        public DateTime? RevokedAt { get; set; }
        public virtual ApplicationUser User { get; set; } = null!;
    }
}