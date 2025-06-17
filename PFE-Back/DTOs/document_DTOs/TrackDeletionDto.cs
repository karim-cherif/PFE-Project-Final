using System;

namespace PFE_Back.DTOs.document_DTOs
{
    public class TrackDeletionDto
    {
        public Guid DocumentId { get; set; }
        public bool IsPermanent { get; set; }
    }
} 