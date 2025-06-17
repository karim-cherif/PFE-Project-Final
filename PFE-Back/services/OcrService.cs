using System.Net.Http.Headers;
using System.Text.Json;
using PFE_Back.interfaces;
using PFE_Back.models;
using PFE_Back.data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace PFE_Back.services
{
    public class OcrService : IOcrService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OcrService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly string _ocrServiceUrl;

        public OcrService(HttpClient httpClient, ILogger<OcrService> logger, IConfiguration configuration, ApplicationDbContext context)
        {
            _httpClient = httpClient;
            _context = context;
            _ocrServiceUrl = configuration["OcrService:Url"] ?? "http://localhost:5002";
            _httpClient.BaseAddress = new Uri(_ocrServiceUrl);
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // Increase timeout for large files
            _logger = logger;
            _logger.LogInformation("OCR Service configured with URL: {OcrServiceUrl}", _ocrServiceUrl);
        }

        public async Task<OcrResult> ProcessOcrAsync(string filePath, string language, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Processing OCR with language: {Language} for file: {FilePath}", language, filePath);

                // Check file size and set appropriate timeout
                var fileInfo = new FileInfo(filePath);
                var timeoutMinutes = CalculateTimeoutBasedOnFileSize(fileInfo.Length);

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                // Get user settings for OCR processing
                var userSettings = await GetUserOcrSettings();

                using var formData = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, combinedCts.Token));

                // Set proper content type based on file extension
                var contentType = GetContentType(filePath);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

                formData.Add(fileContent, "file", Path.GetFileName(filePath));
                formData.Add(new StringContent(language), "language");

                // Add user settings to the request
                if (userSettings != null)
                {
                    formData.Add(new StringContent(userSettings.ConfidenceThreshold.ToString()), "confidence_threshold");
                    formData.Add(new StringContent(userSettings.AutoEnhancement.ToString().ToLower()), "auto_enhancement");
                    formData.Add(new StringContent(userSettings.ProcessingQuality), "processing_quality");
                }

                _logger.LogInformation("Sending OCR request to: {Url} with timeout: {Timeout} minutes", $"{_ocrServiceUrl}/extract-text/", timeoutMinutes);

                var response = await _httpClient.PostAsync("/extract-text/", formData, combinedCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OCR service returned error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    throw new Exception($"OCR service returned error: {response.StatusCode}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("OCR service response: {Response}", jsonContent);

                var result = JsonSerializer.Deserialize<OcrResult>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null)
                    throw new Exception("OCR service returned null response.");

                _logger.LogInformation("OCR processing completed successfully. Text length: {Length}, Suggestions: {SuggestionCount}",
                    result.Text?.Length ?? 0, result.Suggestions?.Count ?? 0);

                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "OCR service request failed: {Error}", ex.Message);
                throw new Exception("OCR service is unavailable. Please ensure the OCR service is running.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "OCR service request timed out: {Error}", ex.Message);
                throw new Exception("OCR processing timed out. Please try with a smaller file.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse OCR service response: {Error}", ex.Message);
                throw new Exception("Invalid response from OCR service.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR processing failed: {Error}", ex.Message);
                throw new Exception($"An error occurred during OCR processing: {ex.Message}");
            }
        }

        private async Task<UserOcrSettings?> GetUserOcrSettings()
        {
            try
            {
                // For now, return default settings. In a real implementation,
                // you would get the current user's settings from the database
                return new UserOcrSettings
                {
                    ConfidenceThreshold = 70,
                    AutoEnhancement = true,
                    ProcessingQuality = "balanced"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get user OCR settings, using defaults");
                return null;
            }
        }

        private static string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".tiff" or ".tif" => "image/tiff",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        private static int CalculateTimeoutBasedOnFileSize(long fileSizeBytes)
        {
            // Base timeout of 2 minutes
            var baseTimeoutMinutes = 2;

            // Add 1 minute for every 5MB
            var additionalMinutes = (int)(fileSizeBytes / (5 * 1024 * 1024));

            // Cap at 15 minutes maximum
            return Math.Min(baseTimeoutMinutes + additionalMinutes, 15);
        }

        public async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    // Helper class for user OCR settings
    public class UserOcrSettings
    {
        public int ConfidenceThreshold { get; set; } = 70;
        public bool AutoEnhancement { get; set; } = true;
        public string ProcessingQuality { get; set; } = "balanced";
    }
}