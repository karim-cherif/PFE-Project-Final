namespace PFE_Back.DTOs.document_DTOs
{
    public class CorrectionAlternativeDto
    {
        public Guid Id { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
    }
}