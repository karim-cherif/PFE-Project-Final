using System.Text.Json;

namespace PFE_Back.utils
{
    public static class SettingsValidator
    {
        public static ValidationResult ValidateSettings(string settingsJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settingsJson))
                {
                    return new ValidationResult(false, "Settings data cannot be empty");
                }

                var settings = JsonSerializer.Deserialize<JsonElement>(settingsJson);

                // Validate required sections
                if (!settings.TryGetProperty("ocrSettings", out var ocrSettings))
                {
                    return new ValidationResult(false, "Missing ocrSettings section");
                }

                if (!settings.TryGetProperty("aiModels", out var aiModels))
                {
                    return new ValidationResult(false, "Missing aiModels section");
                }

                if (!settings.TryGetProperty("uiSettings", out var uiSettings))
                {
                    return new ValidationResult(false, "Missing uiSettings section");
                }

                if (!settings.TryGetProperty("performanceSettings", out var performanceSettings))
                {
                    return new ValidationResult(false, "Missing performanceSettings section");
                }

                // Validate OCR Settings
                var ocrValidation = ValidateOcrSettings(ocrSettings);
                if (!ocrValidation.IsValid)
                {
                    return ocrValidation;
                }

                // Validate AI Models
                var aiValidation = ValidateAiModels(aiModels);
                if (!aiValidation.IsValid)
                {
                    return aiValidation;
                }

                // Validate UI Settings
                var uiValidation = ValidateUiSettings(uiSettings);
                if (!uiValidation.IsValid)
                {
                    return uiValidation;
                }

                // Validate Performance Settings
                var performanceValidation = ValidatePerformanceSettings(performanceSettings);
                if (!performanceValidation.IsValid)
                {
                    return performanceValidation;
                }

                return new ValidationResult(true, "Settings are valid");
            }
            catch (JsonException)
            {
                return new ValidationResult(false, "Invalid JSON format");
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"Validation error: {ex.Message}");
            }
        }

        private static ValidationResult ValidateOcrSettings(JsonElement ocrSettings)
        {
            // Validate primary language
            if (!ocrSettings.TryGetProperty("primaryLanguage", out var primaryLang) ||
                primaryLang.ValueKind != JsonValueKind.String)
            {
                return new ValidationResult(false, "Primary language is required");
            }

            var primaryLanguage = primaryLang.GetString();
            if (!IsValidLanguage(primaryLanguage))
            {
                return new ValidationResult(false, "Invalid primary language");
            }

            // Validate secondary language (optional)
            if (ocrSettings.TryGetProperty("secondaryLanguage", out var secondaryLang) &&
                secondaryLang.ValueKind == JsonValueKind.String)
            {
                var secondaryLanguage = secondaryLang.GetString();
                if (!string.IsNullOrEmpty(secondaryLanguage) && !IsValidLanguage(secondaryLanguage))
                {
                    return new ValidationResult(false, "Invalid secondary language");
                }
            }

            // Validate confidence threshold
            if (!ocrSettings.TryGetProperty("confidenceThreshold", out var threshold) ||
                threshold.ValueKind != JsonValueKind.Number)
            {
                return new ValidationResult(false, "Confidence threshold must be a number");
            }

            var thresholdValue = threshold.GetInt32();
            if (thresholdValue < 10 || thresholdValue > 95)
            {
                return new ValidationResult(false, "Confidence threshold must be between 10 and 95");
            }

            // Validate boolean settings
            var booleanSettings = new[] { "autoDetectLanguage", "autoEnhancement", "autoSummary", "personalDataDetection", "autoSave" };
            foreach (var setting in booleanSettings)
            {
                if (!ocrSettings.TryGetProperty(setting, out var value) ||
                    value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                {
                    return new ValidationResult(false, $"{setting} must be a boolean value");
                }
            }

            return new ValidationResult(true, "OCR settings are valid");
        }

        private static ValidationResult ValidateAiModels(JsonElement aiModels)
        {
            var requiredModels = new[] { "arabic", "english", "french" };
            
            foreach (var model in requiredModels)
            {
                if (!aiModels.TryGetProperty(model, out var modelValue) ||
                    modelValue.ValueKind != JsonValueKind.String)
                {
                    return new ValidationResult(false, $"AI model for {model} is required");
                }

                var modelName = modelValue.GetString();
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    return new ValidationResult(false, $"AI model for {model} cannot be empty");
                }
            }

            return new ValidationResult(true, "AI models are valid");
        }

        private static ValidationResult ValidateUiSettings(JsonElement uiSettings)
        {
            // Validate theme
            if (!uiSettings.TryGetProperty("theme", out var theme) ||
                theme.ValueKind != JsonValueKind.String)
            {
                return new ValidationResult(false, "Theme is required");
            }

            var themeValue = theme.GetString();
            var validThemes = new[] { "light", "dark", "system" };
            if (!validThemes.Contains(themeValue))
            {
                return new ValidationResult(false, "Invalid theme value");
            }

            // Validate language
            if (!uiSettings.TryGetProperty("language", out var language) ||
                language.ValueKind != JsonValueKind.String)
            {
                return new ValidationResult(false, "UI language is required");
            }

            var languageValue = language.GetString();
            var validLanguages = new[] { "fr", "en", "ar" };
            if (!validLanguages.Contains(languageValue))
            {
                return new ValidationResult(false, "Invalid UI language");
            }

            // Validate boolean settings
            var booleanSettings = new[] { "animations", "sounds" };
            foreach (var setting in booleanSettings)
            {
                if (!uiSettings.TryGetProperty(setting, out var value) ||
                    value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                {
                    return new ValidationResult(false, $"{setting} must be a boolean value");
                }
            }

            return new ValidationResult(true, "UI settings are valid");
        }

        private static ValidationResult ValidatePerformanceSettings(JsonElement performanceSettings)
        {
            // Validate processing quality
            if (!performanceSettings.TryGetProperty("processingQuality", out var quality) ||
                quality.ValueKind != JsonValueKind.String)
            {
                return new ValidationResult(false, "Processing quality is required");
            }

            var qualityValue = quality.GetString();
            var validQualities = new[] { "fast", "balanced", "high" };
            if (!validQualities.Contains(qualityValue))
            {
                return new ValidationResult(false, "Invalid processing quality");
            }

            // Validate boolean settings
            var booleanSettings = new[] { "backgroundProcessing", "smartCache" };
            foreach (var setting in booleanSettings)
            {
                if (!performanceSettings.TryGetProperty(setting, out var value) ||
                    value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                {
                    return new ValidationResult(false, $"{setting} must be a boolean value");
                }
            }

            return new ValidationResult(true, "Performance settings are valid");
        }

        private static bool IsValidLanguage(string language)
        {
            var validLanguages = new[] { "ara", "eng", "fra" };
            return validLanguages.Contains(language);
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }

        public ValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }
    }
}
