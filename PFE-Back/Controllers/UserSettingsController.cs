using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PFE_Back.data;
using PFE_Back.models;
using PFE_Back.utils;
using System.Security.Claims;
using System.Text.Json;

namespace PFE_Back.Controllers
{
    [ApiController]
    [Route("api/user/settings")]
    [Authorize]
    public class UserSettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserSettingsController> _logger;

        public UserSettingsController(ApplicationDbContext context, ILogger<UserSettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserSettings()
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                {
                    return Unauthorized("User not found");
                }

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings == null)
                {
                    // Create default settings
                    var defaultSettings = GetDefaultSettings();
                    userSettings = new UserSettings
                    {
                        UserId = userId,
                        SettingsData = JsonSerializer.Serialize(defaultSettings),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.UserSettings.Add(userSettings);
                    await _context.SaveChangesAsync();

                    return Ok(defaultSettings);
                }

                var settings = JsonSerializer.Deserialize<object>(userSettings.SettingsData);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user settings");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUserSettings([FromBody] JsonElement settingsData)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                {
                    return Unauthorized("User not found");
                }

                if (settingsData.ValueKind == JsonValueKind.Undefined)
                {
                    return BadRequest("No settings data provided");
                }

                var settingsJson = settingsData.GetRawText();

                // Validate settings using the validator
                var validationResult = SettingsValidator.ValidateSettings(settingsJson);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.Message);
                }

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings != null)
                {
                    userSettings.SettingsData = settingsJson;
                    userSettings.UpdatedAt = DateTime.UtcNow;
                    _context.UserSettings.Update(userSettings);
                }
                else
                {
                    userSettings = new UserSettings
                    {
                        UserId = userId,
                        SettingsData = settingsJson,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.UserSettings.Add(userSettings);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} updated settings");
                return Ok(new { message = "Settings updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user settings");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("reset")]
        public async Task<IActionResult> ResetUserSettings()
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                {
                    return Unauthorized("User not found");
                }

                var defaultSettings = GetDefaultSettings();
                var settingsJson = JsonSerializer.Serialize(defaultSettings);

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings != null)
                {
                    userSettings.SettingsData = settingsJson;
                    userSettings.UpdatedAt = DateTime.UtcNow;
                    _context.UserSettings.Update(userSettings);
                }
                else
                {
                    userSettings = new UserSettings
                    {
                        UserId = userId,
                        SettingsData = settingsJson,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.UserSettings.Add(userSettings);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} reset settings to defaults");
                return Ok(defaultSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting user settings");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("models")]
        public async Task<IActionResult> GetAvailableModels()
        {
            try
            {
                // Try to get models from Ollama API
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                try
                {
                    var response = await httpClient.GetAsync("http://localhost:11434/api/tags");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var ollamaResponse = JsonSerializer.Deserialize<JsonElement>(content);

                        if (ollamaResponse.TryGetProperty("models", out var modelsElement))
                        {
                            var models = new List<object>();
                            foreach (var model in modelsElement.EnumerateArray())
                            {
                                if (model.TryGetProperty("name", out var nameElement))
                                {
                                    var modelName = nameElement.GetString();
                                    models.Add(new
                                    {
                                        name = modelName,
                                        displayName = GetModelDisplayName(modelName),
                                        size = model.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0,
                                        modified_at = model.TryGetProperty("modified_at", out var modifiedElement) ? modifiedElement.GetString() : ""
                                    });
                                }
                            }
                            return Ok(new { models });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cannot connect to Ollama, returning default models");
                }

                // Return default models if Ollama is not available
                return Ok(new { models = GetDefaultModels() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                return Ok(new { models = GetDefaultModels() });
            }
        }

        private object GetDefaultSettings()
        {
            return new
            {
                ocrSettings = new
                {
                    primaryLanguage = "ara",
                    secondaryLanguage = "",
                    autoDetectLanguage = true,
                    confidenceThreshold = 70,
                    autoEnhancement = true,
                    autoSummary = false,
                    personalDataDetection = true,
                    autoSave = true
                },
                aiModels = new
                {
                    arabic = "qwen3:1.7b",
                    english = "phi4-mini:3.8b",
                    french = "phi4-mini:3.8b"
                },
                uiSettings = new
                {
                    theme = "system",
                    language = "fr",
                    animations = true,
                    sounds = false
                },
                performanceSettings = new
                {
                    processingQuality = "balanced",
                    backgroundProcessing = true,
                    smartCache = true
                }
            };
        }



        private string GetModelDisplayName(string modelName)
        {
            var modelNames = new Dictionary<string, string>
            {
                { "qwen3:1.7b", "Qwen3 1.7B (Recommandé pour l'arabe)" },
                { "phi4-mini:3.8b", "Phi4-Mini 3.8B (Recommandé pour anglais/français)" },
                { "smollm2:1.7b", "SmolLM2 1.7B (Détection précise)" },
                { "deepcoder:1.5b", "DeepCoder 1.5B (Documents techniques)" },
                { "command-r7b-arabic:7b", "Command-R 7B Arabic (Spécialisé arabe)" },
                { "aya-expanse:8b", "Aya Expanse 8B (Multilingue)" },
                { "llama3.2:3b", "Llama 3.2 3B (Polyvalent)" },
                { "mistral:7b", "Mistral 7B (Haute qualité)" },
                { "gemma2:2b", "Gemma 2 2B (Rapide et efficace)" }
            };

            return modelNames.TryGetValue(modelName, out var displayName) ? displayName : modelName;
        }

        private object[] GetDefaultModels()
        {
            return new object[]
            {
                new { name = "qwen3:1.7b", displayName = "Qwen3 1.7B (Recommandé pour l'arabe)" },
                new { name = "phi4-mini:3.8b", displayName = "Phi4-Mini 3.8B (Recommandé pour anglais/français)" },
                new { name = "smollm2:1.7b", displayName = "SmolLM2 1.7B (Détection précise)" },
                new { name = "deepcoder:1.5b", displayName = "DeepCoder 1.5B (Documents techniques)" },
                new { name = "command-r7b-arabic:7b", displayName = "Command-R 7B Arabic (Spécialisé arabe)" },
                new { name = "aya-expanse:8b", displayName = "Aya Expanse 8B (Multilingue)" }
            };
        }
    }
}
