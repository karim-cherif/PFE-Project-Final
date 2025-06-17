using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PFE_Back.interfaces;
using PFE_Back.models;
using PFE_Back.services;
using PFE_Back.DTOs;
using PFE_Back.DTOs.dashboard;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using System;

namespace PFE_Back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IUserService _userService;
        private readonly IOcrStatisticsService _ocrStatisticsService;

        public DashboardController(
            IDocumentService documentService,
            IUserService userService,
            IOcrStatisticsService ocrStatisticsService)
        {
            _documentService = documentService;
            _userService = userService;
            _ocrStatisticsService = ocrStatisticsService;
        }

        [HttpGet("document-type-distribution")]
        public async Task<ActionResult<List<PFE_Back.DTOs.dashboard.DocumentTypeDistribution>>> GetDocumentTypeDistribution()
        {
            try
            {
                var userId = GetUserId();
                var distribution = await _documentService.GetDocumentTypeDistributionAsync(userId);
                return Ok(distribution);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("admin/stats")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AdminDashboardStatsDto>> GetAdminDashboardStats()
        {
            try
            {
                var stats = await _documentService.GetAdminDashboardStatsAsync();
                return Ok(new { Success = true, Data = stats });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("admin/ocr-stats")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AdminOcrStatsDto>> GetAdminOcrStats()
        {
            try
            {
                var stats = await _documentService.GetAdminOcrStatsAsync();
                return Ok(new { Success = true, Data = stats });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User not authenticated.");
            return Guid.Parse(userIdClaim);
        }
    }
}