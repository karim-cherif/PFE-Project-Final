using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.ComponentModel.DataAnnotations;

namespace PFE_Back.Attributes
{
    /// <summary>
    /// Validates uploaded files for security and size constraints
    /// </summary>
    public class ValidateFileAttribute : ActionFilterAttribute
    {
        private readonly long _maxFileSize;
        private readonly string[] _allowedExtensions;
        private readonly string[] _allowedMimeTypes;

        public ValidateFileAttribute(
            long maxFileSizeInMB = 50,
            string[]? allowedExtensions = null,
            string[]? allowedMimeTypes = null)
        {
            _maxFileSize = maxFileSizeInMB * 1024 * 1024; // Convert to bytes
            _allowedExtensions = allowedExtensions ?? new[] { ".pdf", ".jpg", ".jpeg", ".png", ".tiff", ".bmp" };
            _allowedMimeTypes = allowedMimeTypes ?? new[] 
            { 
                "application/pdf", 
                "image/jpeg", 
                "image/jpg", 
                "image/png", 
                "image/tiff", 
                "image/bmp" 
            };
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var files = context.HttpContext.Request.Form.Files;
            
            foreach (var file in files)
            {
                // Check file size
                if (file.Length > _maxFileSize)
                {
                    context.Result = new BadRequestObjectResult(new 
                    { 
                        Success = false, 
                        Message = $"File {file.FileName} exceeds maximum size of {_maxFileSize / (1024 * 1024)}MB" 
                    });
                    return;
                }

                // Check file extension
                var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                {
                    context.Result = new BadRequestObjectResult(new 
                    { 
                        Success = false, 
                        Message = $"File type {extension} is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}" 
                    });
                    return;
                }

                // Check MIME type
                if (!_allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
                {
                    context.Result = new BadRequestObjectResult(new 
                    { 
                        Success = false, 
                        Message = $"MIME type {file.ContentType} is not allowed" 
                    });
                    return;
                }

                // Check for malicious file content (basic check)
                if (ContainsMaliciousContent(file))
                {
                    context.Result = new BadRequestObjectResult(new 
                    { 
                        Success = false, 
                        Message = "File contains potentially malicious content" 
                    });
                    return;
                }
            }

            base.OnActionExecuting(context);
        }

        private bool ContainsMaliciousContent(IFormFile file)
        {
            // Basic malicious content detection
            var maliciousPatterns = new[]
            {
                "<?php", "<script", "javascript:", "vbscript:", "onload=", "onerror=",
                "eval(", "document.cookie", "window.location", "alert("
            };

            using var reader = new StreamReader(file.OpenReadStream());
            var content = reader.ReadToEnd();
            file.OpenReadStream().Position = 0; // Reset stream position

            return maliciousPatterns.Any(pattern => 
                content.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Validates string input for XSS and injection attacks
    /// </summary>
    public class ValidateInputAttribute : ValidationAttribute
    {
        private readonly int _maxLength;
        private readonly bool _allowHtml;

        public ValidateInputAttribute(int maxLength = 1000, bool allowHtml = false)
        {
            _maxLength = maxLength;
            _allowHtml = allowHtml;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null) return ValidationResult.Success;

            var input = value.ToString();
            if (string.IsNullOrEmpty(input)) return ValidationResult.Success;

            // Check length
            if (input.Length > _maxLength)
            {
                return new ValidationResult($"Input exceeds maximum length of {_maxLength} characters");
            }

            // Check for malicious patterns
            if (!_allowHtml && ContainsMaliciousPatterns(input))
            {
                return new ValidationResult("Input contains potentially malicious content");
            }

            return ValidationResult.Success;
        }

        private bool ContainsMaliciousPatterns(string input)
        {
            var maliciousPatterns = new[]
            {
                "<script", "</script>", "javascript:", "vbscript:", "onload=", "onerror=",
                "eval(", "document.cookie", "window.location", "alert(", "confirm(",
                "prompt(", "innerHTML", "outerHTML", "document.write", "exec(",
                "system(", "shell_exec", "passthru", "file_get_contents", "fopen",
                "SELECT ", "INSERT ", "UPDATE ", "DELETE ", "DROP ", "UNION ",
                "OR 1=1", "' OR '", "\" OR \"", "; --", "/*", "*/"
            };

            return maliciousPatterns.Any(pattern => 
                input.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }
}
