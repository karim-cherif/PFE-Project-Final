using PFE_Back.enums;

namespace PFE_Back.DTOs.document_DTOs
{
    public class DocumentSearchDto
    {
        public string? SearchTerm { get; set; }
        public SupportedLanguage? Language { get; set; }
        public string? Topic { get; set; }
        public ProcessingStatus? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? Type { get; set; }
        public DocumentType? DocumentType { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        private bool _includeDeleted;
        public bool IncludeDeleted
        {
            get => _includeDeleted;
            set => _includeDeleted = value;
        }
        public int IncludeDeletedNum
        {
            get => _includeDeleted ? 1 : 0;
            set => _includeDeleted = value == 1;
        }
        public bool FavoritesOnly { get; set; } = false;
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; }
    }
}