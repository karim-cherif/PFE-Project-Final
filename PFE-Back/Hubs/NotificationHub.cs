using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace PFE_Back.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("🔌 [SignalR] Client connected. ConnectionId: {ConnectionId}, UserId: {UserId}", 
                Context.ConnectionId, userId ?? "anonymous");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("❌ [SignalR] Client disconnected. ConnectionId: {ConnectionId}, UserId: {UserId}, Error: {Error}", 
                Context.ConnectionId, userId ?? "anonymous", exception?.Message ?? "No error");
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendNotification(string userId, string message)
        {
            _logger.LogInformation("📤 [SignalR] Sending notification to user {UserId}: {Message}", userId, message);
            try
            {
                await Clients.User(userId).SendAsync("ReceiveNotification", message);
                _logger.LogInformation("✅ [SignalR] Notification sent successfully to user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SignalR] Failed to send notification to user {UserId}: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }
    }
}