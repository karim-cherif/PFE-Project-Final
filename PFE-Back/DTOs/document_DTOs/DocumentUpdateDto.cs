using PFE_Back.enums;
using System.ComponentModel.DataAnnotations;

namespace PFE_Back.DTOs.document_DTOs
{
    public class DocumentUpdateDto
    {
        [StringLength(255)]
        public string? FileName { get; set; }

        [Range(1, int.MaxValue)]
        public int? PageCount { get; set; }

        public SupportedLanguage? Language { get; set; }
        [StringLength(255)]
        public string? Topic { get; set; }
        [StringLength(1000)]
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
        public bool? IsFavorite { get; set; }
        public string? OcrText { get; set; }
        public string? OcrResponse { get; set; }
        public string? Summary { get; set; }
        public DateTime? LastModified { get; set; }
        public ProcessingStatus? Status { get; set; }
        public string? RecoveryProcess { get; set; }
    }
}