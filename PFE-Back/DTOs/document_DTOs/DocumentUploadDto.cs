using PFE_Back.enums;
using System.ComponentModel.DataAnnotations;

namespace PFE_Back.DTOs.document_DTOs
{
    public class DocumentUploadDto
    {
        [Required]
        public required IFormFile File { get; set; }
        [Required]
        public SupportedLanguage Language { get; set; }
        [StringLength(255)]
        public string? Topic { get; set; }
        [StringLength(1000)]
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
        public bool IsFavorite { get; set; } = false;
    }
}