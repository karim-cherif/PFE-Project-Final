using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PFE_Back.DTOs;
using PFE_Back.interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PFE_Back.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OcrStatisticsController : ControllerBase
    {
        private readonly IOcrStatisticsService _ocrStatisticsService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OcrStatisticsController(IOcrStatisticsService ocrStatisticsService, IHttpContextAccessor httpContextAccessor)
        {
            _ocrStatisticsService = ocrStatisticsService;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid GetUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User not authenticated.");
            return Guid.Parse(userIdClaim);
        }

        [HttpGet]
        public async Task<ActionResult<OcrStatisticsDto>> GetStatistics()
        {
            try
            {
                var userId = GetUserId();
                var stats = await _ocrStatisticsService.GetStatisticsAsync(userId);
                return Ok(new { Success = true, Message = "Statistics retrieved successfully.", Data = stats });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("increment-total")]
        public async Task<ActionResult<OcrStatisticsDto>> IncrementTotalAttempts()
        {
            try
            {
                var userId = GetUserId();
                var stats = await _ocrStatisticsService.IncrementTotalAttemptsAsync(userId);
                return Ok(new { Success = true, Message = "Total attempts incremented successfully.", Data = stats });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("increment-success")]
        public async Task<ActionResult<OcrStatisticsDto>> IncrementSuccessfulAttempts()
        {
            try
            {
                var userId = GetUserId();
                var stats = await _ocrStatisticsService.IncrementSuccessfulAttemptsAsync(userId);
                return Ok(new { Success = true, Message = "Successful attempts incremented successfully.", Data = stats });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("increment-failed")]
        public async Task<ActionResult<OcrStatisticsDto>> IncrementFailedAttempts()
        {
            try
            {
                var userId = GetUserId();
                var stats = await _ocrStatisticsService.IncrementFailedAttemptsAsync(userId);
                return Ok(new { Success = true, Message = "Failed attempts incremented successfully.", Data = stats });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }
    }
} 