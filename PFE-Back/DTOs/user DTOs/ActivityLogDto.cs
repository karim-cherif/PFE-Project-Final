namespace PFE_Back.DTOs.user_DTOs
{
    public class ActivityLogDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Action { get; set; }
        public DateTime Timestamp { get; set; }
        public string? UserEmail { get; set; }
    }
}
