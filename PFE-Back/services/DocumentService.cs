using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PFE_Back.data;
using PFE_Back.DTOs.dashboard;
using PFE_Back.DTOs.document_DTOs;
using PFE_Back.DTOs.user_DTOs;
using PFE_Back.enums;
using PFE_Back.Hubs;
using PFE_Back.interfaces;
using PFE_Back.models;
using PFE_Back.common;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace PFE_Back.services
{
    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IOcrService _ocrService;
        private readonly ILlmService _llmService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentService> _logger;
        private readonly IOcrStatisticsService _ocrStatisticsService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICacheService _cacheService;

        public DocumentService(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IOcrService ocrService,
            ILlmService llmService,
            IHubContext<NotificationHub> hubContext,
            IConfiguration configuration,
            ILogger<DocumentService> logger,
            IOcrStatisticsService ocrStatisticsService,
            IHttpContextAccessor httpContextAccessor,
            ICacheService cacheService)
        {
            _context = context;
            _environment = environment;
            _ocrService = ocrService;
            _llmService = llmService;
            _hubContext = hubContext;
            _configuration = configuration;
            _logger = logger;
            _ocrStatisticsService = ocrStatisticsService;
            _httpContextAccessor = httpContextAccessor;
            _cacheService = cacheService;
        }

        private Guid GetUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User not authenticated.");
            return Guid.Parse(userIdClaim);
        }

        public async Task<DocumentDto> UploadDocumentAsync(DocumentUploadDto uploadDto, Guid userId, Guid? createdBy = null)
        {
            ValidateUpload(uploadDto.File);
            var document = await SaveDocument(uploadDto, userId, createdBy ?? userId);
            _logger.LogInformation("Document {DocumentId} uploaded by {UserId}.", document.Id, userId);
            await LogActivity(userId, $"Uploaded document {document.FileName}");

            _logger.LogInformation("📤 [SignalR] Attempting to send upload notification to user {UserId}", userId);
            try
            {
                var notificationMessage = $"Document {document.FileName} uploaded successfully.";
                _logger.LogInformation("📤 [SignalR] Sending notification: {Message}", notificationMessage);

                await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", notificationMessage);
                _logger.LogInformation("✅ [SignalR] Upload notification sent successfully to user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SignalR] Failed to send upload notification to user {UserId}: {ErrorMessage}", userId, ex.Message);
            }

            return MapToDocumentDto(document);
        }

        public async Task<List<DocumentDto>> BulkUploadDocumentsAsync(List<DocumentUploadDto> uploadDtos, Guid userId, Guid? createdBy = null)
        {
            if (uploadDtos == null || !uploadDtos.Any())
                throw new Exception("No files provided for upload.");

            var documents = new List<Document>();
            foreach (var uploadDto in uploadDtos)
            {
                ValidateUpload(uploadDto.File);
                var document = await SaveDocument(uploadDto, userId, createdBy ?? userId);
                documents.Add(document);
            }

            _logger.LogInformation("Bulk uploaded {Count} documents by {UserId}.", documents.Count, userId);
            await LogActivity(userId, $"Bulk uploaded {documents.Count} documents");
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"{documents.Count} documents uploaded successfully.");
            return documents.Select(MapToDocumentDto).ToList();
        }

        public async Task<DocumentDto> GetDocumentByIdAsync(Guid id, Guid userId)
        {
            var document = await FetchDocument(id, userId, true);
            return MapToDocumentDto(document);
        }

        public async Task<DocumentSearchResultDto> GetDocumentsAsync(DocumentSearchDto searchDto, Guid userId)
        {
            _logger.LogInformation("Starting document fetch for user {UserId} with parameters: {@SearchDto}", userId, searchDto);

            var query = _context.Documents.Where(d => d.UserId == userId);
            _logger.LogDebug("Initial query created for user {UserId}", userId);

            var totalUserDocuments = await _context.Documents.CountAsync(d => d.UserId == userId);
            _logger.LogDebug("Total documents for user {UserId}: {Count}", userId, totalUserDocuments);

            var deletedUserDocuments = await _context.Documents.CountAsync(d => d.UserId == userId && d.IsDeleted);
            _logger.LogDebug("Deleted documents for user {UserId}: {Count}", userId, deletedUserDocuments);

            _logger.LogDebug("IncludeDeleted parameter value: {Value}", searchDto.IncludeDeleted);
            _logger.LogDebug("IncludeDeletedNum parameter value: {Value}", searchDto.IncludeDeletedNum);

            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                var searchTerm = searchDto.SearchTerm.ToLower();
                query = query.Where(d =>
                    EF.Functions.Like(d.FileName.ToLower(), $"%{searchTerm}%") ||
                    (d.OcrText != null && EF.Functions.Like(d.OcrText.ToLower(), $"%{searchTerm}%")) ||
                    (d.EnhancedText != null && EF.Functions.Like(d.EnhancedText.ToLower(), $"%{searchTerm}%")) ||
                    (d.Topic != null && EF.Functions.Like(d.Topic.ToLower(), $"%{searchTerm}%")) ||
                    (d.Description != null && EF.Functions.Like(d.Description.ToLower(), $"%{searchTerm}%")) ||
                    (d.Tags != null && d.Tags.Any(t => EF.Functions.Like(t.ToLower(), $"%{searchTerm}%")))
                );
                _logger.LogDebug("Applied search term filter: {SearchTerm}", searchTerm);
            }

            if (searchDto.Language.HasValue)
            {
                query = query.Where(d => d.Language == searchDto.Language.Value.ToString().ToLower());
                _logger.LogDebug("Applied language filter: {Language}", searchDto.Language.Value);
            }

            if (!string.IsNullOrEmpty(searchDto.Topic))
            {
                query = query.Where(d => d.Topic == searchDto.Topic);
                _logger.LogDebug("Applied topic filter: {Topic}", searchDto.Topic);
            }

            if (searchDto.Status.HasValue)
            {
                _logger.LogDebug("Applying status filter: {Status}", searchDto.Status.Value);
                query = query.Where(d => d.Status == searchDto.Status.Value);
                _logger.LogInformation("Status filter applied successfully for status: {Status}", searchDto.Status.Value);
            }
            else
            {
                _logger.LogDebug("No status filter applied - showing all statuses");
            }

            if (searchDto.DateFrom.HasValue)
            {
                query = query.Where(d => d.UploadDate >= searchDto.DateFrom.Value);
                _logger.LogDebug("Applied date from filter: {DateFrom}", searchDto.DateFrom.Value);
            }

            if (searchDto.DateTo.HasValue)
            {
                query = query.Where(d => d.UploadDate <= searchDto.DateTo.Value);
                _logger.LogDebug("Applied date to filter: {DateTo}", searchDto.DateTo.Value);
            }

            if (!string.IsNullOrEmpty(searchDto.Type))
            {
                var type = searchDto.Type.ToLower();
                if (type == "pdf")
                {
                    query = query.Where(d => d.FileType.ToLower().Contains("pdf"));
                    _logger.LogDebug("Applied PDF type filter");
                }
                else if (type == "image")
                {
                    query = query.Where(d => d.FileType.ToLower().Contains("image"));
                    _logger.LogDebug("Applied image type filter");
                }
            }

            if (searchDto.FavoritesOnly)
            {
                query = query.Where(d => d.IsFavorite);
                _logger.LogDebug("Applied favorites filter");
            }

            if (searchDto.DocumentType.HasValue && searchDto.DocumentType.Value != DocumentType.All)
            {
                if (searchDto.DocumentType.Value == DocumentType.PDF)
                {
                    query = query.Where(d => d.FileType.ToLower().Contains("pdf"));
                }
                else if (searchDto.DocumentType.Value == DocumentType.Image)
                {
                    query = query.Where(d => d.FileType.ToLower().Contains("image"));
                }
                _logger.LogDebug("Applied document type filter: {DocumentType}", searchDto.DocumentType.Value);
            }

            if (searchDto.IncludeDeleted)
            {
                query = query.Where(d => d.IsDeleted);
                _logger.LogDebug("Filtering for deleted documents only");
            }
            else
            {
                query = query.Where(d => !d.IsDeleted);
                _logger.LogDebug("Excluding deleted documents");
            }

            if (!string.IsNullOrEmpty(searchDto.SortBy))
            {
                var sortField = searchDto.SortBy.ToLower();
                _logger.LogInformation("Applying sort with field: {SortField}, order: {SortOrder}", sortField, searchDto.SortOrder);

                switch (sortField)
                {
                    case "uploaddate":
                    case "date":
                        if (searchDto.IncludeDeleted)
                        {
                            query = searchDto.SortOrder?.ToLower() == "asc"
                                ? query.OrderBy(d => d.DeletedAt)
                                : query.OrderByDescending(d => d.DeletedAt);
                            _logger.LogDebug("Sorted by DeletedAt {Order}", searchDto.SortOrder);
                        }
                        else
                        {
                            query = searchDto.SortOrder?.ToLower() == "asc"
                                ? query.OrderBy(d => d.UploadDate)
                                : query.OrderByDescending(d => d.UploadDate);
                            _logger.LogDebug("Sorted by UploadDate {Order}", searchDto.SortOrder);
                        }
                        break;
                    case "filename":
                    case "name":
                        query = searchDto.SortOrder?.ToLower() == "asc"
                            ? query.OrderBy(d => d.FileName)
                            : query.OrderByDescending(d => d.FileName);
                        _logger.LogDebug("Sorted by FileName {Order}", searchDto.SortOrder);
                        break;
                    case "filesize":
                    case "size":
                        query = searchDto.SortOrder?.ToLower() == "asc"
                            ? query.OrderBy(d => d.FileSize)
                            : query.OrderByDescending(d => d.FileSize);
                        _logger.LogDebug("Sorted by FileSize {Order}", searchDto.SortOrder);
                        break;
                    case "accuracy":
                        query = searchDto.SortOrder?.ToLower() == "asc"
                            ? query.OrderBy(d => d.Corrections.Any() ? d.Corrections.Average(c => c.Confidence) : 0)
                            : query.OrderByDescending(d => d.Corrections.Any() ? d.Corrections.Average(c => c.Confidence) : 0);
                        _logger.LogDebug("Sorted by average confidence {Order}", searchDto.SortOrder);
                        break;
                    default:
                        if (searchDto.IncludeDeleted)
                        {
                            query = query.OrderByDescending(d => d.DeletedAt);
                            _logger.LogDebug("Using default sort by DeletedAt descending for trash view");
                        }
                        else
                        {
                            query = query.OrderByDescending(d => d.UploadDate);
                            _logger.LogDebug("Using default sort by UploadDate descending");
                        }
                        break;
                }
            }
            else
            {
                if (searchDto.IncludeDeleted)
                {
                    query = query.OrderByDescending(d => d.DeletedAt);
                    _logger.LogDebug("No sorting specified, using default sort by DeletedAt descending for trash view");
                }
                else
                {
                    query = query.OrderByDescending(d => d.UploadDate);
                    _logger.LogDebug("No sorting specified, using default sort by UploadDate descending");
                }
            }

            var sql = query.ToQueryString();
            _logger.LogDebug("Generated SQL query: {Sql}", sql);

            var resultsBeforePagination = await query.ToListAsync();
            _logger.LogDebug("Results before pagination: {Count} documents", resultsBeforePagination.Count);

            foreach (var doc in resultsBeforePagination)
            {
                _logger.LogDebug("Document {Id}: IsDeleted = {IsDeleted}, FileName = {FileName}", doc.Id, doc.IsDeleted, doc.FileName);
            }

            var totalCount = await query.CountAsync();
            _logger.LogDebug("Total document count: {TotalCount}", totalCount);

            var documents = await query
                .Skip((searchDto.Page - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .Include(d => d.Corrections)
                .ThenInclude(c => c.Alternatives)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} documents for page {Page}", documents.Count, searchDto.Page);

            foreach (var doc in documents)
            {
                _logger.LogDebug("Final document {Id}: IsDeleted = {IsDeleted}, FileName = {FileName}", doc.Id, doc.IsDeleted, doc.FileName);
            }

            var result = new DocumentSearchResultDto
            {
                Items = documents.Select(MapToDocumentDto).ToList(),
                TotalCount = totalCount,
                Page = searchDto.Page,
                PageSize = searchDto.PageSize
            };

            _logger.LogInformation("Document fetch completed for user {UserId}. Found {TotalCount} documents, returning {PageSize} for page {Page}",
                userId, totalCount, searchDto.PageSize, searchDto.Page);

            return result;
        }

        public async Task UpdateDocumentAsync(Guid id, DocumentUpdateDto updateDto, Guid userId, Guid? updatedBy = null)
        {
            _logger.LogInformation("Starting UpdateDocumentAsync for document {DocumentId} with update data: {@UpdateDto}", id, updateDto);

            var document = await FetchDocument(id, userId, true);
            _logger.LogInformation("Current document state before update: {@Document}", new
            {
                document.Id,
                document.Status,
                document.RecoveryProcessList,
                document.LastModified,
                document.UpdatedAt
            });

            if (!string.IsNullOrWhiteSpace(updateDto.FileName))
                document.FileName = updateDto.FileName;
            if (updateDto.Language.HasValue)
                document.Language = NormalizeLanguageName(updateDto.Language.Value.ToString());
            if (updateDto.Topic != null)
                document.Topic = updateDto.Topic;
            if (updateDto.Description != null)
                document.Description = updateDto.Description;
            if (updateDto.Tags != null)
                document.Tags = updateDto.Tags;
            if (updateDto.IsFavorite.HasValue)
                document.IsFavorite = updateDto.IsFavorite.Value;
            if (updateDto.OcrText != null)
                document.OcrText = updateDto.OcrText;
            if (updateDto.OcrResponse != null)
                document.OcrResponse = updateDto.OcrResponse;
            if (updateDto.Summary != null)
                document.Summary = updateDto.Summary;
            if (updateDto.RecoveryProcess != null)
            {
                try
                {
                    _logger.LogInformation("Attempting to parse RecoveryProcess: {RecoveryProcess}", updateDto.RecoveryProcess);
                    // Parse the recovery process from JSON string
                    var recoveryProcess = JsonSerializer.Deserialize<List<int>>(updateDto.RecoveryProcess);
                    if (recoveryProcess != null)
                    {
                        document.RecoveryProcessList = recoveryProcess;
                        _logger.LogInformation("Successfully updated RecoveryProcess for document {DocumentId}: {Process}",
                            document.Id, string.Join(",", recoveryProcess));
                    }
                    else
                    {
                        _logger.LogWarning("RecoveryProcess deserialized to null for document {DocumentId}", document.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing RecoveryProcess for document {DocumentId}: {Process}",
                        document.Id, updateDto.RecoveryProcess);
                }
            }
            if (updateDto.Status.HasValue)
            {
                var newStatus = (ProcessingStatus)updateDto.Status.Value;
                var previousStatus = document.Status;

                // Initialize RecoveryProcess list if it's null
                if (document.RecoveryProcessList == null)
                {
                    document.RecoveryProcessList = new List<int>();
                    _logger.LogInformation("Initialized RecoveryProcess list for document {DocumentId}", document.Id);
                }

                // Add new status to recovery process if it's not already the last status
                if (!document.RecoveryProcessList.Any() || document.RecoveryProcessList.Last() != (int)newStatus)
                {
                    document.RecoveryProcessList.Add((int)newStatus);
                    _logger.LogInformation("Added status {Status} to RecoveryProcess for document {DocumentId}. Current process: {Process}",
                        newStatus, document.Id, string.Join(",", document.RecoveryProcessList));
                }

                // If this is a recovery from error state, update recovery statistics
                if (previousStatus == ProcessingStatus.Failed &&
                    (newStatus == ProcessingStatus.Processing || newStatus == ProcessingStatus.EnhancementInProgress))
                {
                    await _ocrStatisticsService.IncrementRecoveredDocumentsAsync(userId);
                    _logger.LogInformation("Document {DocumentId} recovered from error state (status {PreviousStatus} to {NewStatus}) with recovery process: {RecoveryProcess}",
                        id, previousStatus, newStatus, string.Join(",", document.RecoveryProcessList));
                }

                document.Status = newStatus;
                _logger.LogInformation("Document {DocumentId} status changed from {PreviousStatus} to {NewStatus}",
                    id, previousStatus, newStatus);
            }

            document.LastModified = DateTime.UtcNow;
            document.UpdatedBy = updatedBy ?? userId;
            document.UpdatedAt = DateTime.UtcNow;

            // Log the state before saving
            _logger.LogInformation("Document state before saving: {@Document}", new
            {
                document.Id,
                document.Status,
                document.RecoveryProcessList,
                document.LastModified,
                document.UpdatedAt,
                document.UpdatedBy
            });

            try
            {
                var result = await _context.SaveChangesAsync();
                _logger.LogInformation("SaveChangesAsync completed with {Count} changes", result);

                // Verify the changes were saved
                var savedDocument = await _context.Documents.FindAsync(id);
                _logger.LogInformation("Document state after saving: {@Document}", new
                {
                    savedDocument.Id,
                    savedDocument.Status,
                    savedDocument.RecoveryProcessList,
                    savedDocument.LastModified,
                    savedDocument.UpdatedAt,
                    savedDocument.UpdatedBy
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving document {DocumentId} changes", id);
                throw;
            }

            _logger.LogInformation("Document {DocumentId} updated by {UserId}.", id, userId);
            await LogActivity(userId, $"Updated document {document.FileName}");
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"Document {document.FileName} updated successfully.");
        }

        public async Task DeleteDocumentAsync(Guid id, Guid userId, Guid? deletedBy = null)
        {
            _logger.LogInformation("Starting document deletion for document {DocumentId} by user {UserId}", id, userId);

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && !d.IsDeleted);

            if (document == null)
            {
                _logger.LogWarning("Document {DocumentId} not found or already deleted for user {UserId}", id, userId);
                throw new Exception("Document not found or already deleted.");
            }

            _logger.LogDebug("Found document {DocumentId} with IsDeleted = {IsDeleted}, FileName = {FileName}", document.Id, document.IsDeleted, document.FileName);

            document.IsDeleted = true;
            document.DeletedAt = DateTime.UtcNow;
            document.DeletedBy = deletedBy;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Document {DocumentId} soft-deleted by user {UserId}. New IsDeleted status: {IsDeleted}, FileName = {FileName}",
                document.Id, userId, document.IsDeleted, document.FileName);
            await LogActivity(userId, $"Soft deleted document {document.FileName}");
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"Document {document.FileName} soft deleted.");
        }

        public async Task RestoreDocumentAsync(Guid id, Guid userId, Guid? restoredBy = null)
        {
            _logger.LogInformation("Starting document restoration for document {DocumentId} by user {UserId}", id, userId);

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && d.IsDeleted);

            if (document == null)
            {
                _logger.LogWarning("Document {DocumentId} not found or not deleted for user {UserId}", id, userId);
                throw new Exception("Document not found or not deleted.");
            }

            _logger.LogDebug("Found document {DocumentId} with IsDeleted = {IsDeleted}", document.Id, document.IsDeleted);

            document.IsDeleted = false;
            document.DeletedAt = null;
            document.DeletedBy = null;
            document.UpdatedAt = DateTime.UtcNow;
            document.UpdatedBy = restoredBy;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Document {DocumentId} restored by user {UserId}. New IsDeleted status: {IsDeleted}",
                document.Id, userId, document.IsDeleted);
            await LogActivity(userId, $"Restored document {document.FileName}");
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"Document {document.FileName} restored.");
        }

        public async Task TrackDocumentDeletion(TrackDeletionDto trackDeletionDto)
        {
            // Only track permanent deletions
            if (!trackDeletionDto.IsPermanent)
            {
                return;
            }

            var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var userId = GetUserId(); // Get the current user's ID

            // Check if the document exists
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == trackDeletionDto.DocumentId && d.UserId == userId);

            if (document == null)
            {
                // Document doesn't exist, don't track
                return;
            }

            var stats = await _context.DeletionStats
                .FirstOrDefaultAsync(s => s.Month == currentMonth && s.UserId == userId);

            if (stats == null)
            {
                stats = new DeletionStats
                {
                    Month = currentMonth,
                    UserId = userId,
                    DeleteCount = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _context.DeletionStats.Add(stats);
            }

            stats.DeleteCount++;
            stats.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task PermanentlyDeleteDocumentAsync(Guid id, Guid userId, Guid? deletedBy = null)
        {
            var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && d.IsDeleted);
            if (document == null)
                throw new Exception("Document not found or not deleted.");

            // Track the deletion before removing the document
            await TrackDocumentDeletion(new TrackDeletionDto { DocumentId = id, IsPermanent = true });

            // Delete the physical file
            if (File.Exists(document.FilePath))
            {
                File.Delete(document.FilePath);
            }

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Document {DocumentId} permanently deleted by {UserId}.", id, userId);
            await LogActivity(userId, $"Permanently deleted document {document.FileName}");
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"Document {document.FileName} permanently deleted.");
        }

        public async Task BulkPermanentlyDeleteDocumentsAsync(List<Guid> ids, Guid userId, Guid? deletedBy = null)
        {
            _logger.LogInformation("Starting bulk permanent document deletion for {Count} documents by user {UserId}", ids.Count, userId);

            var documents = await _context.Documents
                .Where(d => ids.Contains(d.Id) && d.UserId == userId && d.IsDeleted)
                .ToListAsync();

            if (!documents.Any())
            {
                _logger.LogWarning("No documents found for bulk permanent deletion for user {UserId}", userId);
                throw new Exception("No documents found for permanent deletion.");
            }

            _logger.LogDebug("Found {Count} documents for bulk permanent deletion", documents.Count);

            // Track the deletions before removing the documents
            foreach (var id in ids)
            {
                await TrackDocumentDeletion(new TrackDeletionDto { DocumentId = id, IsPermanent = true });
            }

            foreach (var document in documents)
            {
                // Delete the physical file
                if (File.Exists(document.FilePath))
                {
                    File.Delete(document.FilePath);
                    _logger.LogDebug("Physical file deleted: {FilePath}", document.FilePath);
                }
                _logger.LogDebug("Removing document {DocumentId} from database", document.Id);
            }

            // Remove from database
            _context.Documents.RemoveRange(documents);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully bulk permanently deleted {Count} documents for user {UserId}", documents.Count, userId);
        }

        public async Task<string> GetDocumentDownloadUrlAsync(Guid id, string type, Guid userId)
        {
            var document = await FetchDocument(id, userId, true);
            switch (type.ToLower())
            {
                case "original":
                    return document.FilePath;
                case "extractedtext":
                    // Smart logic: use enhanced text if available, otherwise OCR text
                    string textContent;
                    if (!string.IsNullOrEmpty(document.EnhancedText))
                    {
                        textContent = document.EnhancedText;
                    }
                    else if (!string.IsNullOrEmpty(document.OcrText))
                    {
                        textContent = document.OcrText;
                    }
                    else
                    {
                        throw new Exception("No extracted text is available for this document.");
                    }
                    var textDir = Path.GetDirectoryName(document.FilePath) ?? Path.Combine(_environment.ContentRootPath, "uploads", "temp");
                    var textPath = Path.Combine(textDir, $"{Path.GetFileNameWithoutExtension(document.FileName)}_extracted_text.txt");
                    await System.IO.File.WriteAllTextAsync(textPath, textContent);
                    return textPath;
                case "ocr":  // Legacy support
                    if (string.IsNullOrEmpty(document.OcrText))
                        throw new Exception("OCR text not available.");
                    var directory = Path.GetDirectoryName(document.FilePath) ?? Path.Combine(_environment.ContentRootPath, "uploads", "temp");
                    var ocrPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(document.FileName)}_ocr.txt");
                    await System.IO.File.WriteAllTextAsync(ocrPath, document.OcrText);
                    return ocrPath;
                case "enhanced":  // Legacy support
                    if (string.IsNullOrEmpty(document.EnhancedText))
                        throw new Exception("Enhanced text not available.");
                    var enhancedDir = Path.GetDirectoryName(document.FilePath) ?? Path.Combine(_environment.ContentRootPath, "uploads", "temp");
                    var enhancedPath = Path.Combine(enhancedDir, $"{Path.GetFileNameWithoutExtension(document.FileName)}_enhanced.txt");
                    await System.IO.File.WriteAllTextAsync(enhancedPath, document.EnhancedText);
                    return enhancedPath;
                default:
                    throw new Exception("Invalid download type.");
            }
        }

        public async Task<string> GetDocumentDownloadUrlWithTokenAsync(string token)
        {
            var shareToken = await _context.ShareTokens
                .Include(st => st.Document)
                .FirstOrDefaultAsync(st => st.Token == token && st.ExpiryDate > DateTime.UtcNow && !st.IsDeleted);
            if (shareToken == null)
                throw new Exception("Invalid or expired token.");

            var document = shareToken.Document;
            string filePath;

            switch (shareToken.Type)
            {
                case "Original":
                    filePath = document.FilePath;
                    break;
                case "ExtractedText":
                    // Smart logic: use enhanced text if available, otherwise OCR text
                    string textContent;
                    if (!string.IsNullOrEmpty(document.EnhancedText))
                    {
                        textContent = document.EnhancedText;
                    }
                    else if (!string.IsNullOrEmpty(document.OcrText))
                    {
                        textContent = document.OcrText;
                    }
                    else
                    {
                        throw new Exception("No extracted text is available.");
                    }
                    var textDir = Path.GetDirectoryName(document.FilePath) ?? Path.Combine(_environment.ContentRootPath, "uploads", "temp");
                    filePath = Path.Combine(textDir, $"{Path.GetFileNameWithoutExtension(document.FileName)}_extracted_text.txt");
                    await System.IO.File.WriteAllTextAsync(filePath, textContent);
                    break;
                case "Ocr":  // Legacy support
                    if (string.IsNullOrEmpty(document.OcrText))
                        throw new Exception("OCR text is not available.");
                    var ocrDir = Path.GetDirectoryName(document.FilePath) ?? Path.Combine(_environment.ContentRootPath, "uploads", "temp");
                    filePath = Path.Combine(ocrDir, $"{Path.GetFileNameWithoutExtension(document.FileName)}_ocr.txt");
                    await System.IO.File.WriteAllTextAsync(filePath, document.OcrText);
                    break;
                case "Enhanced":  // Legacy support
                    if (string.IsNullOrEmpty(document.EnhancedText))
                        throw new Exception("Enhanced text is not available.");
                    var enhancedDir = Path.GetDirectoryName(document.FilePath) ?? Path.Combine(_environment.ContentRootPath, "uploads", "temp");
                    filePath = Path.Combine(enhancedDir, $"{Path.GetFileNameWithoutExtension(document.FileName)}_enhanced.txt");
                    await System.IO.File.WriteAllTextAsync(filePath, document.EnhancedText);
                    break;
                default:
                    throw new Exception("Invalid download type.");
            }

            return filePath;
        }

        public async Task TriggerOcrAsync(Guid id, Guid userId, Guid? updatedBy = null)
        {
            _logger.LogInformation("Starting TriggerOcrAsync for document {DocumentId} by user {UserId}", id, userId);

            using var transaction = await _context.Database.BeginTransactionAsync();
            _logger.LogInformation("Started transaction for OCR processing. Transaction ID: {TransactionId}",
                transaction.TransactionId);
            try
            {
                // Fetch the document
                var document = await _context.Documents
                    .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

                if (document == null)
                {
                    _logger.LogError("Document {DocumentId} not found for user {UserId}", id, userId);
                    throw new Exception("Document not found");
                }

                // Check if document is in Processing status
                if (document.Status != ProcessingStatus.Processing)
                {
                    _logger.LogWarning("Document {DocumentId} is not in Processing status. Current status: {Status}", id, document.Status);
                    throw new Exception("Document must be in 'Processing' status for OCR.");
                }

                // Update document status to OcrInProgress
                document.Status = ProcessingStatus.OcrInProgress;
                document.UpdatedBy = updatedBy ?? userId;
                document.LastModified = DateTime.UtcNow;

                // Log tracked entities before save
                LogTrackedEntities("Before save");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Transaction committed successfully");

                // Log transaction state
                _logger.LogInformation("Transaction state after commit: {State}", transaction.TransactionId);

                // Enqueue the OCR job
                BackgroundJob.Enqueue(() => ProcessOcrJob(id, userId));
                _logger.LogInformation("OCR job enqueued for document {DocumentId}", id);

                // Log the activity
                await LogActivity(userId, $"Triggered OCR for document {document.FileName}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during transaction in TriggerOcrAsync for document {DocumentId}. InnerException: {InnerException}",
                    id, ex.InnerException?.Message);
                throw;
            }
        }

        public async Task ProcessOcrJob(Guid id, Guid userId)
        {
            _logger.LogInformation("========================================= STARTING ProcessOcrJob ==========================================");
            _logger.LogInformation("Parameters - DocumentId: {DocumentId}, UserId: {UserId}", id, userId);

            try
            {
                _logger.LogInformation("Attempting to start transaction...");
                using var transaction = await _context.Database.BeginTransactionAsync();
                _logger.LogInformation("Transaction started successfully. Transaction ID: {TransactionId}",
                    transaction.TransactionId);

                _logger.LogInformation("Fetching document from database...");
                var document = await _context.Documents
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

                if (document == null)
                {
                    _logger.LogError("Document {DocumentId} not found for user {UserId}", id, userId);
                    throw new Exception("Document not found");
                }

                _logger.LogInformation("Document found: {DocumentId}, Current status: {Status}, FilePath: {FilePath}",
                    document.Id, document.Status, document.FilePath);

                _logger.LogInformation("Incrementing total attempts for user {UserId}...", userId);
                await _ocrStatisticsService.IncrementTotalAttemptsAsync(userId);
                _logger.LogInformation("Total attempts incremented successfully");

                _logger.LogInformation("Starting OCR processing for document {DocumentId} with language {Language}",
                    document.Id, document.Language);
                var result = await _ocrService.ProcessOcrAsync(document.FilePath, document.Language);
                _logger.LogInformation("=========================================OCR processing completed. Success: {Success}, Confidence: {Confidence}, ProcessingTime: {ProcessingTime}, ErrorMessage: {ErrorMessage}=========================================",

                    result.Success, result.Confidence, result.ProcessingTime, result.ErrorMessage ?? "None");

                if (result.Success)
                {
                    _logger.LogInformation("=========================================OCR was successful, updating document status...=========================================");
                    document.Status = ProcessingStatus.Processing;
                    var ocrResponse = new
                    {
                        confidence = result.Confidence,
                        processingTime = result.ProcessingTime,
                        text = result.Text,
                        suggestions = result.Suggestions,
                        corrections = result.Corrections
                    };
                    document.OcrResponse = JsonSerializer.Serialize(ocrResponse);
                    document.LastModified = DateTime.UtcNow;

                    _logger.LogInformation("Creating DocumentProcessingHistory entry...");
                    var history = new DocumentProcessingHistory
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        UserId = userId,
                        ProcessedAt = DateTime.UtcNow,
                        Confidence = result.Confidence,
                        ProcessingTime = result.ProcessingTime,
                        OcrResponse = document.OcrResponse
                    };

                    _logger.LogInformation("History entry created with ID {HistoryId}", history.Id);
                    _logger.LogInformation("Adding history entry to context...");
                    _context.DocumentProcessingHistory.Add(history);

                    _logger.LogInformation("Current tracked entities before SaveChanges:");
                    LogTrackedEntities("Before SaveChanges");

                    _logger.LogInformation("Attempting to save changes to database...");
                    try
                    {
                        var saveResult = await _context.SaveChangesAsync();
                        _logger.LogInformation("SaveChanges completed successfully. Affected entities: {Count}", saveResult);
                        _logger.LogInformation("Current tracked entities after SaveChanges:");
                        LogTrackedEntities("After SaveChanges");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving changes to database. Error: {ErrorMessage}", ex.Message);
                        _logger.LogInformation("Current tracked entities after error:");
                        LogTrackedEntities("After Error");
                        throw;
                    }

                    _logger.LogInformation("Committing transaction...");
                    try
                    {
                        await transaction.CommitAsync();
                        _logger.LogInformation("Transaction committed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error committing transaction. Error: {ErrorMessage}", ex.Message);
                        throw;
                    }

                    _logger.LogInformation("Incrementing successful attempts for user {UserId}...", userId);
                    await _ocrStatisticsService.IncrementSuccessfulAttemptsAsync(userId);
                    _logger.LogInformation("Successful attempts incremented");

                    // Send notification after OCR is completed successfully
                    _logger.LogInformation("📤 [SignalR] Attempting to send OCR completion notification to user {UserId}", userId);
                    try
                    {
                        _logger.LogInformation("📤 [SignalR] Preparing to send notification to user {UserId} with message: {Message}",
                            userId, $"OCR processing completed for {document.FileName}");
                        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"OCR processing completed for {document.FileName}");
                        _logger.LogInformation("✅ [SignalR] OCR completion notification sent successfully to user {UserId}", userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [SignalR] Failed to send OCR completion notification to user {UserId}. Error: {Error}",
                            userId, ex.Message);
                    }
                }
                else
                {
                    _logger.LogWarning("OCR processing failed. Setting document status to Failed");
                    document.Status = ProcessingStatus.Failed;
                    document.ErrorMessage = result.ErrorMessage;
                    document.LastModified = DateTime.UtcNow;

                    _logger.LogInformation("Creating DocumentProcessingHistory entry for failed attempt...");
                    var history = new DocumentProcessingHistory
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        UserId = userId,
                        ProcessedAt = DateTime.UtcNow,
                        Confidence = result.Confidence,
                        ProcessingTime = result.ProcessingTime,
                        OcrResponse = JsonSerializer.Serialize(new
                        {
                            error = result.ErrorMessage,
                            confidence = result.Confidence,
                            processingTime = result.ProcessingTime
                        })
                    };

                    _logger.LogInformation("History entry created with ID {HistoryId}", history.Id);
                    _logger.LogInformation("Adding history entry to context...");
                    _context.DocumentProcessingHistory.Add(history);

                    _logger.LogInformation("Incrementing failed attempts for user {UserId}...", userId);
                    await _ocrStatisticsService.IncrementFailedAttemptsAsync(userId);
                    _logger.LogInformation("Failed attempts incremented");

                    _logger.LogInformation("Saving failed status to database...");
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }

                _logger.LogInformation("=== COMPLETED ProcessOcrJob ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessOcrJob. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task TriggerEnhancementAsync(Guid id, Guid userId, Guid? updatedBy = null)
        {
            var document = await FetchDocument(id, userId, true);
            if (document.Status == ProcessingStatus.Uploaded)
            {
                await TriggerOcrAsync(id, userId, updatedBy);
                document = await FetchDocument(id, userId, true);
            }
            if (document.Status != ProcessingStatus.OcrComplete)
                throw new Exception("Document must be OCR-complete for enhancement.");

            document.Status = ProcessingStatus.EnhancementInProgress;
            document.UpdatedBy = updatedBy ?? userId;
            document.LastModified = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"Enhancement processing started for {document.FileName}");
            BackgroundJob.Enqueue(() => ProcessEnhancementJob(id, userId));
            _logger.LogInformation("Enhancement triggered for document {DocumentId} by {UserId}.", id, userId);
            await LogActivity(userId, $"Triggered enhancement for document {document.FileName}");
        }

        public async Task ProcessEnhancementJob(Guid documentId, Guid userId)
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null || document.IsDeleted)
                return;

            try
            {
                if (string.IsNullOrEmpty(document.OcrText))
                    throw new Exception("OCR text is not available for enhancement.");

                var llmResult = await _llmService.EnhanceTextAsync(document.OcrText, document.Language, document.Topic);
                document.EnhancedText = llmResult.EnhancedText;
                foreach (var correction in llmResult.Corrections)
                {
                    var textCorrection = new PFE_Back.models.TextCorrection
                    {
                        DocumentId = document.Id,
                        OriginalText = correction.Original,
                        CorrectedText = correction.Alternatives.FirstOrDefault() ?? correction.Original,
                        Confidence = correction.Confidence ?? 1.0f,
                        PageNumber = correction.Page,
                        LineNumber = correction.Line,
                        CharPosition = correction.CharPosition,
                        CreatedBy = userId
                    };
                    _context.TextCorrections.Add(textCorrection);

                    foreach (var alt in correction.Alternatives)
                        _context.CorrectionAlternatives.Add(new CorrectionAlternative
                        {
                            CorrectionId = textCorrection.Id,
                            Text = alt,
                            CreatedBy = userId
                        });
                }
                document.Status = ProcessingStatus.Complete;
                document.LastModified = DateTime.UtcNow;
                document.UpdatedBy = userId;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"Enhancement processing completed for {document.FileName}");
                _logger.LogInformation("Enhancement completed for document {DocumentId}.", documentId);
                await LogActivity(userId, $"Enhancement completed for document {document.FileName}");

                // Send notification after enhancement is completed
                _logger.LogInformation("📤 [SignalR] Attempting to send enhancement completion notification to user {UserId}", userId);
                try
                {
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"Enhancement processing completed for {document.FileName}");
                    _logger.LogInformation("✅ [SignalR] Enhancement completion notification sent successfully to user {UserId}", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [SignalR] Failed to send enhancement completion notification to user {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                document.Status = ProcessingStatus.Failed;
                document.ErrorMessage = $"Enhancement failed: {ex.Message}";
                document.LastModified = DateTime.UtcNow;
                document.UpdatedBy = userId;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", $"Enhancement failed for {document.FileName}: {ex.Message}");
                _logger.LogError("Enhancement failed for document {DocumentId}: {Error}", documentId, ex.Message);
            }
        }

        public async Task ShareDocumentAsync(Guid id, Guid userId, string recipientEmail, DownloadType type)
        {
            var document = await FetchDocument(id, userId, true);
            string filePath;
            string actualType = type.ToString();

            switch (type)
            {
                case DownloadType.Original:
                    filePath = document.FilePath;
                    break;

                case DownloadType.ExtractedText:
                    // Smart logic: use enhanced text if available, otherwise OCR text
                    string textContent;
                    if (!string.IsNullOrEmpty(document.EnhancedText))
                    {
                        textContent = document.EnhancedText;
                        actualType = "Enhanced";
                        _logger.LogInformation("Using enhanced text for ExtractedText share of document {DocumentId}", id);
                    }
                    else if (!string.IsNullOrEmpty(document.OcrText))
                    {
                        textContent = document.OcrText;
                        actualType = "Ocr";
                        _logger.LogInformation("Using OCR text for ExtractedText share of document {DocumentId}", id);
                    }
                    else
                    {
                        throw new Exception("No extracted text is available for this document. Please process the document first.");
                    }

                    var textDir = Path.GetDirectoryName(document.FilePath) ?? Path.Combine(_environment.ContentRootPath, "uploads", "temp");
                    filePath = Path.Combine(textDir, $"{Path.GetFileNameWithoutExtension(document.FileName)}_extracted_text.txt");
                    await System.IO.File.WriteAllTextAsync(filePath, textContent);
                    break;

                case DownloadType.Ocr:  // Legacy support
                    if (string.IsNullOrEmpty(document.OcrText))
                        throw new Exception("OCR text is not available for this document.");
                    var ocrDir = Path.GetDirectoryName(document.FilePath) ?? Path.Combine(_environment.ContentRootPath, "uploads", "temp");
                    filePath = Path.Combine(ocrDir, $"{Path.GetFileNameWithoutExtension(document.FileName)}_ocr.txt");
                    await System.IO.File.WriteAllTextAsync(filePath, document.OcrText);
                    break;

                case DownloadType.Enhanced:  // Legacy support
                    if (string.IsNullOrEmpty(document.EnhancedText))
                        throw new Exception("Enhanced text is not available for this document.");
                    var enhancedDir = Path.GetDirectoryName(document.FilePath) ?? Path.Combine(_environment.ContentRootPath, "uploads", "temp");
                    filePath = Path.Combine(enhancedDir, $"{Path.GetFileNameWithoutExtension(document.FileName)}_enhanced.txt");
                    await System.IO.File.WriteAllTextAsync(filePath, document.EnhancedText);
                    break;

                default:
                    throw new Exception("Invalid download type specified.");
            }

            var shareToken = new ShareToken
            {
                DocumentId = document.Id,
                Token = Guid.NewGuid().ToString(),
                ExpiryDate = DateTime.UtcNow.AddDays(7),
                CreatedBy = userId,
                Type = actualType  // Use the actual type (Enhanced/Ocr for ExtractedText)
            };
            _context.ShareTokens.Add(shareToken);
            await _context.SaveChangesAsync();

            var shareLink = $"{_configuration["AppUrl"]}/api/Documents/download?token={shareToken.Token}";

            // Enhanced email content based on share type
            string shareTypeDisplay = type == DownloadType.ExtractedText ? "Texte extrait" :
                                    type == DownloadType.Original ? "Document original" : type.ToString();

            string emailSubject = $"Document partagé: {document.FileName}";
            string emailTextContent = $@"Bonjour,

Un document vous a été partagé: {document.FileName}

Type de partage: {shareTypeDisplay}
Lien de téléchargement: {shareLink}

Ce lien est valide pendant 7 jours.

Cordialement";

            string emailHtmlContent = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <h2 style='color: #333;'>Document partagé</h2>
    <p>Bonjour,</p>
    <p>Un document vous a été partagé:</p>
    <div style='background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
        <p><strong>Nom du document:</strong> {document.FileName}</p>
        <p><strong>Type de partage:</strong> {shareTypeDisplay}</p>
    </div>
    <div style='text-align: center; margin: 30px 0;'>
        <a href='{shareLink}' style='background-color: #007bff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
            Télécharger le document
        </a>
    </div>
    <p style='color: #666; font-size: 12px;'>Ce lien est valide pendant 7 jours.</p>
</div>";

            try
            {
                var client = new SendGridClient(_configuration["SendGrid:ApiKey"]);
                var msg = MailHelper.CreateSingleEmail(
                    new EmailAddress(_configuration["SendGrid:FromEmail"], _configuration["SendGrid:FromName"]),
                    new EmailAddress(recipientEmail),
                    emailSubject,
                    emailTextContent,
                    emailHtmlContent
                );

                var response = await client.SendEmailAsync(msg);
                _logger.LogInformation("SendGrid response status: {StatusCode}", response.StatusCode);

                if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
                {
                    var responseBody = await response.Body.ReadAsStringAsync();
                    _logger.LogError("SendGrid error: {StatusCode} - {ResponseBody}", response.StatusCode, responseBody);
                    throw new Exception($"Failed to send sharing email. Status: {response.StatusCode}");
                }

                _logger.LogInformation("Document {DocumentId} shared successfully with {Email} via email", id, recipientEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending share email: {Error}", ex.Message);
                throw new Exception($"Failed to send sharing email: {ex.Message}");
            }

            var recipientUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == recipientEmail && !u.IsDeleted);
            if (recipientUser != null)
                await _hubContext.Clients.User(recipientUser.Id.ToString()).SendAsync("ReceiveNotification",
                    $"You have been shared a document: {document.FileName} ({type})");

            _logger.LogInformation("Document {DocumentId} ({Type}) shared with {Email} by {UserId}.", id, type, recipientEmail, userId);
            await LogActivity(userId, $"Shared document {document.FileName} ({type}) with {recipientEmail}");
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification",
                $"Document {document.FileName} ({type}) shared with {recipientEmail}.");
        }

        public async Task<DashboardDataDto> GetDashboardDataAsync(Guid userId, bool useSampleData = false)
        {
            var ocrStats = await _context.OcrStatistics
                .FirstOrDefaultAsync(s => s.UserId == userId) ?? new OcrStatistics
                {
                    UserId = userId,
                    TotalAttempts = 0,
                    SuccessfulAttempts = 0,
                    FailedAttempts = 0,
                    LastUpdated = DateTime.UtcNow
                };

            _logger.LogInformation("GetDashboardDataAsync: Filtering documents for userId={UserId}", userId);

            var documents = await _context.Documents
                .Where(d => d.UserId == userId && !d.IsDeleted)
                .ToListAsync();

            _logger.LogInformation("GetDashboardDataAsync: Found {Count} documents for userId={UserId}", documents.Count, userId);

            var today = DateTime.UtcNow.Date;
            var documentsToday = documents.Count(d => d.UploadDate.Date == today);

            var totalDocuments = documents.Count;
            var processedDocuments = documents.Count(d => d.Status == ProcessingStatus.EnhancementInProgress);
            var processingQueue = documents.Count(d => d.Status == ProcessingStatus.Processing || d.Status == ProcessingStatus.OcrInProgress);

            var totalStorage = documents.Sum(d => d.FileSize);
            var storageGrowth = CalculateStorageGrowth(documents);

            var (averageAccuracy, successRate, errorRate, recoveryRate, characterExtractionRate, totalCharacters) = CalculateQualityMetrics(documents, ocrStats);

            // Only provide sample data when explicitly requested
            if (useSampleData)
            {
                var random = new Random();
                totalDocuments = random.Next(50, 150);
                processedDocuments = (int)(totalDocuments * 0.8);
                documentsToday = random.Next(5, 15);
                processingQueue = random.Next(0, 8);
                averageAccuracy = random.Next(75, 95);
                successRate = random.Next(85, 98);
                errorRate = random.Next(2, 15);
                recoveryRate = random.Next(80, 95);
                characterExtractionRate = random.Next(90, 99);
                totalCharacters = random.Next(50000, 200000);
            }

            var languageDistribution = documents
                .GroupBy(d => NormalizeLanguageName(d.Language))
                .Select(g => new LanguageDistributionDto
                {
                    Name = g.Key,
                    Docs = g.Count()
                })
                .ToList();

            // Only provide sample data when explicitly requested
            if (useSampleData)
            {
                var random = new Random();
                languageDistribution = new List<LanguageDistributionDto>
                {
                    new LanguageDistributionDto { Name = "French", Docs = random.Next(20, 40) },
                    new LanguageDistributionDto { Name = "English", Docs = random.Next(15, 30) },
                    new LanguageDistributionDto { Name = "Arabic", Docs = random.Next(10, 25) }
                };
            }

            var weeklyActivity = CalculateWeeklyActivity(userId, useSampleData);

            // Get the last 6 months
            var lastSixMonths = Enumerable.Range(0, 6)
                .Select(i => DateTime.UtcNow.AddMonths(-i).Date.AddDays(1 - DateTime.UtcNow.Day))
                .ToList();

            // Get deletion stats for the last 6 months
            var deletionStats = await _context.DeletionStats
                .Where(s => lastSixMonths.Contains(s.Month) && s.UserId == userId)
                .ToDictionaryAsync(s => s.Month, s => s.DeleteCount);

            // Calculate monthly trends with both active and deleted counts
            var monthlyTrends = new List<MonthlyTrendDto>();
            foreach (var month in lastSixMonths)
            {
                var deleteCount = deletionStats.GetValueOrDefault(month, 0);
                var activeCount = await _context.Documents
                    .CountAsync(d => d.UserId == userId &&
                              d.CreatedAt.Month == month.Month &&
                              d.CreatedAt.Year == month.Year &&
                              !d.IsDeleted);

                monthlyTrends.Add(new MonthlyTrendDto
                {
                    Month = month.ToString("MMM"),
                    Active = activeCount,
                    Deleted = deleteCount
                });
            }

            // Only provide sample data when explicitly requested
            if (useSampleData)
            {
                var random = new Random();
                var monthNames = new[] { "Jan", "Fév", "Mar", "Avr", "Mai", "Jun" };
                monthlyTrends = monthNames.Select(month => new MonthlyTrendDto
                {
                    Month = month,
                    Active = random.Next(10, 30),
                    Deleted = random.Next(0, 5)
                }).ToList();
            }

            var processingMetrics = new List<ProcessingMetricDto>
            {
                new() { Metric = "Précision", Value = (int)averageAccuracy },
                new() { Metric = "Taux de réussite", Value = (int)successRate },
                new() { Metric = "Taux d'erreur", Value = (int)errorRate },
                new() { Metric = "Taux de récupération", Value = (int)recoveryRate }
            };

            // Calculate document type distribution
            var documentTypeDistribution = new List<PFE_Back.DTOs.dashboard.DocumentTypeDistribution>
            {
                new PFE_Back.DTOs.dashboard.DocumentTypeDistribution { Type = "Documents PDF", Nombre = documents.Count(d => d.FileType.ToLower() == "application/pdf") },
                new PFE_Back.DTOs.dashboard.DocumentTypeDistribution { Type = "Images", Nombre = documents.Count(d =>
                    d.FileType.ToLower().StartsWith("image/") &&
                    (d.FileType.ToLower() == "image/png" ||
                     d.FileType.ToLower() == "image/jpeg" ||
                     d.FileType.ToLower() == "image/webp" ||
                     d.FileType.ToLower() == "image/tiff" ||
                     d.FileType.ToLower() == "image/tif")) }
            };

            // Only provide sample data when explicitly requested
            if (useSampleData)
            {
                var random = new Random();
                documentTypeDistribution = new List<PFE_Back.DTOs.dashboard.DocumentTypeDistribution>
                {
                    new PFE_Back.DTOs.dashboard.DocumentTypeDistribution { Type = "Documents PDF", Nombre = random.Next(15, 35) },
                    new PFE_Back.DTOs.dashboard.DocumentTypeDistribution { Type = "Images", Nombre = random.Next(10, 25) }
                };
            }

            return new DashboardDataDto
            {
                DocumentsProcessed = totalDocuments,
                ProcessedDocuments = processedDocuments,
                DocumentsToday = documentsToday,
                ProcessingTime = CalculateAverageProcessingTime(documents),
                ProcessingQueue = processingQueue,
                AverageAccuracy = averageAccuracy,
                SuccessRate = successRate,
                ErrorRate = errorRate,
                RecoveryRate = recoveryRate,
                CharacterExtractionRate = characterExtractionRate,
                TotalCharacters = totalCharacters.ToString("N0"),
                TotalStorage = (long)totalStorage,
                StorageGrowth = (long)storageGrowth,
                LanguagesDetected = languageDistribution.Select(l => l.Name).ToList(),
                WeeklyActivity = weeklyActivity,
                MonthlyTrends = monthlyTrends,
                LanguageDistribution = languageDistribution,
                ProcessingMetrics = processingMetrics,
                DocumentTypeDistribution = documentTypeDistribution
            };
        }

        private double CalculateStorageGrowth(List<Document> documents)
        {
            var today = DateTime.UtcNow.Date;

            // Calculate total storage used by documents uploaded today
            var todayStorage = documents
                .Where(d => d.UploadDate.Date == today)
                .Sum(d => d.FileSize);

            return todayStorage;
        }

        private (double accuracy, double successRate, double errorRate, double recoveryRate, double extractionRate, double totalCharacters)
            CalculateQualityMetrics(List<Document> documents, OcrStatistics ocrStats)
        {
            // Include documents that have been processed (Processing, EnhancementInProgress, Complete)
            var processedDocs = documents.Where(d =>
                d.Status == ProcessingStatus.Processing ||
                d.Status == ProcessingStatus.EnhancementInProgress ||
                d.Status == ProcessingStatus.Complete ||
                d.Status == ProcessingStatus.Completed).ToList();
            _logger.LogInformation("Found {Count} documents in processed status (Processing, EnhancementInProgress, Complete)", processedDocs.Count);

            var accuracy = 0.0;
            var validDocs = 0;
            var totalProcessingTime = 0.0;

            foreach (var doc in processedDocs)
            {
                _logger.LogInformation("Processing document {DocumentId}", doc.Id);

                try
                {
                    if (string.IsNullOrEmpty(doc.OcrResponse))
                    {
                        _logger.LogWarning("Document {DocumentId} has empty OcrResponse", doc.Id);
                        continue;
                    }

                    _logger.LogInformation("Document {DocumentId} OCR Response: {OcrResponse}", doc.Id, doc.OcrResponse);

                    var ocrResponse = JsonSerializer.Deserialize<PFE_Back.DTOs.dashboard.OcrResponse>(doc.OcrResponse);
                    if (ocrResponse == null)
                    {
                        _logger.LogWarning("Document {DocumentId} failed to deserialize OCR response", doc.Id);
                        continue;
                    }

                    _logger.LogInformation("Document {DocumentId} deserialized OCR response - Confidence: {Confidence}, ProcessingTime: {ProcessingTime}",
                        doc.Id, ocrResponse.Confidence, ocrResponse.ProcessingTime);

                    if (ocrResponse.Confidence > 0)
                    {
                        accuracy += ocrResponse.Confidence * 100;
                        totalProcessingTime += ocrResponse.ProcessingTime;
                        validDocs++;
                        _logger.LogInformation("Document {DocumentId} is valid - Confidence: {Confidence}, ProcessingTime: {ProcessingTime}",
                            doc.Id, ocrResponse.Confidence, ocrResponse.ProcessingTime);
                    }
                    else
                    {
                        _logger.LogWarning("Document {DocumentId} has zero confidence", doc.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to process document {DocumentId}: {Error}", doc.Id, ex.Message);
                    continue;
                }
            }

            accuracy = validDocs > 0 ? accuracy / validDocs : 0;
            _logger.LogInformation("Final calculation - Valid documents: {ValidDocs}, Average accuracy: {Accuracy}%", validDocs, accuracy);

            var successRate = ocrStats.TotalAttempts > 0
                ? (double)ocrStats.SuccessfulAttempts / ocrStats.TotalAttempts * 100
                : 0;

            var errorRate = ocrStats.TotalAttempts > 0
                ? (double)ocrStats.FailedAttempts / ocrStats.TotalAttempts * 100
                : 0;

            var recoveryRate = ocrStats.FailedAttempts > 0
                ? (double)(ocrStats.TotalAttempts - ocrStats.FailedAttempts) / ocrStats.FailedAttempts * 100
                : 100;

            var totalChars = processedDocs.Sum(d => string.IsNullOrEmpty(d.OcrText) ? 0 : d.OcrText.Length);
            var extractionRate = processedDocs.Any()
                ? (double)processedDocs.Count(d => !string.IsNullOrEmpty(d.OcrText)) / processedDocs.Count * 100
                : 0;

            return (accuracy, successRate, errorRate, recoveryRate, extractionRate, totalChars);
        }

        public List<WeeklyActivityDto> CalculateWeeklyActivity(Guid userId = default, bool useSampleData = false)
        {
            _logger.LogInformation("Starting CalculateWeeklyActivity");

            // Calculate the start of the week (7 days ago from today)
            var endOfWeek = DateTime.UtcNow.Date;
            var startOfWeek = endOfWeek.AddDays(-6).Date;
            _logger.LogInformation($"Week range: {startOfWeek} to {endOfWeek}");

            // Initialize results for each day of the week
            var daysOfWeek = new[] { "Lun", "Mar", "Mer", "Jeu", "Ven", "Sam", "Dim" };
            var result = new List<WeeklyActivityDto>();

            // If sample data is requested, return sample data immediately
            if (useSampleData)
            {
                _logger.LogInformation("Sample data requested, providing sample weekly activity");
                var random = new Random();
                return new List<WeeklyActivityDto>
                {
                    new WeeklyActivityDto { Day = "Lun", Documents = random.Next(5, 15), Accuracy = random.Next(75, 95) },
                    new WeeklyActivityDto { Day = "Mar", Documents = random.Next(8, 18), Accuracy = random.Next(80, 95) },
                    new WeeklyActivityDto { Day = "Mer", Documents = random.Next(6, 16), Accuracy = random.Next(70, 90) },
                    new WeeklyActivityDto { Day = "Jeu", Documents = random.Next(10, 20), Accuracy = random.Next(85, 98) },
                    new WeeklyActivityDto { Day = "Ven", Documents = random.Next(7, 17), Accuracy = random.Next(78, 92) },
                    new WeeklyActivityDto { Day = "Sam", Documents = random.Next(3, 13), Accuracy = random.Next(72, 88) },
                    new WeeklyActivityDto { Day = "Dim", Documents = random.Next(2, 12), Accuracy = random.Next(68, 85) }
                };
            }

            // Process each day of the week
            for (int i = 0; i < 7; i++)
            {
                var date = startOfWeek.AddDays(i);
                var dayName = daysOfWeek[((int)date.DayOfWeek + 6) % 7]; // Convert to Monday-based week
                _logger.LogInformation($"\nProcessing day {dayName} ({date})");

                // Get unique documents processed on this day for the specific user
                var uniqueDocuments = _context.DocumentProcessingHistory
                    .Where(h => h.ProcessedAt.Date == date && h.UserId == userId)
                    .Select(h => h.DocumentId)
                    .Distinct()
                    .ToList();

                _logger.LogInformation($"Unique documents processed on {dayName}: {uniqueDocuments.Count}");

                // Calculate average accuracy for these documents
                double accuracy = 0;
                if (uniqueDocuments.Any())
                {
                    var processingHistory = _context.DocumentProcessingHistory
                        .Where(h => uniqueDocuments.Contains(h.DocumentId) &&
                                  h.ProcessedAt.Date == date &&
                                  h.UserId == userId)
                        .ToList();

                    if (processingHistory.Any())
                    {
                        accuracy = processingHistory.Average(h => h.Confidence) * 100;
                    }
                    else
                    {
                        // Fallback to document accuracy if no processing history
                        var documentsWithAccuracy = _context.Documents
                            .Where(d => uniqueDocuments.Contains(d.Id) && d.Accuracy.HasValue)
                            .ToList();

                        if (documentsWithAccuracy.Any())
                        {
                            accuracy = documentsWithAccuracy.Average(d => d.Accuracy.Value) * 100;
                        }
                    }
                }

                _logger.LogInformation($"Accuracy for {dayName}: {accuracy:F2}%");

                result.Add(new WeeklyActivityDto
                {
                    Day = dayName,
                    Documents = uniqueDocuments.Count,
                    Accuracy = accuracy
                });
            }

            _logger.LogInformation("\nFinal weekly activity results:");
            foreach (var day in result)
            {
                _logger.LogInformation($"{day.Day}: {day.Documents} unique documents, {day.Accuracy:F2}% accuracy");
            }



            return result;
        }

        private string CalculateAverageProcessingTime(List<Document> documents)
        {
            // Include documents that have been processed (Processing, EnhancementInProgress, Complete)
            var processedDocs = documents.Where(d =>
                d.Status == ProcessingStatus.Processing ||
                d.Status == ProcessingStatus.EnhancementInProgress ||
                d.Status == ProcessingStatus.Complete ||
                d.Status == ProcessingStatus.Completed).ToList();
            var totalProcessingTime = 0.0;
            var validDocs = 0;

            _logger.LogInformation("CalculateAverageProcessingTime: Found {Count} processed documents", processedDocs.Count);
            foreach (var doc in processedDocs.Take(5)) // Log first 5 for debugging
            {
                _logger.LogInformation("Document {DocumentId}: Status={Status}, OcrResponse={HasOcrResponse}",
                    doc.Id, doc.Status, !string.IsNullOrEmpty(doc.OcrResponse));
            }

            foreach (var doc in processedDocs)
            {
                try
                {
                    if (string.IsNullOrEmpty(doc.OcrResponse))
                        continue;

                    var ocrResponse = JsonSerializer.Deserialize<PFE_Back.DTOs.dashboard.OcrResponse>(doc.OcrResponse);
                    if (ocrResponse != null && ocrResponse.ProcessingTime > 0)
                    {
                        totalProcessingTime += ocrResponse.ProcessingTime;
                        validDocs++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to deserialize OCR response for document {DocumentId}: {Error}",
                        doc.Id, ex.Message);
                    continue;
                }
            }

            var averageTime = validDocs > 0 ? totalProcessingTime / validDocs : 0;
            return $"{averageTime:F1}s";
        }

        private void ValidateUpload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new Exception("File is required.");
            var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png", "image/tiff", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType))
                throw new Exception("Invalid file type. Allowed: PDF, JPEG, PNG, TIFF, WebP.");
            if (file.Length > 50 * 1024 * 1024)
                throw new Exception("File size exceeds 50MB.");
        }

        private async Task<Document> SaveDocument(DocumentUploadDto uploadDto, Guid userId, Guid createdBy)
        {
            var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads", userId.ToString());
            Directory.CreateDirectory(uploadsPath);
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadDto.File.FileName);
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await uploadDto.File.CopyToAsync(stream);

            var document = new Document
            {
                UserId = userId,
                FileName = uploadDto.File.FileName,
                FilePath = filePath,
                FileSize = uploadDto.File.Length,
                FileType = uploadDto.File.ContentType,
                Language = NormalizeLanguageName(uploadDto.Language.ToString()),
                Topic = uploadDto.Topic,
                Description = uploadDto.Description,
                Tags = uploadDto.Tags ?? new List<string>(),
                IsFavorite = uploadDto.IsFavorite,
                Summary = null,
                CreatedBy = createdBy
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
            return document;
        }

        private async Task<Document> FetchDocument(Guid id, Guid userId, bool includeDeleted = false)
        {
            var query = _context.Documents
                .Include(d => d.Corrections)
                .ThenInclude(c => c.Alternatives)
                .Where(d => d.Id == id && d.UserId == userId);

            if (!includeDeleted)
            {
                query = query.Where(d => !d.IsDeleted);
            }

            var document = await query.FirstOrDefaultAsync();
            if (document == null)
                throw new Exception("Document not found or access denied.");
            return document;
        }

        private DocumentDto MapToDocumentDto(Document document)
        {
            return new DocumentDto
            {
                Id = document.Id,
                FileName = document.FileName,
                FilePath = document.FilePath,
                FileSize = document.FileSize,
                FileType = document.FileType,
                Language = document.Language,
                Topic = document.Topic,
                Description = document.Description,
                Tags = document.Tags,
                IsFavorite = document.IsFavorite,
                Status = document.Status,
                ErrorMessage = document.ErrorMessage,
                OcrText = document.OcrText,
                EnhancedText = document.EnhancedText,
                OcrResponse = document.OcrResponse,
                Summary = document.Summary,
                PageCount = document.PageCount,
                UploadDate = document.UploadDate,
                LastModified = document.LastModified,
                DeletedAt = document.DeletedAt,
                IsDeleted = document.IsDeleted,
                RecoveryProcess = document.RecoveryProcessList,
                Corrections = document.Corrections.Select(c => new CorrectionDto
                {
                    Id = c.Id,
                    OriginalText = c.OriginalText,
                    CorrectedText = c.CorrectedText,
                    Confidence = c.Confidence,
                    PageNumber = c.PageNumber,
                    LineNumber = c.LineNumber,
                    CharPosition = c.CharPosition,
                    Alternatives = c.Alternatives.Select(a => new CorrectionAlternativeDto
                    {
                        Id = a.Id,
                        Text = a.Text,
                        CreatedAt = a.CreatedAt,
                        CreatedBy = a.CreatedBy
                    }).ToList(),
                    CreatedAt = c.CreatedAt,
                    CreatedBy = c.CreatedBy
                }).ToList()
            };
        }

        private async Task LogActivity(Guid userId, string action)
        {
            var activity = new ActivityLog
            {
                UserId = userId,
                Action = action,
                Timestamp = DateTime.UtcNow
            };
            await _context.ActivityLogs.AddAsync(activity);
            await _context.SaveChangesAsync();
        }

        private string NormalizeLanguageName(string language)
        {
            return language.ToLower() switch
            {
                "fr" or "french" => "French",
                "en" or "english" => "English",
                "ar" or "arabic" => "Arabic",
                _ => language
            };
        }

        public async Task<ServiceResponse<bool>> DeleteDocumentAsync(string id)
        {
            try
            {
                var document = await _context.Documents.FindAsync(id);
                if (document == null)
                {
                    return new ServiceResponse<bool>
                    {
                        Success = false,
                        Message = "Document not found"
                    };
                }

                document.IsDeleted = true;
                document.DeletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return new ServiceResponse<bool>
                {
                    Data = true,
                    Success = true,
                    Message = "Document soft deleted successfully"
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse<bool>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ServiceResponse<bool>> PermanentlyDeleteDocumentAsync(string id)
        {
            try
            {
                var document = await _context.Documents.FindAsync(id);
                if (document == null)
                {
                    return new ServiceResponse<bool>
                    {
                        Success = false,
                        Message = "Document not found"
                    };
                }

                // Track the deletion before removing the document
                await TrackDocumentDeletion(new TrackDeletionDto { DocumentId = Guid.Parse(id), IsPermanent = true });

                _context.Documents.Remove(document);
                await _context.SaveChangesAsync();

                return new ServiceResponse<bool>
                {
                    Data = true,
                    Success = true,
                    Message = "Document permanently deleted successfully"
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse<bool>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<DashboardResponse> GetDashboardStats()
        {
            var userId = GetUserId(); // Get the current user's ID
            var lastSixMonths = Enumerable.Range(0, 6)
                .Select(i => DateTime.UtcNow.AddMonths(-i).Date.AddDays(1 - DateTime.UtcNow.Day))
                .ToList();

            var deletionStats = await _context.DeletionStats
                .Where(s => lastSixMonths.Contains(s.Month) && s.UserId == userId)
                .ToDictionaryAsync(s => s.Month, s => s.DeleteCount);

            var monthlyTrends = new List<MonthlyTrendDto>();
            foreach (var month in lastSixMonths)
            {
                var deleteCount = deletionStats.GetValueOrDefault(month, 0);
                var activeCount = await _context.Documents
                    .CountAsync(d => d.UserId == userId &&
                              d.CreatedAt.Month == month.Month &&
                              d.CreatedAt.Year == month.Year &&
                              !d.IsDeleted);

                monthlyTrends.Add(new MonthlyTrendDto
                {
                    Month = month.ToString("MMM"),
                    Active = activeCount,
                    Deleted = deleteCount
                });
            }

            return new DashboardResponse
            {
                MonthlyTrends = monthlyTrends
            };
        }

        public async Task BulkDeleteDocumentsAsync(List<Guid> ids, Guid userId, Guid? deletedBy = null)
        {
            var documents = await _context.Documents
                .Where(d => ids.Contains(d.Id) && d.UserId == userId && !d.IsDeleted)
                .ToListAsync();

            if (documents.Count == 0)
            {
                throw new Exception("No documents found");
            }

            foreach (var document in documents)
            {
                document.IsDeleted = true;
                document.DeletedAt = DateTime.UtcNow;
                document.DeletedBy = deletedBy ?? userId;
            }

            await _context.SaveChangesAsync();
            await LogActivity(userId, $"Bulk soft deleted {documents.Count} documents");
        }

        public async Task BulkRestoreDocumentsAsync(List<Guid> ids, Guid userId, Guid? restoredBy = null)
        {
            var documents = await _context.Documents
                .Where(d => ids.Contains(d.Id) && d.UserId == userId && d.IsDeleted)
                .ToListAsync();

            if (documents.Count == 0)
            {
                throw new Exception("No soft-deleted documents found");
            }

            foreach (var document in documents)
            {
                document.IsDeleted = false;
                document.DeletedAt = null;
                document.DeletedBy = null;
                document.UpdatedBy = restoredBy ?? userId;
                document.LastModified = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await LogActivity(userId, $"Bulk restored {documents.Count} documents");
        }

        public async Task<List<DocumentProcessingHistory>> GetDocumentProcessingHistoryAsync(Guid documentId, Guid userId)
        {
            // Verify document ownership
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

            if (document == null)
            {
                throw new Exception("Document not found or access denied.");
            }

            return await _context.DocumentProcessingHistory
                .Where(h => h.DocumentId == documentId)
                .OrderByDescending(h => h.ProcessedAt)
                .ToListAsync();
        }

        private void LogTrackedEntities(string context)
        {
            var tracked = _context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .Select(e => new { Entity = e.Entity.GetType().Name, State = e.State })
                .ToList();
            _logger.LogInformation("{Context} - Tracked entities: {Entities}",
                context,
                string.Join(", ", tracked.Select(e => $"{e.Entity}: {e.State}")));
        }

        public async Task<List<PFE_Back.DTOs.dashboard.DocumentTypeDistribution>> GetDocumentTypeDistributionAsync(Guid userId)
        {
            _logger.LogInformation($"Calculating document type distribution for user {userId}");

            var documents = await _context.Documents
                .Where(d => d.UserId == userId && !d.IsDeleted)
                .ToListAsync();

            var documentTypeDistribution = new List<PFE_Back.DTOs.dashboard.DocumentTypeDistribution>
            {
                new PFE_Back.DTOs.dashboard.DocumentTypeDistribution { Type = "Documents PDF", Nombre = documents.Count(d => d.FileType.ToLower() == "application/pdf") },
                new PFE_Back.DTOs.dashboard.DocumentTypeDistribution { Type = "Images", Nombre = documents.Count(d =>
                    d.FileType.ToLower().StartsWith("image/") &&
                    (d.FileType.ToLower() == "image/png" ||
                     d.FileType.ToLower() == "image/jpeg" ||
                     d.FileType.ToLower() == "image/webp" ||
                     d.FileType.ToLower() == "image/tiff" ||
                     d.FileType.ToLower() == "image/tif")) }
            };

            _logger.LogInformation($"Document type distribution calculated: {string.Join(", ", documentTypeDistribution.Select(d => $"{d.Type}: {d.Nombre}"))}");

            return documentTypeDistribution;
        }

        public async Task<List<ShareTokenDto>> GetDocumentSharesAsync(Guid documentId, Guid userId)
        {
            // Verify document ownership
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId && !d.IsDeleted);

            if (document == null)
                throw new Exception("Document not found or access denied.");

            var shares = await _context.ShareTokens
                .Where(st => st.DocumentId == documentId && !st.IsDeleted)
                .OrderByDescending(st => st.CreatedAt)
                .ToListAsync();

            return shares.Select(st => new ShareTokenDto
            {
                Id = st.Id,
                DocumentId = st.DocumentId,
                Token = st.Token,
                ExpiryDate = st.ExpiryDate,
                Type = st.Type,
                CreatedAt = st.CreatedAt,
                CreatedBy = st.CreatedBy,
                DocumentName = document.FileName,
                RecipientEmail = "Shared via email" // We don't store recipient email in ShareToken
            }).ToList();
        }

        public async Task RevokeDocumentShareAsync(Guid shareId, Guid userId)
        {
            var shareToken = await _context.ShareTokens
                .Include(st => st.Document)
                .FirstOrDefaultAsync(st => st.Id == shareId && st.Document.UserId == userId && !st.IsDeleted);

            if (shareToken == null)
                throw new Exception("Share not found or access denied.");

            shareToken.IsDeleted = true;
            shareToken.DeletedAt = DateTime.UtcNow;
            shareToken.DeletedBy = userId;

            await _context.SaveChangesAsync();
            await LogActivity(userId, $"Revoked share for document {shareToken.Document.FileName}");
        }

        public async Task<DocumentSearchResultDto> GetSharedDocumentsAsync(string userEmail, int page, int pageSize)
        {
            // This would require implementing a proper sharing system with recipient tracking
            // For now, return empty result as the current implementation doesn't track recipients
            return new DocumentSearchResultDto
            {
                Items = new List<DocumentDto>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<AdminDashboardStatsDto> GetAdminDashboardStatsAsync()
        {
            _logger.LogInformation("Fetching admin dashboard statistics");

            try
            {
                // DEVELOPMENT: Caching disabled to ensure real-time updates
                // Try to get from cache first
                var cacheKey = "admin:dashboard:stats";
                // var cachedStats = await _cacheService.GetAsync<AdminDashboardStatsDto>(cacheKey);
                // if (cachedStats != null)
                // {
                //     _logger.LogInformation("Returning cached admin dashboard statistics");
                //     return cachedStats;
                // }

                var now = DateTime.UtcNow;
                var today = now.Date;
                var thirtyDaysAgo = today.AddDays(-30);

                // User statistics
                var totalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
                var activeUsers = await _context.Users.CountAsync(u => !u.IsDeleted && u.LastLoginDate >= thirtyDaysAgo);
                var deletedUsers = await _context.Users.CountAsync(u => u.IsDeleted);
                var usersToday = await _context.Users.CountAsync(u => !u.IsDeleted && u.CreatedAt.Date == today);

                // Document statistics - only count documents from active users
                var totalDocuments = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted)
                    .CountAsync();
                var processedDocuments = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted && d.Status == ProcessingStatus.Completed)
                    .CountAsync();
                var failedDocuments = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted && d.Status == ProcessingStatus.Failed)
                    .CountAsync();
                var documentsInProcessing = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted && d.Status == ProcessingStatus.Processing)
                    .CountAsync();
                var documentsToday = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted && d.CreatedAt.Date == today)
                    .CountAsync();

                // Storage statistics - only count storage from active users
                var totalStorage = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted)
                    .SumAsync(d => (long?)d.FileSize) ?? 0;

                // Processing statistics - only from active users
                var allDocuments = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted && d.ProcessingTime.HasValue)
                    .Select(d => d.ProcessingTime.Value)
                    .ToListAsync();

                var averageProcessingTime = allDocuments.Any() ? allDocuments.Average() : 0;
                var overallSuccessRate = totalDocuments > 0 ? (double)processedDocuments / totalDocuments * 100 : 0;
                var overallErrorRate = totalDocuments > 0 ? (double)failedDocuments / totalDocuments * 100 : 0;

                // Language distribution - only from active users
                var languageDistribution = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted)
                    .GroupBy(d => d.Language)
                    .Select(g => new AdminLanguageDistributionDto
                    {
                        Language = g.Key,
                        Count = g.Count(),
                        Percentage = totalDocuments > 0 ? (double)g.Count() / totalDocuments * 100 : 0
                    })
                    .OrderByDescending(l => l.Count)
                    .ToListAsync();

                // Document type distribution - only from active users
                var documentTypeDistribution = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted)
                    .GroupBy(d => d.FileType)
                    .Select(g => new AdminDocumentTypeDistributionDto
                    {
                        Type = g.Key,
                        Count = g.Count(),
                        Percentage = totalDocuments > 0 ? (double)g.Count() / totalDocuments * 100 : 0
                    })
                    .OrderByDescending(t => t.Count)
                    .ToListAsync();

                // Recent activity (last 7 days)
                var recentActivity = new List<DailyActivityDto>();
                for (int i = 6; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    var dayDocuments = await _context.Documents
                        .Where(d => !d.IsDeleted && !d.User.IsDeleted && d.CreatedAt.Date == date)
                        .ToListAsync();

                    var dayUsers = await _context.Users
                        .Where(u => !u.IsDeleted && u.LastLoginDate.HasValue && u.LastLoginDate.Value.Date == date)
                        .CountAsync();

                    var averageAccuracy = dayDocuments.Where(d => d.Accuracy.HasValue).Any()
                        ? dayDocuments.Where(d => d.Accuracy.HasValue).Average(d => d.Accuracy.Value)
                        : 0;

                    recentActivity.Add(new DailyActivityDto
                    {
                        Date = date,
                        DocumentsProcessed = dayDocuments.Count,
                        UsersActive = dayUsers,
                        AverageAccuracy = averageAccuracy
                    });
                }

                // Monthly trends (last 6 months)
                var monthlyTrends = new List<MonthlyStatsDto>();
                for (int i = 5; i >= 0; i--)
                {
                    var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                    var monthEnd = monthStart.AddMonths(1);

                    var monthDocuments = await _context.Documents
                        .Where(d => !d.IsDeleted && !d.User.IsDeleted && d.CreatedAt >= monthStart && d.CreatedAt < monthEnd)
                        .ToListAsync();

                    var monthActiveUsers = await _context.Users
                        .Where(u => !u.IsDeleted && u.LastLoginDate.HasValue &&
                                   u.LastLoginDate.Value >= monthStart && u.LastLoginDate.Value < monthEnd)
                        .CountAsync();

                    var monthStorageUsed = monthDocuments.Sum(d => d.FileSize);
                    var monthAverageAccuracy = monthDocuments.Where(d => d.Accuracy.HasValue).Any()
                        ? monthDocuments.Where(d => d.Accuracy.HasValue).Average(d => d.Accuracy.Value)
                        : 0;

                    monthlyTrends.Add(new MonthlyStatsDto
                    {
                        Month = monthStart.ToString("MMM yyyy"),
                        DocumentsProcessed = monthDocuments.Count,
                        ActiveUsers = monthActiveUsers,
                        AverageAccuracy = monthAverageAccuracy,
                        StorageUsed = monthStorageUsed
                    });
                }

                var stats = new AdminDashboardStatsDto
                {
                    TotalUsers = totalUsers,
                    ActiveUsers = activeUsers,
                    DeletedUsers = deletedUsers,
                    TotalDocuments = totalDocuments,
                    ProcessedDocuments = processedDocuments,
                    FailedDocuments = failedDocuments,
                    DocumentsInProcessing = documentsInProcessing,
                    TotalStorage = totalStorage,
                    AverageProcessingTime = averageProcessingTime,
                    OverallSuccessRate = overallSuccessRate,
                    OverallErrorRate = overallErrorRate,
                    DocumentsToday = documentsToday,
                    UsersToday = usersToday,
                    LanguageDistribution = languageDistribution,
                    DocumentTypeDistribution = documentTypeDistribution,
                    RecentActivity = recentActivity,
                    MonthlyTrends = monthlyTrends
                };

                // DEVELOPMENT: Caching disabled to ensure real-time updates
                // Cache the results for 5 minutes
                // await _cacheService.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(5));
                _logger.LogInformation("Admin dashboard statistics calculated (caching disabled for development)");

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching admin dashboard statistics");
                throw;
            }
        }

        public async Task<AdminOcrStatsDto> GetAdminOcrStatsAsync()
        {
            _logger.LogInformation("Fetching admin OCR statistics");

            try
            {
                // DEVELOPMENT: Caching disabled to ensure real-time updates
                // Try to get from cache first
                var cacheKey = "admin:ocr:stats";
                // var cachedStats = await _cacheService.GetAsync<AdminOcrStatsDto>(cacheKey);
                // if (cachedStats != null)
                // {
                //     _logger.LogInformation("Returning cached admin OCR statistics");
                //     return cachedStats;
                // }

                var now = DateTime.UtcNow;
                var today = now.Date;

                // Aggregate OCR statistics across active users only
                var allOcrStats = await _context.OcrStatistics
                    .Where(s => !s.User.IsDeleted)
                    .ToListAsync();

                var totalAttempts = allOcrStats.Sum(s => s.TotalAttempts);
                var successfulAttempts = allOcrStats.Sum(s => s.SuccessfulAttempts);
                var failedAttempts = allOcrStats.Sum(s => s.FailedAttempts);
                var recoveredDocuments = allOcrStats.Sum(s => s.RecoveredDocuments);

                var successRate = totalAttempts > 0 ? (double)successfulAttempts / totalAttempts * 100 : 0;
                var failureRate = totalAttempts > 0 ? (double)failedAttempts / totalAttempts * 100 : 0;

                // Calculate average confidence and processing time from documents of active users
                var processedDocuments = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted && d.Status == ProcessingStatus.Completed)
                    .ToListAsync();

                var averageConfidence = processedDocuments.Where(d => d.Accuracy.HasValue).Any()
                    ? processedDocuments.Where(d => d.Accuracy.HasValue).Average(d => d.Accuracy.Value)
                    : 0;

                var averageProcessingTime = processedDocuments.Where(d => d.ProcessingTime.HasValue).Any()
                    ? processedDocuments.Where(d => d.ProcessingTime.HasValue).Average(d => d.ProcessingTime.Value)
                    : 0;

                // Language-specific OCR statistics - only from active users
                var languageStats = await _context.Documents
                    .Where(d => !d.IsDeleted && !d.User.IsDeleted)
                    .GroupBy(d => d.Language)
                    .Select(g => new LanguageOcrStatsDto
                    {
                        Language = g.Key,
                        Attempts = g.Count(),
                        Successful = g.Count(d => d.Status == ProcessingStatus.Completed),
                        SuccessRate = g.Count() > 0 ? (double)g.Count(d => d.Status == ProcessingStatus.Completed) / g.Count() * 100 : 0,
                        AverageConfidence = g.Where(d => d.Accuracy.HasValue).Any() ? g.Where(d => d.Accuracy.HasValue).Average(d => d.Accuracy.Value) : 0
                    })
                    .OrderByDescending(l => l.Attempts)
                    .ToListAsync();

                // Daily OCR statistics (last 7 days)
                var dailyStats = new List<DailyOcrStatsDto>();
                for (int i = 6; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    var dayDocuments = await _context.Documents
                        .Where(d => !d.IsDeleted && !d.User.IsDeleted && d.CreatedAt.Date == date)
                        .ToListAsync();

                    var dayAttempts = dayDocuments.Count;
                    var daySuccessful = dayDocuments.Count(d => d.Status == ProcessingStatus.Completed);
                    var daySuccessRate = dayAttempts > 0 ? (double)daySuccessful / dayAttempts * 100 : 0;
                    var dayAvgProcessingTime = dayDocuments.Where(d => d.ProcessingTime.HasValue).Any()
                        ? dayDocuments.Where(d => d.ProcessingTime.HasValue).Average(d => d.ProcessingTime.Value)
                        : 0;

                    dailyStats.Add(new DailyOcrStatsDto
                    {
                        Date = date,
                        Attempts = dayAttempts,
                        Successful = daySuccessful,
                        SuccessRate = daySuccessRate,
                        AverageProcessingTime = dayAvgProcessingTime
                    });
                }

                var ocrStats = new AdminOcrStatsDto
                {
                    TotalAttempts = totalAttempts,
                    SuccessfulAttempts = successfulAttempts,
                    FailedAttempts = failedAttempts,
                    AverageConfidence = averageConfidence,
                    AverageProcessingTime = averageProcessingTime,
                    SuccessRate = successRate,
                    FailureRate = failureRate,
                    RecoveredDocuments = recoveredDocuments,
                    LanguageStats = languageStats,
                    DailyStats = dailyStats
                };

                // DEVELOPMENT: Caching disabled to ensure real-time updates
                // Cache the results for 5 minutes
                // await _cacheService.SetAsync(cacheKey, ocrStats, TimeSpan.FromMinutes(5));
                _logger.LogInformation("Admin OCR statistics calculated (caching disabled for development)");

                return ocrStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching admin OCR statistics");
                throw;
            }
        }
    }
}