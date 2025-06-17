namespace PFE_Back.DTOs.document_DTOs
{
    public class ShareTokenDto
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string Token { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedBy { get; set; }
        public string DocumentName { get; set; }
        public string RecipientEmail { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpiryDate;
    }
}
