using System.ComponentModel.DataAnnotations;

namespace PFE_Back.models
{
    public class TextCorrection : AuditableEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public Guid DocumentId { get; set; }
        [Required]
        [StringLength(1000)]
        public string OriginalText { get; set; } = string.Empty;
        [Required]
        [StringLength(1000)]
        public string CorrectedText { get; set; } = string.Empty;
        [Required]
        public float Confidence { get; set; }
        public int PageNumber { get; set; } = 1;
        public int LineNumber { get; set; }
        public int CharPosition { get; set; }
        public virtual Document Document { get; set; } = null!;
        public virtual ICollection<CorrectionAlternative> Alternatives { get; set; } = new List<CorrectionAlternative>();
    }
}