using System.ComponentModel.DataAnnotations;

namespace PFE_Back.models
{
    public class CorrectionAlternative : AuditableEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public Guid CorrectionId { get; set; }
        [Required]
        [StringLength(1000)]
        public string Text { get; set; } = string.Empty;
        public virtual TextCorrection Correction { get; set; } = null!;
    }
}