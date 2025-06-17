using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PFE_Back.data;
using PFE_Back.DTOs.user_DTOs;
using PFE_Back.interfaces;
using PFE_Back.models;
using System.Security.Claims;

namespace PFE_Back.services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly ICacheService _cacheService;

        public UserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IHttpContextAccessor httpContextAccessor,
            ApplicationDbContext context,
            ILogger<UserService> logger,
            ICacheService cacheService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _logger = logger;
            _cacheService = cacheService;
        }

        public async Task<UserDto> GetCurrentUserAsync()
        {
            var userId = GetCurrentUserId();
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || user.IsDeleted)
                throw new KeyNotFoundException("User not found.");
            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
            return MapToUserDto(user, role);
        }

        public async Task<UserDto> GetUserByIdAsync(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null || user.IsDeleted)
                throw new KeyNotFoundException("User not found.");
            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
            return MapToUserDto(user, role);
        }

        public async Task<UserSearchResultDto> GetAllUsersAsync(int page, int pageSize, bool includeDeleted)
        {
            var query = includeDeleted ? _userManager.Users : _userManager.Users.Where(u => !u.IsDeleted);
            var totalCount = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userDtos = new List<UserDto>();
            foreach (var user in users)
            {
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
                userDtos.Add(MapToUserDto(user, role));
            }

            return new UserSearchResultDto
            {
                Items = userDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task UpdateUserAsync(Guid id, UpdateUserDto updateUserDto, Guid? updatedBy = null)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null || user.IsDeleted)
                throw new KeyNotFoundException("User not found.");

            UpdateUserFields(user, updateUserDto, updatedBy ?? id);
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new Exception("Update failed.");
            await LogActivity(id, "Updated profile");
        }

        public async Task AdminUpdateUserAsync(Guid id, AdminUpdateUserDto adminUpdateUserDto, Guid? updatedBy = null)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null || user.IsDeleted)
                throw new KeyNotFoundException("User not found.");

            UpdateUserFields(user, new UpdateUserDto
            {
                FirstName = adminUpdateUserDto.FirstName,
                LastName = adminUpdateUserDto.LastName,
                Email = adminUpdateUserDto.Email
            }, updatedBy ?? id);

            if (!string.IsNullOrEmpty(adminUpdateUserDto.Role))
            {
                var roleExists = await _roleManager.RoleExistsAsync(adminUpdateUserDto.Role);
                if (!roleExists)
                    throw new Exception($"Role '{adminUpdateUserDto.Role}' does not exist.");
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, adminUpdateUserDto.Role);
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new Exception("Update failed.");
            await LogActivity(id, "Admin updated user");
        }

        public async Task DeleteUserAsync(Guid id, Guid deletedBy)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null || user.IsDeleted)
                throw new Exception("User not found or already deleted.");

            // Prevent admin from deleting themselves
            if (id == deletedBy)
                throw new Exception("You cannot delete your own account.");

            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletedBy = deletedBy;
            await _userManager.UpdateAsync(user);

            // Clear all caches for this user immediately
            await InvalidateAllUserCachesAsync(id);
            await InvalidateGlobalCachesAsync();

            // Invalidate admin dashboard statistics cache
            await InvalidateAdminStatsCacheAsync();

            await LogActivity(deletedBy, $"Soft deleted user {user.Email}");
        }

        public async Task RestoreUserAsync(Guid id, Guid restoredBy)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null || !user.IsDeleted)
                throw new Exception("User not found or not deleted.");

            user.IsDeleted = false;
            user.DeletedAt = null;
            user.DeletedBy = null;
            user.UpdatedBy = restoredBy;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // Invalidate admin dashboard statistics cache
            await InvalidateAdminStatsCacheAsync();

            await LogActivity(restoredBy, $"Restored user {user.Email}");
        }

        public async Task PermanentlyDeleteUserAsync(Guid id, Guid deletedBy)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                throw new Exception("User not found.");

            // Prevent admin from deleting themselves
            if (id == deletedBy)
                throw new Exception("You cannot permanently delete your own account.");

            // Allow permanent deletion of both soft-deleted and active users
            // This ensures complete removal regardless of current state

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Clear all caches first to prevent stale data
                await InvalidateAllUserCachesAsync(user.Id);

                // Comprehensive user data deletion
                await DeleteAllUserDataAsync(user.Id);

                // Delete the user account completely from Identity tables
                await _userManager.DeleteAsync(user);

                // Clear caches again after deletion
                await InvalidateGlobalCachesAsync();

                // Invalidate admin dashboard statistics cache
                await InvalidateAdminStatsCacheAsync();

                await transaction.CommitAsync();
                await LogActivity(deletedBy, $"Permanently deleted user {user.Email} and all associated data");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to permanently delete user: {ex.Message}");
            }
        }

        public async Task BulkDeleteUsersAsync(List<Guid> ids, Guid deletedBy)
        {
            var users = await _userManager.Users.Where(u => ids.Contains(u.Id) && !u.IsDeleted).ToListAsync();
            if (users.Count != ids.Count)
                throw new Exception("Some users not found or already deleted.");

            foreach (var user in users)
            {
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;
                user.DeletedBy = deletedBy;
                await _userManager.UpdateAsync(user);
            }

            // Invalidate admin dashboard statistics cache
            await InvalidateAdminStatsCacheAsync();

            await LogActivity(deletedBy, $"Bulk soft deleted {users.Count} users");
        }

        public async Task BulkRestoreUsersAsync(List<Guid> ids, Guid restoredBy)
        {
            var users = await _userManager.Users.Where(u => ids.Contains(u.Id) && u.IsDeleted).ToListAsync();
            if (users.Count != ids.Count)
                throw new Exception("Some users not found or not deleted.");

            foreach (var user in users)
            {
                user.IsDeleted = false;
                user.DeletedAt = null;
                user.DeletedBy = null;
                user.UpdatedBy = restoredBy;
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            // Clear caches after bulk restore
            await InvalidateGlobalCachesAsync();

            // Invalidate admin dashboard statistics cache
            await InvalidateAdminStatsCacheAsync();

            await LogActivity(restoredBy, $"Bulk restored {users.Count} users");
        }

        public async Task BulkPermanentlyDeleteUsersAsync(List<Guid> ids, Guid deletedBy)
        {
            var users = await _userManager.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
            if (users.Count == 0)
                throw new Exception("No users found for deletion.");

            // Prevent admin from deleting themselves
            if (ids.Contains(deletedBy))
                throw new Exception("You cannot permanently delete your own account.");

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                foreach (var user in users)
                {
                    // Clear all caches first to prevent stale data
                    await InvalidateAllUserCachesAsync(user.Id);

                    // Comprehensive user data deletion
                    await DeleteAllUserDataAsync(user.Id);

                    // Delete the user account completely from Identity tables
                    await _userManager.DeleteAsync(user);
                }

                // Clear caches again after deletion
                await InvalidateGlobalCachesAsync();

                // Invalidate admin dashboard statistics cache
                await InvalidateAdminStatsCacheAsync();

                await transaction.CommitAsync();
                await LogActivity(deletedBy, $"Bulk permanently deleted {users.Count} users and all associated data");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to bulk permanently delete users: {ex.Message}");
            }
        }



        public async Task<ActivityLogResultDto> GetUserActivityAsync(Guid userId, int page, int pageSize)
        {
            var query = _context.ActivityLogs.Where(a => a.UserId == userId);
            var totalCount = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new ActivityLogResultDto
            {
                Items = logs.Select(l => new ActivityLogDto
                {
                    Id = l.Id,
                    UserId = l.UserId,
                    Action = l.Action,
                    Timestamp = l.Timestamp
                }).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<ActivityLogResultDto> GetSystemActivityAsync(int page, int pageSize)
        {
            var query = _context.ActivityLogs
                .OrderByDescending(a => a.Timestamp);

            var totalCount = await query.CountAsync();
            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get user emails separately to avoid navigation property issues
            var userIds = logs.Select(l => l.UserId).Distinct().ToList();
            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email })
                .ToListAsync();

            var userEmailMap = users.ToDictionary(u => u.Id, u => u.Email);

            return new ActivityLogResultDto
            {
                Items = logs.Select(l => new ActivityLogDto
                {
                    Id = l.Id,
                    UserId = l.UserId,
                    Action = l.Action,
                    Timestamp = l.Timestamp,
                    UserEmail = userEmailMap.TryGetValue(l.UserId, out var email) ? email : "System"
                }).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User not authenticated.");
            return Guid.Parse(userIdClaim);
        }

        private void UpdateUserFields(ApplicationUser user, UpdateUserDto dto, Guid updatedBy)
        {
            if (!string.IsNullOrEmpty(dto.FirstName))
                user.FirstName = dto.FirstName;
            if (!string.IsNullOrEmpty(dto.LastName))
                user.LastName = dto.LastName;
            if (!string.IsNullOrEmpty(dto.Email))
            {
                user.Email = dto.Email;
                user.UserName = dto.Email;
                user.NormalizedEmail = dto.Email.ToUpper();
                user.NormalizedUserName = dto.Email.ToUpper();
            }
            if (dto.ProfilePictureUrl != null)
                user.ProfilePictureUrl = dto.ProfilePictureUrl;
            user.UpdatedBy = updatedBy;
            user.UpdatedAt = DateTime.UtcNow;
        }

        private UserDto MapToUserDto(ApplicationUser user, string role)
        {
            return new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = role,
                ProfilePictureUrl = user.ProfilePictureUrl,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                CreatedBy = user.CreatedBy,
                UpdatedBy = user.UpdatedBy,
                DeletedAt = user.DeletedAt,
                DeletedBy = user.DeletedBy,
                IsDeleted = user.IsDeleted
            };
        }

        private async Task LogActivity(Guid userId, string action)
        {
            _context.ActivityLogs.Add(new ActivityLog { UserId = userId, Action = action });
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Comprehensive deletion of all user-related data across all tables with cache invalidation
        /// </summary>
        private async Task DeleteAllUserDataAsync(Guid userId)
        {
            _logger.LogInformation("Starting comprehensive user data deletion for user: {UserId}", userId);

            try
            {
                // 1. CRITICAL: Clear all caches first to prevent stale data
                await InvalidateAllUserCachesAsync(userId);

                // 2. Delete user documents and all related data (cascading will handle corrections, share tokens, etc.)
                var userDocuments = await _context.Documents
                    .Where(d => d.UserId == userId)
                    .ToListAsync();

                _logger.LogInformation("Found {DocumentCount} documents to delete for user: {UserId}", userDocuments.Count, userId);

                foreach (var document in userDocuments)
                {
                    // Delete physical files if they exist
                    await DeletePhysicalFileAsync(document.FilePath);
                }

                // Remove documents (cascading will handle TextCorrections, ShareTokens, etc.)
                _context.Documents.RemoveRange(userDocuments);

                // 3. Delete user-specific data entities
                var userSettings = await _context.UserSettings
                    .Where(us => us.UserId == userId)
                    .ToListAsync();
                _context.UserSettings.RemoveRange(userSettings);
                _logger.LogInformation("Deleted {Count} user settings for user: {UserId}", userSettings.Count, userId);

                var refreshTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId)
                    .ToListAsync();
                _context.RefreshTokens.RemoveRange(refreshTokens);
                _logger.LogInformation("Deleted {Count} refresh tokens for user: {UserId}", refreshTokens.Count, userId);

                var activityLogs = await _context.ActivityLogs
                    .Where(al => al.UserId == userId)
                    .ToListAsync();
                _context.ActivityLogs.RemoveRange(activityLogs);
                _logger.LogInformation("Deleted {Count} activity logs for user: {UserId}", activityLogs.Count, userId);

                var ocrStatistics = await _context.OcrStatistics
                    .Where(os => os.UserId == userId)
                    .ToListAsync();
                _context.OcrStatistics.RemoveRange(ocrStatistics);
                _logger.LogInformation("Deleted {Count} OCR statistics for user: {UserId}", ocrStatistics.Count, userId);

                var deletionStats = await _context.DeletionStats
                    .Where(ds => ds.UserId == userId)
                    .ToListAsync();
                _context.DeletionStats.RemoveRange(deletionStats);
                _logger.LogInformation("Deleted {Count} deletion stats for user: {UserId}", deletionStats.Count, userId);

                // DocumentProcessingHistory - manually delete since it has NoAction cascade
                var documentProcessingHistory = await _context.DocumentProcessingHistory
                    .Where(dph => dph.UserId == userId)
                    .ToListAsync();
                _context.DocumentProcessingHistory.RemoveRange(documentProcessingHistory);
                _logger.LogInformation("Deleted {Count} document processing history records for user: {UserId}", documentProcessingHistory.Count, userId);

                // 4. Save all deletions in a transaction
                await _context.SaveChangesAsync();

                // 5. CRITICAL: Clear caches again after deletion to ensure no stale data
                await InvalidateAllUserCachesAsync(userId);

                // 6. Clear any global caches that might contain user data
                await InvalidateGlobalCachesAsync();

                _logger.LogInformation("Successfully completed comprehensive user data deletion for user: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during comprehensive user data deletion for user: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Invalidate all user-specific caches
        /// </summary>
        private async Task InvalidateAllUserCachesAsync(Guid userId)
        {
            try
            {
                // User-specific caches
                await _cacheService.RemoveAsync($"user_documents_{userId}");
                await _cacheService.RemoveAsync($"user_profile_{userId}");
                await _cacheService.RemoveAsync($"document_stats_{userId}");
                await _cacheService.RemoveAsync($"ocr_statistics_{userId}");
                await _cacheService.RemoveAsync($"document_type_distribution_{userId}");
                await _cacheService.RemoveAsync($"user_settings_{userId}");
                await _cacheService.RemoveAsync($"user_activity_{userId}");
                await _cacheService.RemoveAsync($"user_processing_history_{userId}");

                // Document-specific caches for user documents
                var userDocuments = await _context.Documents
                    .Where(d => d.UserId == userId)
                    .Select(d => d.Id)
                    .ToListAsync();

                foreach (var docId in userDocuments)
                {
                    await _cacheService.RemoveAsync($"document_{docId}");
                    await _cacheService.RemoveAsync($"document_content_{docId}");
                    await _cacheService.RemoveAsync($"document_corrections_{docId}");
                    await _cacheService.RemoveAsync($"document_share_tokens_{docId}");
                }

                _logger.LogInformation("Invalidated all user-specific caches for user: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating user caches for user: {UserId}", userId);
            }
        }

        /// <summary>
        /// Invalidate global caches that might contain user data
        /// </summary>
        private async Task InvalidateGlobalCachesAsync()
        {
            try
            {
                // Global statistics caches
                await _cacheService.RemoveAsync("admin_dashboard_stats");
                await _cacheService.RemoveAsync("global_user_count");
                await _cacheService.RemoveAsync("global_document_count");
                await _cacheService.RemoveAsync("global_ocr_stats");
                await _cacheService.RemoveAsync("system_statistics");
                await _cacheService.RemoveAsync("recent_documents");
                await _cacheService.RemoveAsync("user_list");
                await _cacheService.RemoveAsync("document_list");

                // Clear all user-related cache patterns
                await _cacheService.RemoveByPatternAsync("user_*");
                await _cacheService.RemoveByPatternAsync("document_*");
                await _cacheService.RemoveByPatternAsync("admin_*");
                await _cacheService.RemoveByPatternAsync("stats_*");

                // Clear authentication and session caches
                await _cacheService.RemoveByPatternAsync("auth_*");
                await _cacheService.RemoveByPatternAsync("session_*");
                await _cacheService.RemoveByPatternAsync("profile_*");

                _logger.LogInformation("Invalidated all global and pattern-based caches");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating global caches");
            }
        }

        /// <summary>
        /// Delete physical file from storage
        /// </summary>
        private async Task DeletePhysicalFileAsync(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the deletion process
                Console.WriteLine($"Warning: Could not delete physical file {filePath}: {ex.Message}");
            }
        }

        private async Task InvalidateAdminStatsCacheAsync()
        {
            try
            {
                // Clear specific admin dashboard statistics caches
                await _cacheService.RemoveAsync("admin:dashboard:stats");
                await _cacheService.RemoveAsync("admin:ocr:stats");
                await _cacheService.RemoveAsync("admin:user:count");
                await _cacheService.RemoveAsync("admin:document:count");
                await _cacheService.RemoveByPatternAsync("admin:stats:*");
                await _cacheService.RemoveByPatternAsync("dashboard:*");
                _logger.LogInformation("Admin statistics caches invalidated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating admin statistics caches");
            }
        }

        public async Task MakeUserAdminAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                throw new ArgumentException("User not found");
            }

            if (!await _userManager.IsInRoleAsync(user, "Admin"))
            {
                var result = await _userManager.AddToRoleAsync(user, "Admin");
                if (!result.Succeeded)
                {
                    throw new Exception("Failed to add admin role");
                }
                _logger.LogInformation("User {UserId} ({Email}) was made admin", user.Id, user.Email);
            }
        }
    }
}