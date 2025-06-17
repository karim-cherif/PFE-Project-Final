using System.ComponentModel.DataAnnotations;

namespace PFE_Back.models
{
    public class ActivityLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public string Action { get; set; } = string.Empty;
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}