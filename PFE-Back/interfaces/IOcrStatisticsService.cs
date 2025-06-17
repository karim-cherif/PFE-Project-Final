using System;
using System.Threading.Tasks;
using PFE_Back.DTOs;

namespace PFE_Back.interfaces
{
    public interface IOcrStatisticsService
    {
        Task<OcrStatisticsDto> GetStatisticsAsync(Guid userId);
        Task<OcrStatisticsDto> IncrementTotalAttemptsAsync(Guid userId);
        Task<OcrStatisticsDto> IncrementSuccessfulAttemptsAsync(Guid userId);
        Task<OcrStatisticsDto> IncrementFailedAttemptsAsync(Guid userId);
        Task<OcrStatisticsDto> IncrementRecoveredDocumentsAsync(Guid userId);
    }
} 