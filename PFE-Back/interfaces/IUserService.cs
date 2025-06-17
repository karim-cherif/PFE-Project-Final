using PFE_Back.DTOs.user_DTOs;

namespace PFE_Back.interfaces
{
    public interface IUserService
    {
        Task<UserDto> GetCurrentUserAsync();
        Task<UserDto> GetUserByIdAsync(Guid id);
        Task<UserSearchResultDto> GetAllUsersAsync(int page, int pageSize, bool includeDeleted = false);
        Task UpdateUserAsync(Guid id, UpdateUserDto updateUserDto, Guid? updatedBy = null);
        Task AdminUpdateUserAsync(Guid id, AdminUpdateUserDto adminUpdateUserDto, Guid? updatedBy = null);
        Task DeleteUserAsync(Guid id, Guid deletedBy);
        Task RestoreUserAsync(Guid id, Guid restoredBy);
        Task PermanentlyDeleteUserAsync(Guid id, Guid deletedBy);
        Task BulkDeleteUsersAsync(List<Guid> ids, Guid deletedBy);
        Task BulkRestoreUsersAsync(List<Guid> ids, Guid restoredBy);
        Task BulkPermanentlyDeleteUsersAsync(List<Guid> ids, Guid deletedBy);
        Task<ActivityLogResultDto> GetUserActivityAsync(Guid userId, int page, int pageSize);
        Task<ActivityLogResultDto> GetSystemActivityAsync(int page, int pageSize);
        Task MakeUserAdminAsync(Guid userId);
    }


}