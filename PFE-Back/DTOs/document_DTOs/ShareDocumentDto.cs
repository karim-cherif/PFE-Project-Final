using PFE_Back.enums;

namespace PFE_Back.DTOs.document_DTOs
{
    public class ShareDocumentDto
    {
        public string Email { get; set; }
        public string Type { get; set; } = "Original"; // Accept string and convert to enum
    }
}
