using System.Collections.Generic;

namespace PFE_Back.DTOs.dashboard
{
    public class DashboardResponse
    {
        public int DocumentsProcessed { get; set; }
        public int ProcessedDocuments { get; set; }
        public int DocumentsToday { get; set; }
        public string ProcessingTime { get; set; }
        public int ProcessingQueue { get; set; }
        public double AverageAccuracy { get; set; }
        public double SuccessRate { get; set; }
        public double ErrorRate { get; set; }
        public double RecoveryRate { get; set; }
        public double CharacterExtractionRate { get; set; }
        public long TotalStorage { get; set; }
        public long StorageGrowth { get; set; }
        public List<string> LanguagesDetected { get; set; } = new();
        public string TotalCharacters { get; set; }
        public List<WeeklyActivityDto> WeeklyActivity { get; set; } = new();
        public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();
        public List<LanguageDistributionDto> LanguageDistribution { get; set; } = new();
        public List<ProcessingMetricDto> ProcessingMetrics { get; set; } = new();
    }
} 