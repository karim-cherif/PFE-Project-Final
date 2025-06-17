namespace PFE_Back.DTOs.document_DTOs
{
    public class DocumentSearchResultDto
    {
        public List<DocumentDto> Items { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}