using PFE_Back.enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace PFE_Back.models
{
    public class Document : AuditableEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public Guid UserId { get; set; }
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;
        [Required]
        [StringLength(1000)]
        public string FilePath { get; set; } = string.Empty;
        [Required]
        public long FileSize { get; set; }
        [Required]
        [StringLength(100)]
        public string FileType { get; set; } = string.Empty;
        [Required]
        [StringLength(50)]
        public string Language { get; set; } = string.Empty;
        [StringLength(255)]
        public string? Topic { get; set; }
        [StringLength(1000)]
        public string? Description { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public bool IsFavorite { get; set; } = false;
        [Required]
        public ProcessingStatus Status { get; set; } = ProcessingStatus.Uploaded;
        public string? ErrorMessage { get; set; }
        public string? OcrText { get; set; }
        public string? EnhancedText { get; set; }
        [Column(TypeName = "nvarchar(max)")]
        public string? OcrResponse { get; set; }
        [Column(TypeName = "nvarchar(max)")]
        public string? Summary { get; set; }
        public int? PageCount { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.Now;
        public DateTime? LastModified { get; set; }
        public new DateTime? DeletedAt { get; set; }
        public new bool IsDeleted { get; set; }
        public double? Accuracy { get; set; }
        public double? ProcessingTime { get; set; }
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual ICollection<TextCorrection> Corrections { get; set; } = new List<TextCorrection>();
        public virtual ICollection<ShareToken> ShareTokens { get; set; } = new List<ShareToken>();

        [Column(TypeName = "nvarchar(max)")]
        public string RecoveryProcess { get; set; } = "[]";

        [NotMapped]
        public List<int> RecoveryProcessList
        {
            get => string.IsNullOrEmpty(RecoveryProcess)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(RecoveryProcess);
            set => RecoveryProcess = JsonSerializer.Serialize(value);
        }
    }
}