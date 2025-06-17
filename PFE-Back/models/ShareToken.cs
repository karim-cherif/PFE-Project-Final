using System.ComponentModel.DataAnnotations;
using PFE_Back.enums;

namespace PFE_Back.models
{
    public class ShareToken : AuditableEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public Guid DocumentId { get; set; }
        [Required]
        public string Token { get; set; } = string.Empty;
        [Required]
        public DateTime ExpiryDate { get; set; }
        public string Type { get; set; } = string.Empty;
        public virtual Document Document { get; set; } = null!;
    }
}
