using PFE_Back.interfaces;

namespace PFE_Back.services
{
    public class LlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<LlmService> _logger;

        public LlmService(HttpClient httpClient, ILogger<LlmService> logger)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://llm-service:6000");
            _logger = logger;
        }

        public async Task<LlmResult> EnhanceTextAsync(string text, string language, string? topic)
        {
            try
            {
                var requestBody = new { text, language, topic };
                var response = await _httpClient.PostAsJsonAsync("/api/llm/enhance", requestBody);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<LlmResult>();
                if (result == null)
                    throw new Exception("LLM service returned null response.");
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError("LLM service request failed: {Error}", ex.Message);
                throw new Exception("LLM service is unavailable.");
            }
            catch (Exception ex)
            {
                _logger.LogError("LLM enhancement failed: {Error}", ex.Message);
                throw new Exception("An error occurred during LLM enhancement.");
            }
        }
    }
}