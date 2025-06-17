using PFE_Back.interfaces;

namespace PFE_Back.DTOs.user_DTOs
{
    public class ActivityLogResultDto
    {
        public List<ActivityLogDto> Items { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
