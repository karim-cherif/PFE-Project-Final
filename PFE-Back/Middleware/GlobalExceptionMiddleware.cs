using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace PFE_Back.Middleware
{
    /// <summary>
    /// Global exception handling middleware with comprehensive error logging and user-friendly responses
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred. Request: {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ErrorResponse
            {
                Success = false,
                Timestamp = DateTime.UtcNow,
                Path = context.Request.Path,
                Method = context.Request.Method
            };

            switch (exception)
            {
                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Message = "Access denied. Please check your credentials.";
                    errorResponse.ErrorCode = "UNAUTHORIZED";
                    break;

                case ArgumentNullException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Required parameter is missing.";
                    errorResponse.ErrorCode = "MISSING_PARAMETER";
                    break;

                case ArgumentException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Invalid request parameters.";
                    errorResponse.ErrorCode = "INVALID_PARAMETERS";
                    break;

                case FileNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Message = "The requested resource was not found.";
                    errorResponse.ErrorCode = "RESOURCE_NOT_FOUND";
                    break;

                case InvalidOperationException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "The requested operation is not valid in the current state.";
                    errorResponse.ErrorCode = "INVALID_OPERATION";
                    break;

                case DbUpdateException dbEx:
                    response.StatusCode = (int)HttpStatusCode.Conflict;
                    errorResponse.Message = "A database conflict occurred. Please try again.";
                    errorResponse.ErrorCode = "DATABASE_CONFLICT";

                    // Log detailed database error
                    _logger.LogError(dbEx, "Database update exception: {InnerException}",
                        dbEx.InnerException?.Message);
                    break;

                case TimeoutException:
                    response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    errorResponse.Message = "The request timed out. Please try again.";
                    errorResponse.ErrorCode = "TIMEOUT";
                    break;

                case NotSupportedException:
                    response.StatusCode = (int)HttpStatusCode.NotImplemented;
                    errorResponse.Message = "This operation is not supported.";
                    errorResponse.ErrorCode = "NOT_SUPPORTED";
                    break;

                case HttpRequestException httpEx:
                    response.StatusCode = (int)HttpStatusCode.BadGateway;
                    errorResponse.Message = "External service is unavailable. Please try again later.";
                    errorResponse.ErrorCode = "EXTERNAL_SERVICE_ERROR";

                    _logger.LogError(httpEx, "External service error: {Message}", httpEx.Message);
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = "An unexpected error occurred. Please try again later.";
                    errorResponse.ErrorCode = "INTERNAL_ERROR";

                    // Log full exception details for internal errors
                    _logger.LogError(exception, "Unhandled exception: {ExceptionType} - {Message}",
                        exception.GetType().Name, exception.Message);
                    break;
            }

            // Include stack trace in development environment
            if (_environment.IsDevelopment())
            {
                errorResponse.Details = exception.ToString();
            }

            // Generate correlation ID for tracking
            errorResponse.CorrelationId = Guid.NewGuid().ToString();

            // Add correlation ID to response headers
            response.Headers["X-Correlation-ID"] = errorResponse.CorrelationId;

            // Log with correlation ID
            _logger.LogError("Error response sent. CorrelationId: {CorrelationId}, StatusCode: {StatusCode}, ErrorCode: {ErrorCode}",
                errorResponse.CorrelationId, response.StatusCode, errorResponse.ErrorCode);

            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await response.WriteAsync(jsonResponse);
        }
    }

    public class ErrorResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string? Details { get; set; }
    }

    /// <summary>
    /// Custom exceptions for better error handling
    /// </summary>
    public class BusinessLogicException : Exception
    {
        public string ErrorCode { get; }

        public BusinessLogicException(string message, string errorCode = "BUSINESS_LOGIC_ERROR")
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public BusinessLogicException(string message, Exception innerException, string errorCode = "BUSINESS_LOGIC_ERROR")
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    public class ValidationException : Exception
    {
        public Dictionary<string, string[]> Errors { get; }

        public ValidationException(string message) : base(message)
        {
            Errors = new Dictionary<string, string[]>();
        }

        public ValidationException(Dictionary<string, string[]> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }
    }

    public class ExternalServiceException : Exception
    {
        public string ServiceName { get; }
        public int? StatusCode { get; }

        public ExternalServiceException(string serviceName, string message, int? statusCode = null)
            : base(message)
        {
            ServiceName = serviceName;
            StatusCode = statusCode;
        }

        public ExternalServiceException(string serviceName, string message, Exception innerException, int? statusCode = null)
            : base(message, innerException)
        {
            ServiceName = serviceName;
            StatusCode = statusCode;
        }
    }

    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}
