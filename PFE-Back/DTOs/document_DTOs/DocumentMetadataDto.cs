using Microsoft.AspNetCore.Mvc;
using PFE_Back.enums;
using System.ComponentModel.DataAnnotations;

namespace PFE_Back.DTOs.document_DTOs
{
    public class DocumentMetadataDto
    {
        [Required]
        public SupportedLanguage language { get; set; }

        [StringLength(255)]
        
        public string? topic { get; set; }

        [StringLength(1000)]
        
        public string? description { get; set; }

        public List<string>? tags { get; set; }

        
        public bool isFavorite { get; set; } = false;
    }
}
