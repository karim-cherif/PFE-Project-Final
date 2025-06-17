namespace PFE_Back.DTOs.document_DTOs
{
    public class CorrectionDto
    {
        public Guid Id { get; set; }
        public string OriginalText { get; set; }
        public string CorrectedText { get; set; }
        public float Confidence { get; set; }
        public int PageNumber { get; set; }
        public int LineNumber { get; set; }
        public int CharPosition { get; set; }
        public List<CorrectionAlternativeDto> Alternatives { get; set; } = new List<CorrectionAlternativeDto>();
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
    }
}