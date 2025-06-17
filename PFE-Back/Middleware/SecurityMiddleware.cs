using Microsoft.Extensions.Primitives;
using System.Net;
using System.Text.Json;

namespace PFE_Back.Middleware
{
    /// <summary>
    /// Comprehensive security middleware for request validation and protection
    /// </summary>
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMiddleware> _logger;
        private readonly SecurityOptions _options;

        public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger, SecurityOptions options)
        {
            _next = next;
            _logger = logger;
            _options = options;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Rate limiting check
                if (!await CheckRateLimit(context))
                {
                    await WriteErrorResponse(context, HttpStatusCode.TooManyRequests, "Rate limit exceeded");
                    return;
                }

                // Request size validation
                if (!ValidateRequestSize(context))
                {
                    await WriteErrorResponse(context, HttpStatusCode.RequestEntityTooLarge, "Request too large");
                    return;
                }

                // SQL injection detection
                if (DetectSqlInjection(context))
                {
                    _logger.LogWarning("SQL injection attempt detected from {IP}: {Path}",
                        context.Connection.RemoteIpAddress, context.Request.Path);
                    await WriteErrorResponse(context, HttpStatusCode.BadRequest, "Invalid request");
                    return;
                }

                // XSS detection
                if (DetectXss(context))
                {
                    _logger.LogWarning("XSS attempt detected from {IP}: {Path}",
                        context.Connection.RemoteIpAddress, context.Request.Path);
                    await WriteErrorResponse(context, HttpStatusCode.BadRequest, "Invalid request");
                    return;
                }

                // Add security headers
                AddSecurityHeaders(context);

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Security middleware error");
                await WriteErrorResponse(context, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        private Task<bool> CheckRateLimit(HttpContext context)
        {
            // Simple in-memory rate limiting (for production, use Redis or similar)
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = $"rate_limit_{clientIp}";

            // This is a simplified implementation
            // In production, implement proper distributed rate limiting
            return Task.FromResult(true); // Placeholder
        }

        private bool ValidateRequestSize(HttpContext context)
        {
            if (context.Request.ContentLength.HasValue)
            {
                return context.Request.ContentLength.Value <= _options.MaxRequestSizeBytes;
            }
            return true;
        }

        private bool DetectSqlInjection(HttpContext context)
        {
            var sqlPatterns = new[]
            {
                @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|UNION|CREATE|ALTER|EXEC|EXECUTE)\b)",
                @"(\b(OR|AND)\s+\d+\s*=\s*\d+)",
                @"('|\s)(OR|AND)(\s|')(\d+|')",
                @"(;|\s)(--)(\s|$)",
                @"(/\*|\*/)",
                @"(\bxp_cmdshell\b)",
                @"(\bsp_executesql\b)"
            };

            var queryString = context.Request.QueryString.ToString();
            var requestBody = GetRequestBody(context);

            foreach (var pattern in sqlPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(queryString, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                    System.Text.RegularExpressions.Regex.IsMatch(requestBody, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool DetectXss(HttpContext context)
        {
            var xssPatterns = new[]
            {
                @"<script[^>]*>.*?</script>",
                @"javascript:",
                @"vbscript:",
                @"onload\s*=",
                @"onerror\s*=",
                @"onclick\s*=",
                @"onmouseover\s*=",
                @"eval\s*\(",
                @"document\.cookie",
                @"document\.write",
                @"window\.location"
            };

            var queryString = context.Request.QueryString.ToString();
            var requestBody = GetRequestBody(context);

            foreach (var pattern in xssPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(queryString, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                    System.Text.RegularExpressions.Regex.IsMatch(requestBody, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetRequestBody(HttpContext context)
        {
            // For performance, only check small request bodies
            if (context.Request.ContentLength > 1024 * 10) // 10KB limit for security checks
                return string.Empty;

            try
            {
                context.Request.EnableBuffering();
                context.Request.Body.Position = 0;
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = reader.ReadToEndAsync().Result;
                context.Request.Body.Position = 0;
                return body;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void AddSecurityHeaders(HttpContext context)
        {
            var headers = context.Response.Headers;

            // Remove server information
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            // Add security headers
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["X-XSS-Protection"] = "1; mode=block";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            // Add HSTS for HTTPS
            if (context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }
        }

        private async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var response = new
            {
                Success = false,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }

    public class SecurityOptions
    {
        public long MaxRequestSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB
        public int RateLimitRequestsPerMinute { get; set; } = 100;
        public bool EnableSqlInjectionDetection { get; set; } = true;
        public bool EnableXssDetection { get; set; } = true;
    }

    public static class SecurityMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityMiddleware(this IApplicationBuilder builder, SecurityOptions? options = null)
        {
            return builder.UseMiddleware<SecurityMiddleware>(options ?? new SecurityOptions());
        }
    }
}
