using PFE_Back.DTOs.dashboard;
using PFE_Back.DTOs.document_DTOs;
using PFE_Back.enums;
using PFE_Back.models;

namespace PFE_Back.interfaces
{
    public interface IDocumentService
    {
        Task<DocumentDto> UploadDocumentAsync(DocumentUploadDto uploadDto, Guid userId, Guid? createdBy = null);
        Task<List<DocumentDto>> BulkUploadDocumentsAsync(List<DocumentUploadDto> uploadDtos, Guid userId, Guid? createdBy = null);
        Task<DocumentDto> GetDocumentByIdAsync(Guid id, Guid userId);
        Task<DocumentSearchResultDto> GetDocumentsAsync(DocumentSearchDto searchDto, Guid userId);
        Task UpdateDocumentAsync(Guid id, DocumentUpdateDto updateDto, Guid userId, Guid? updatedBy = null);
        Task DeleteDocumentAsync(Guid id, Guid userId, Guid? deletedBy = null);
        Task RestoreDocumentAsync(Guid id, Guid userId, Guid? restoredBy = null);
        Task PermanentlyDeleteDocumentAsync(Guid id, Guid userId, Guid? deletedBy = null);
        Task BulkDeleteDocumentsAsync(List<Guid> ids, Guid userId, Guid? deletedBy = null);
        Task BulkRestoreDocumentsAsync(List<Guid> ids, Guid userId, Guid? restoredBy = null);
        Task BulkPermanentlyDeleteDocumentsAsync(List<Guid> ids, Guid userId, Guid? deletedBy = null);
        Task<string> GetDocumentDownloadUrlAsync(Guid id, string type, Guid userId);
        Task<string> GetDocumentDownloadUrlWithTokenAsync(string token);
        Task TriggerOcrAsync(Guid id, Guid userId, Guid? updatedBy = null);
        Task TriggerEnhancementAsync(Guid id, Guid userId, Guid? updatedBy = null);
        Task ProcessOcrJob(Guid documentId, Guid userId);
        Task ProcessEnhancementJob(Guid documentId, Guid userId);
        Task ShareDocumentAsync(Guid id, Guid userId, string recipientEmail, DownloadType type);
        Task<List<ShareTokenDto>> GetDocumentSharesAsync(Guid documentId, Guid userId);
        Task RevokeDocumentShareAsync(Guid shareId, Guid userId);
        Task<DocumentSearchResultDto> GetSharedDocumentsAsync(string userEmail, int page, int pageSize);
        Task<DashboardDataDto> GetDashboardDataAsync(Guid userId, bool useSampleData = false);
        Task<DashboardResponse> GetDashboardStats();
        Task TrackDocumentDeletion(TrackDeletionDto trackDeletionDto);
        Task<List<DocumentProcessingHistory>> GetDocumentProcessingHistoryAsync(Guid documentId, Guid userId);
        Task<List<PFE_Back.DTOs.dashboard.DocumentTypeDistribution>> GetDocumentTypeDistributionAsync(Guid userId);

        // Admin-specific methods
        Task<AdminDashboardStatsDto> GetAdminDashboardStatsAsync();
        Task<AdminOcrStatsDto> GetAdminOcrStatsAsync();
    }
}