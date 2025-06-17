namespace PFE_Back.DTOs.dashboard
{
    public class AdminDashboardStatsDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int DeletedUsers { get; set; }
        public int TotalDocuments { get; set; }
        public int ProcessedDocuments { get; set; }
        public int FailedDocuments { get; set; }
        public int DocumentsInProcessing { get; set; }
        public long TotalStorage { get; set; }
        public double AverageProcessingTime { get; set; }
        public double OverallSuccessRate { get; set; }
        public double OverallErrorRate { get; set; }
        public int DocumentsToday { get; set; }
        public int UsersToday { get; set; }
        public List<AdminLanguageDistributionDto> LanguageDistribution { get; set; } = new();
        public List<AdminDocumentTypeDistributionDto> DocumentTypeDistribution { get; set; } = new();
        public List<DailyActivityDto> RecentActivity { get; set; } = new();
        public List<MonthlyStatsDto> MonthlyTrends { get; set; } = new();
    }

    public class AdminOcrStatsDto
    {
        public long TotalAttempts { get; set; }
        public long SuccessfulAttempts { get; set; }
        public long FailedAttempts { get; set; }
        public double AverageConfidence { get; set; }
        public double AverageProcessingTime { get; set; }
        public double SuccessRate { get; set; }
        public double FailureRate { get; set; }
        public long RecoveredDocuments { get; set; }
        public List<LanguageOcrStatsDto> LanguageStats { get; set; } = new();
        public List<DailyOcrStatsDto> DailyStats { get; set; } = new();
    }

    public class AdminLanguageDistributionDto
    {
        public string Language { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class AdminDocumentTypeDistributionDto
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class DailyActivityDto
    {
        public DateTime Date { get; set; }
        public int DocumentsProcessed { get; set; }
        public int UsersActive { get; set; }
        public double AverageAccuracy { get; set; }
    }

    public class MonthlyStatsDto
    {
        public string Month { get; set; } = string.Empty;
        public int DocumentsProcessed { get; set; }
        public int ActiveUsers { get; set; }
        public double AverageAccuracy { get; set; }
        public long StorageUsed { get; set; }
    }

    public class LanguageOcrStatsDto
    {
        public string Language { get; set; } = string.Empty;
        public long Attempts { get; set; }
        public long Successful { get; set; }
        public double SuccessRate { get; set; }
        public double AverageConfidence { get; set; }
    }

    public class DailyOcrStatsDto
    {
        public DateTime Date { get; set; }
        public long Attempts { get; set; }
        public long Successful { get; set; }
        public double SuccessRate { get; set; }
        public double AverageProcessingTime { get; set; }
    }
}
