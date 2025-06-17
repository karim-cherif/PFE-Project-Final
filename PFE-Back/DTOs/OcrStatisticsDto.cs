using System;

namespace PFE_Back.DTOs
{
    public class OcrStatisticsDto
    {
        public Guid UserId { get; set; }
        public int TotalAttempts { get; set; }
        public int SuccessfulAttempts { get; set; }
        public int FailedAttempts { get; set; }
        public int RecoveredDocuments { get; set; }
        public DateTime LastUpdated { get; set; }
    }
} 