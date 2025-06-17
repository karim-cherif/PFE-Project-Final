using System;
using PFE_Back.models;
using Microsoft.AspNetCore.Identity;

namespace PFE_Back.models
{
    public class DocumentProcessingHistory
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Guid UserId { get; set; }
        public DateTime ProcessedAt { get; set; }
        public float Confidence { get; set; }
        public float ProcessingTime { get; set; }
        public string OcrResponse { get; set; }
        public Document Document { get; set; }
        public ApplicationUser User { get; set; } = null!;
    }
} 