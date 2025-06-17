using System;
using System.Collections.Generic;
using PFE_Back.enums;

namespace PFE_Back.DTOs.document_DTOs
{
    public class DocumentDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileType { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string? Topic { get; set; }
        public string? Description { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public bool IsFavorite { get; set; }
        public ProcessingStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string? OcrText { get; set; }
        public string? EnhancedText { get; set; }
        public string? OcrResponse { get; set; }
        public string? Summary { get; set; }
        public int? PageCount { get; set; }
        public DateTime UploadDate { get; set; }
        public DateTime? LastModified { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsDeleted { get; set; }
        public List<int> RecoveryProcess { get; set; } = new List<int>();
        public List<CorrectionDto> Corrections { get; set; } = new List<CorrectionDto>();

        // Admin-specific properties
        public string? UserEmail { get; set; }
        public Guid UserId { get; set; }
    }
}