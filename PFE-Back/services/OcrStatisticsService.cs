using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PFE_Back.data;
using PFE_Back.DTOs;
using PFE_Back.interfaces;
using PFE_Back.models;

namespace PFE_Back.services
{
    public class OcrStatisticsService : IOcrStatisticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OcrStatisticsService> _logger;

        public OcrStatisticsService(ApplicationDbContext context, ILogger<OcrStatisticsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<OcrStatisticsDto> GetStatisticsAsync(Guid userId)
        {
            var stats = await _context.OcrStatistics
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (stats == null)
            {
                _logger.LogInformation("No statistics found for user {UserId}, creating new record", userId);
                stats = new OcrStatistics
                {
                    UserId = userId,
                    TotalAttempts = 0,
                    SuccessfulAttempts = 0,
                    FailedAttempts = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _context.OcrStatistics.Add(stats);
                await _context.SaveChangesAsync();
            }

            return MapToDto(stats);
        }

        public async Task<OcrStatisticsDto> IncrementTotalAttemptsAsync(Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var stats = await _context.OcrStatistics
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (stats == null)
                {
                    _logger.LogInformation("Creating new OcrStatistics for user {UserId}", userId);
                    stats = new OcrStatistics
                    {
                        UserId = userId,
                        TotalAttempts = 1,
                        SuccessfulAttempts = 0,
                        FailedAttempts = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.OcrStatistics.Add(stats);
                }
                else
                {
                    _logger.LogInformation("Incrementing total attempts for user {UserId}. Current: {Total}", 
                        userId, stats.TotalAttempts);
                    stats.TotalAttempts++;
                    stats.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully updated total attempts for user {UserId}. New total: {Total}", 
                    userId, stats.TotalAttempts);

                return MapToDto(stats);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error incrementing total attempts for user {UserId}", userId);
                throw;
            }
        }

        public async Task<OcrStatisticsDto> IncrementSuccessfulAttemptsAsync(Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var stats = await _context.OcrStatistics
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (stats == null)
                {
                    throw new Exception("Statistics record not found");
                }

                stats.SuccessfulAttempts++;
                stats.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully updated successful attempts for user {UserId}. New total: {Success}", 
                    userId, stats.SuccessfulAttempts);

                return MapToDto(stats);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error incrementing successful attempts for user {UserId}", userId);
                throw;
            }
        }

        public async Task<OcrStatisticsDto> IncrementFailedAttemptsAsync(Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var stats = await _context.OcrStatistics
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (stats == null)
                {
                    throw new Exception("Statistics record not found");
                }

                stats.FailedAttempts++;
                stats.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully updated failed attempts for user {UserId}. New total: {Failed}", 
                    userId, stats.FailedAttempts);

                return MapToDto(stats);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error incrementing failed attempts for user {UserId}", userId);
                throw;
            }
        }

        public async Task<OcrStatisticsDto> IncrementRecoveredDocumentsAsync(Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var stats = await _context.OcrStatistics
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (stats == null)
                {
                    _logger.LogInformation("Creating new OcrStatistics for user {UserId}", userId);
                    stats = new OcrStatistics
                    {
                        UserId = userId,
                        TotalAttempts = 0,
                        SuccessfulAttempts = 0,
                        FailedAttempts = 0,
                        RecoveredDocuments = 1,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.OcrStatistics.Add(stats);
                }
                else
                {
                    _logger.LogInformation("Incrementing recovered documents for user {UserId}. Current: {Recovered}", 
                        userId, stats.RecoveredDocuments);
                    stats.RecoveredDocuments++;
                    stats.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully updated recovered documents for user {UserId}. New total: {Recovered}", 
                    userId, stats.RecoveredDocuments);

                return MapToDto(stats);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error incrementing recovered documents for user {UserId}", userId);
                throw;
            }
        }

        private static OcrStatisticsDto MapToDto(OcrStatistics stats)
        {
            return new OcrStatisticsDto
            {
                UserId = stats.UserId,
                TotalAttempts = stats.TotalAttempts,
                SuccessfulAttempts = stats.SuccessfulAttempts,
                FailedAttempts = stats.FailedAttempts,
                RecoveredDocuments = stats.RecoveredDocuments,
                LastUpdated = stats.LastUpdated
            };
        }
    }
} 