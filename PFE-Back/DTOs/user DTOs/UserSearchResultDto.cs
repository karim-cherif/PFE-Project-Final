namespace PFE_Back.DTOs.user_DTOs
{
    public class UserSearchResultDto
    {
        public List<UserDto> Items { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
