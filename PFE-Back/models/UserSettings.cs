using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFE_Back.models
{
    public class UserSettings : AuditableEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string SettingsData { get; set; } // JSON string containing all settings

        public new DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public new DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
