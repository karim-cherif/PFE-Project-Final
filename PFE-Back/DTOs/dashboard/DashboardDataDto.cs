using PFE_Back.DTOs.document_DTOs;
using PFE_Back.DTOs.user_DTOs;
using PFE_Back.enums;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;

namespace PFE_Back.DTOs.dashboard
{
    public class DashboardDataDto
    {
        // Basic Statistics
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
        
        // Activity Data
        public List<WeeklyActivityDto> WeeklyActivity { get; set; } = new();
        public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();
        public List<LanguageDistributionDto> LanguageDistribution { get; set; } = new();
        public List<ProcessingMetricDto> ProcessingMetrics { get; set; } = new();
        public List<DocumentTypeDistribution> DocumentTypeDistribution { get; set; } = new();
    }

    public class WeeklyActivityDto
    {
        public string Day { get; set; }
        public int Documents { get; set; }
        public double Accuracy { get; set; }
    }

    public class MonthlyTrendDto
    {
        public string Month { get; set; }
        public int Active { get; set; }
        public int Deleted { get; set; }
    }

    public class LanguageDistributionDto
    {
        public string Name { get; set; }
        public int Docs { get; set; }
    }

    public class ProcessingMetricDto
    {
        public string Metric { get; set; }
        public double Value { get; set; }
    }

    public class DocumentTypeDistribution
    {
        public string Type { get; set; }
        public int Nombre { get; set; }
    }
} 