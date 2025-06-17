using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PFE_Back.DTOs;
using PFE_Back.DTOs.dashboard;
using PFE_Back.DTOs.document_DTOs;
using PFE_Back.enums;
using PFE_Back.interfaces;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PFE_Back.services;
using PFE_Back.models;
using Microsoft.AspNetCore.Http;
using PFE_Back.data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using PFE_Back.Hubs;

namespace PFE_Back.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "UserOrAdmin")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DocumentsController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public DocumentsController(IDocumentService documentService, IHttpContextAccessor httpContextAccessor, ILogger<DocumentsController> logger, ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _documentService = documentService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _context = context;
            _hubContext = hubContext;
        }

        private Guid GetUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User not authenticated.");
            return Guid.Parse(userIdClaim);
        }

        [HttpGet]
        public async Task<IActionResult> GetDocuments([FromQuery] DocumentSearchDto? searchDto = null)
        {
            try
            {
                var userId = GetUserId();

                // Log the raw query parameters
                _logger.LogInformation("Raw query parameters: {@Query}", Request.Query);

                // Handle includeDeleted parameter explicitly
                if (searchDto == null)
                    searchDto = new DocumentSearchDto();

                // Log the initial searchDto state
                _logger.LogInformation("Initial searchDto state: {@SearchDto}", searchDto);

                // Check both includeDeleted and includeDeletedNum parameters
                var includeDeletedStr = Request.Query["includeDeleted"].ToString();
                var includeDeletedNumStr = Request.Query["includeDeletedNum"].ToString();

                _logger.LogInformation("Raw includeDeleted value: {Value}, includeDeletedNum value: {NumValue}",
                    includeDeletedStr, includeDeletedNumStr);

                // First try to parse includeDeletedNum if it exists
                if (!string.IsNullOrEmpty(includeDeletedNumStr))
                {
                    if (int.TryParse(includeDeletedNumStr, out int includeDeletedNum))
                    {
                        searchDto.IncludeDeletedNum = includeDeletedNum;
                        searchDto.IncludeDeleted = includeDeletedNum == 1;
                        _logger.LogInformation("Using includeDeletedNum: {Value}, setting IncludeDeleted to {BoolValue}",
                            includeDeletedNum, includeDeletedNum == 1);
                    }
                }
                // If includeDeletedNum wasn't found or couldn't be parsed, try includeDeleted
                else if (!string.IsNullOrEmpty(includeDeletedStr))
                {
                    if (bool.TryParse(includeDeletedStr, out bool includeDeletedBool))
                    {
                        searchDto.IncludeDeleted = includeDeletedBool;
                        searchDto.IncludeDeletedNum = includeDeletedBool ? 1 : 0;
                        _logger.LogInformation("Using includeDeleted: {Value}, setting IncludeDeletedNum to {NumValue}",
                            includeDeletedBool, includeDeletedBool ? 1 : 0);
                    }
                }
                else
                {
                    // If neither parameter was sent, default to false
                    searchDto.IncludeDeleted = false;
                    searchDto.IncludeDeletedNum = 0;
                    _logger.LogInformation("No includeDeleted parameters found, defaulting to false");
                }

                // Log the final searchDto state
                _logger.LogInformation("Final searchDto state: {@SearchDto}", searchDto);

                var result = await _documentService.GetDocumentsAsync(searchDto, userId);
                return Ok(new { Success = true, Message = "Documents retrieved successfully.", Data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDocuments");
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument([FromForm] DocumentUploadDto uploadDto)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation("📤 [DocumentsController] Uploading document for user {UserId}", userId);

                var document = await _documentService.UploadDocumentAsync(uploadDto, userId);
                _logger.LogInformation("✅ [DocumentsController] Document uploaded successfully for user {UserId}", userId);

                return Ok(new { Success = true, Message = "Document uploaded successfully.", Data = document });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [DocumentsController] Error uploading document: {ErrorMessage}", ex.Message);
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("bulk-upload")]
        public async Task<IActionResult> BulkUploadDocuments([FromForm] List<DocumentUploadDto> uploadDtos)
        {
            try
            {
                var userId = GetUserId();
                var documents = await _documentService.BulkUploadDocumentsAsync(uploadDtos, userId);
                return Ok(new { Success = true, Message = "Documents uploaded successfully.", Data = documents });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }
        /*public async Task<IActionResult> BulkUploadDocuments([FromForm] List<IFormFile> files,
            [FromForm] List<DocumentMetadataDto> metadata)
        {
            try
            {
                var userId = GetUserId();

                // Validate that the number of files matches the number of metadata entries
                if (files == null || !files.Any() || metadata == null || files.Count != metadata.Count)
                    throw new Exception("The number of files must match the number of metadata entries.");

                // Combine files and metadata into DocumentUploadDto objects
                var uploadDtos = new List<DocumentUploadDto>();
                for (int i = 0; i < files.Count; i++)
                {
                    uploadDtos.Add(new DocumentUploadDto
                    {
                        File = files[i],
                        Language = metadata[i].language,
                        Topic = metadata[i].topic,
                        Description = metadata[i].description,
                        Tags = metadata[i].tags,
                        IsFavorite = metadata[i].isFavorite
                    });
                }

                // Call the service method (unchanged)
                var documents = await _documentService.BulkUploadDocumentsAsync(uploadDtos, userId);
                return Ok(new { Success = true, Message = "Documents uploaded successfully.", Data = documents });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }*/

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocument(Guid id)
        {
            try
            {
                var userId = GetUserId();
                var document = await _documentService.GetDocumentByIdAsync(id, userId);
                return Ok(new { Success = true, Message = "Document retrieved successfully.", Data = document });
            }
            catch (Exception ex)
            {
                return NotFound(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDocument(Guid id, [FromBody] DocumentUpdateDto updateDto)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.UpdateDocumentAsync(id, updateDto, userId);
                return Ok(new { Success = true, Message = "Document updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.DeleteDocumentAsync(id, userId);
                return Ok(new { Success = true, Message = "Document soft deleted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("{id}/restore")]
        public async Task<IActionResult> RestoreDocument(Guid id)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.RestoreDocumentAsync(id, userId);
                return Ok(new { Success = true, Message = "Document restored successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpDelete("{id}/permanent")]
        public async Task<IActionResult> PermanentlyDeleteDocument(Guid id)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.PermanentlyDeleteDocumentAsync(id, userId);
                return Ok(new { message = "Document permanently deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently deleting document {DocumentId}", id);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> BulkDeleteDocuments([FromBody] List<Guid> ids)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.BulkDeleteDocumentsAsync(ids, userId);
                return Ok(new { Success = true, Message = "Documents soft deleted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("bulk-restore")]
        public async Task<IActionResult> BulkRestoreDocuments([FromBody] List<Guid> ids)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.BulkRestoreDocumentsAsync(ids, userId);
                return Ok(new { Success = true, Message = "Documents restored successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("bulk-permanent-delete")]
        public async Task<IActionResult> BulkPermanentlyDeleteDocuments([FromBody] List<Guid> ids)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.BulkPermanentlyDeleteDocumentsAsync(ids, userId);
                return Ok(new { message = "Documents permanently deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently deleting documents");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadDocument(Guid id, [FromQuery] DownloadType type = DownloadType.Original)
        {
            try
            {
                var userId = GetUserId();
                var filePath = await _documentService.GetDocumentDownloadUrlAsync(id, type.ToString().ToLower(), userId);
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);
                var contentType = type == DownloadType.Original ? "application/octet-stream" : "text/plain";
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("download")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadWithToken([FromQuery] string token)
        {
            try
            {
                var filePath = await _documentService.GetDocumentDownloadUrlWithTokenAsync(token);
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);
                var contentType = Path.GetExtension(filePath).ToLower() == ".txt" ? "text/plain" : "application/octet-stream";
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("{id}/ocr")]
        public async Task<IActionResult> TriggerOcr(Guid id)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.ProcessOcrJob(id, userId);
                return Ok(new { Success = true, Message = "OCR processing started successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("ocr-callback")]
        public async Task<IActionResult> ReceiveOcrResult([FromBody] OcrCallbackRequest request)
        {
            try
            {
                _logger.LogInformation("Received OCR callback for document {DocumentId}", request.DocumentId);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var document = await _context.Documents.FindAsync(request.DocumentId);
                    if (document == null)
                    {
                        _logger.LogError("Document {DocumentId} not found", request.DocumentId);
                        return NotFound($"Document {request.DocumentId} not found");
                    }

                    // Update document accuracy and processing time
                    document.Accuracy = request.Confidence;
                    document.ProcessingTime = request.ProcessingTime;
                    document.LastModified = DateTime.UtcNow;

                    // Create history entry
                    var history = new DocumentProcessingHistory
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        UserId = document.UserId,
                        ProcessedAt = DateTime.UtcNow,
                        Confidence = request.Confidence,
                        ProcessingTime = request.ProcessingTime,
                        OcrResponse = JsonSerializer.Serialize(new
                        {
                            confidence = request.Confidence,
                            processingTime = request.ProcessingTime,
                            text = request.Text,
                            suggestions = request.Suggestions,
                            corrections = request.Corrections
                        })
                    };

                    _context.DocumentProcessingHistory.Add(history);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Send notification to the user
                    var notificationMessage = $"OCR processing completed for {document.FileName}";
                    _logger.LogInformation("Sending notification to user {UserId}: {Message}", document.UserId, notificationMessage);

                    try
                    {
                        await _hubContext.Clients.User(document.UserId.ToString()).SendAsync("ReceiveNotification", notificationMessage);
                        _logger.LogInformation("Notification sent successfully to user {UserId}", document.UserId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send notification to user {UserId}", document.UserId);
                    }

                    _logger.LogInformation("Successfully saved OCR history for document {DocumentId}", request.DocumentId);
                    return Ok();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing OCR callback for document {DocumentId}", request.DocumentId);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OCR callback endpoint");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/enhance")]
        public async Task<IActionResult> TriggerEnhancement(Guid id)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.TriggerEnhancementAsync(id, userId);
                return Ok(new { Success = true, Message = "Enhancement processing started." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("{id}/share")]
        public async Task<IActionResult> ShareDocument(Guid id, [FromBody] ShareDocumentDto shareDto)
        {
            try
            {
                var userId = GetUserId();

                _logger.LogInformation("🔵 [DocumentsController] Sharing document {DocumentId} with {Email}, Type: {Type}", id, shareDto.Email, shareDto.Type);

                // Validate input
                if (string.IsNullOrWhiteSpace(shareDto.Email))
                {
                    _logger.LogWarning("❌ [DocumentsController] Invalid email provided for document sharing");
                    return BadRequest(new { Success = false, Message = "Email is required" });
                }

                if (string.IsNullOrWhiteSpace(shareDto.Type))
                {
                    _logger.LogWarning("❌ [DocumentsController] Invalid type provided for document sharing");
                    return BadRequest(new { Success = false, Message = "Share type is required" });
                }

                // Convert string type to DownloadType enum
                if (!Enum.TryParse<DownloadType>(shareDto.Type, true, out var downloadType))
                {
                    _logger.LogWarning("❌ [DocumentsController] Invalid share type: {Type}", shareDto.Type);
                    return BadRequest(new { Success = false, Message = "Invalid share type. Valid types are: Original, ExtractedText, Ocr, Enhanced" });
                }

                await _documentService.ShareDocumentAsync(id, userId, shareDto.Email, downloadType);
                _logger.LogInformation("✅ [DocumentsController] Document {DocumentId} shared successfully with {Email}", id, shareDto.Email);
                return Ok(new { Success = true, Message = "Document shared successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [DocumentsController] Error sharing document {DocumentId}: {ErrorMessage}", id, ex.Message);
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("{id}/shares")]
        public async Task<IActionResult> GetDocumentShares(Guid id)
        {
            try
            {
                var userId = GetUserId();
                var shares = await _documentService.GetDocumentSharesAsync(id, userId);
                return Ok(new { Success = true, Message = "Document shares retrieved successfully.", Data = shares });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpDelete("shares/{shareId}")]
        public async Task<IActionResult> RevokeDocumentShare(Guid shareId)
        {
            try
            {
                var userId = GetUserId();
                await _documentService.RevokeDocumentShareAsync(shareId, userId);
                return Ok(new { Success = true, Message = "Document share revoked successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("shared-with-me")]
        public async Task<IActionResult> GetSharedDocuments([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetUserId();
                var user = await _context.Users.FindAsync(userId);
                if (user == null) throw new UnauthorizedAccessException("User not found.");

                var sharedDocuments = await _documentService.GetSharedDocumentsAsync(user.Email, page, pageSize);
                return Ok(new { Success = true, Message = "Shared documents retrieved successfully.", Data = sharedDocuments });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("dashboard")]
        [AllowAnonymous] // Allow anonymous access for development
        public async Task<ActionResult<DashboardDataDto>> GetDashboardData([FromQuery] bool useSampleData = false)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                {
                    if (useSampleData)
                    {
                        // For development: provide sample data when explicitly requested
                        _logger.LogInformation("No authenticated user, providing sample dashboard data");
                        var sampleData = await _documentService.GetDashboardDataAsync(Guid.Empty, true);
                        return Ok(sampleData);
                    }
                    else
                    {
                        // For fresh installations: provide real data (zeros) when not authenticated
                        _logger.LogInformation("No authenticated user, providing real dashboard data (zeros)");
                        var realData = await _documentService.GetDashboardDataAsync(Guid.Empty, false);
                        return Ok(realData);
                    }
                }

                var dashboardData = await _documentService.GetDashboardDataAsync(userGuid, false);
                return Ok(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard data");
                return StatusCode(500, "An error occurred while fetching dashboard data");
            }
        }

        [HttpGet("stats/deletions")]
        [Authorize]
        public async Task<ActionResult<DashboardResponse>> GetDeletionStats()
        {
            try
            {
                var stats = await _documentService.GetDashboardStats();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching deletion stats");
                return StatusCode(500, "An error occurred while fetching deletion stats");
            }
        }

        [HttpGet("admin/all")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAllDocumentsAdmin([FromQuery] DocumentSearchDto? searchDto = null)
        {
            try
            {
                _logger.LogInformation("Admin requesting all documents");

                if (searchDto == null)
                    searchDto = new DocumentSearchDto();

                // For admin, we don't filter by userId - get all documents
                var query = _context.Documents.AsQueryable();

                // Apply search filters with sanitization
                if (!string.IsNullOrEmpty(searchDto.SearchTerm))
                {
                    // Sanitize search term to prevent injection
                    var sanitizedSearchTerm = SanitizeSearchTerm(searchDto.SearchTerm);
                    if (!string.IsNullOrEmpty(sanitizedSearchTerm))
                    {
                        query = query.Where(d => d.FileName.Contains(sanitizedSearchTerm) ||
                                               d.Description.Contains(sanitizedSearchTerm));
                    }
                }

                if (searchDto.Language.HasValue)
                {
                    query = query.Where(d => d.Language == searchDto.Language.Value.ToString().ToLower());
                }

                if (searchDto.Status.HasValue)
                {
                    query = query.Where(d => d.Status == searchDto.Status.Value);
                }

                if (!searchDto.IncludeDeleted)
                {
                    query = query.Where(d => !d.IsDeleted);
                }

                var totalCount = await query.CountAsync();

                var documents = await query
                    .Include(d => d.User)
                    .Include(d => d.Corrections)
                    .ThenInclude(c => c.Alternatives)
                    .OrderByDescending(d => d.UploadDate)
                    .Skip((searchDto.Page - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();

                var documentDtos = documents.Select(d => new DocumentDto
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    FileSize = d.FileSize,
                    FileType = d.FileType,
                    Language = d.Language,
                    Topic = d.Topic,
                    Description = d.Description,
                    Tags = d.Tags,
                    IsFavorite = d.IsFavorite,
                    Status = d.Status,
                    ErrorMessage = d.ErrorMessage,
                    OcrText = d.OcrText,
                    EnhancedText = d.EnhancedText,
                    Summary = d.Summary,
                    PageCount = d.PageCount,
                    UploadDate = d.UploadDate,
                    LastModified = d.LastModified,
                    IsDeleted = d.IsDeleted,
                    DeletedAt = d.DeletedAt,
                    UserEmail = d.User?.Email, // Include user email for admin view
                    UserId = d.UserId,
                    Corrections = d.Corrections?.Select(c => new CorrectionDto
                    {
                        Id = c.Id,
                        OriginalText = c.OriginalText,
                        CorrectedText = c.CorrectedText,
                        Confidence = c.Confidence,
                        PageNumber = c.PageNumber,
                        LineNumber = c.LineNumber,
                        CharPosition = c.CharPosition,
                        CreatedAt = c.CreatedAt,
                        CreatedBy = c.CreatedBy,
                        Alternatives = c.Alternatives?.Select(a => new CorrectionAlternativeDto
                        {
                            Id = a.Id,
                            Text = a.Text,
                            CreatedAt = a.CreatedAt,
                            CreatedBy = a.CreatedBy
                        }).ToList() ?? new List<CorrectionAlternativeDto>()
                    }).ToList() ?? new List<CorrectionDto>()
                }).ToList();

                var result = new DocumentSearchResultDto
                {
                    Items = documentDtos,
                    TotalCount = totalCount,
                    Page = searchDto.Page,
                    PageSize = searchDto.PageSize
                };

                return Ok(new { Success = true, Message = "All documents retrieved successfully.", Data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllDocumentsAdmin");
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("track-deletion")]
        [Authorize]
        public async Task<IActionResult> TrackDocumentDeletion([FromBody] PFE_Back.DTOs.document_DTOs.TrackDeletionDto trackDeletionDto)
        {
            try
            {
                await _documentService.TrackDocumentDeletion(trackDeletionDto);
                return Ok(new { message = "Deletion tracked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking document deletion");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/processing-history")]
        public async Task<IActionResult> GetDocumentProcessingHistory(Guid id)
        {
            try
            {
                var userId = GetUserId();
                var history = await _documentService.GetDocumentProcessingHistoryAsync(id, userId);
                return Ok(new { Success = true, Data = history });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        private static string SanitizeSearchTerm(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return string.Empty;

            // Remove potentially dangerous characters and limit length
            var sanitized = searchTerm
                .Replace("'", "")
                .Replace("\"", "")
                .Replace(";", "")
                .Replace("--", "")
                .Replace("/*", "")
                .Replace("*/", "")
                .Replace("xp_", "")
                .Replace("sp_", "")
                .Trim();

            // Limit length to prevent abuse
            return sanitized.Length > 100 ? sanitized.Substring(0, 100) : sanitized;
        }
    }
}