using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PFE_Back.DTOs.user_DTOs;
using PFE_Back.interfaces;
using System.Security.Claims;

namespace PFE_Back.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UsersController(IUserService userService, IHttpContextAccessor httpContextAccessor)
        {
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User not authenticated.");
            return Guid.Parse(userIdClaim);
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var user = await _userService.GetCurrentUserAsync();
                return Ok(new { Success = true, Message = "User retrieved successfully.", Data = user });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateUserDto updateUserDto)
        {
            try
            {
                var user = await _userService.GetCurrentUserAsync();
                await _userService.UpdateUserAsync(user.Id, updateUserDto);
                var updatedUser = await _userService.GetUserByIdAsync(user.Id);
                return Ok(new { Success = true, Message = "User updated successfully.", Data = updatedUser });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                return Ok(new { Success = true, Message = "User retrieved successfully.", Data = user });
            }
            catch (Exception ex)
            {
                return NotFound(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] bool includeDeleted = false)
        {
            try
            {
                var result = await _userService.GetAllUsersAsync(page, pageSize, includeDeleted);
                return Ok(new { Success = true, Message = "Users retrieved successfully.", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] AdminUpdateUserDto adminUpdateUserDto)
        {
            try
            {
                await _userService.AdminUpdateUserAsync(id, adminUpdateUserDto);
                var updatedUser = await _userService.GetUserByIdAsync(id);
                return Ok(new { Success = true, Message = "User updated successfully.", Data = updatedUser });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }



        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _userService.DeleteUserAsync(id, currentUserId);

                // Force cache clearing after user deletion
                Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Add("Pragma", "no-cache");
                Response.Headers.Add("Expires", "0");

                return Ok(new { Success = true, Message = "User soft deleted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("{id}/restore")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> RestoreUser(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _userService.RestoreUserAsync(id, currentUserId);
                return Ok(new { Success = true, Message = "User restored successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpDelete("{id}/permanent")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> PermanentlyDeleteUser(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _userService.PermanentlyDeleteUserAsync(id, currentUserId);

                // Force cache clearing after permanent deletion
                Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Add("Pragma", "no-cache");
                Response.Headers.Add("Expires", "0");

                return Ok(new { Success = true, Message = "User permanently deleted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("bulk-delete")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> BulkDeleteUsers([FromBody] List<Guid> ids)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _userService.BulkDeleteUsersAsync(ids, currentUserId);
                return Ok(new { Success = true, Message = "Users soft deleted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("bulk-restore")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> BulkRestoreUsers([FromBody] List<Guid> ids)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _userService.BulkRestoreUsersAsync(ids, currentUserId);
                return Ok(new { Success = true, Message = "Users restored successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("bulk-permanent-delete")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> BulkPermanentlyDeleteUsers([FromBody] List<Guid> ids)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _userService.BulkPermanentlyDeleteUsersAsync(ids, currentUserId);
                return Ok(new { Success = true, Message = "Users permanently deleted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("activity")]
        [Authorize]
        public async Task<IActionResult> GetUserActivity([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                var activity = await _userService.GetUserActivityAsync(userId, page, pageSize);
                return Ok(new { Success = true, Message = "Activity retrieved successfully.", Data = activity });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("system-activity")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetSystemActivity([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var activity = await _userService.GetSystemActivityAsync(page, pageSize);
                return Ok(new { Success = true, Message = "System activity retrieved successfully.", Data = activity });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("make-admin")]
        [Authorize]
        public async Task<IActionResult> MakeCurrentUserAdmin()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _userService.MakeUserAdminAsync(currentUserId);
                return Ok(new { Success = true, Message = "User is now an admin" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }
    }
}