using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFE_Back.models
{
    public class OcrStatistics
    {
        [Key]
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public int TotalAttempts { get; set; }

        public int SuccessfulAttempts { get; set; }

        public int FailedAttempts { get; set; }

        public int RecoveredDocuments { get; set; }

        public DateTime LastUpdated { get; set; }

        // Navigation property
        public virtual ApplicationUser User { get; set; }
    }
}