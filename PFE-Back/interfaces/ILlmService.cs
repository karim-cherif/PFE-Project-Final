namespace PFE_Back.interfaces
{
    public interface ILlmService
    {
        Task<LlmResult> EnhanceTextAsync(string text, string language, string? topic);
    }

    public class LlmResult
    {
        public string EnhancedText { get; set; }
        public List<Correction> Corrections { get; set; }
    }

    public class Correction
    {
        public string Original { get; set; }
        public List<string> Alternatives { get; set; }
        public float? Confidence { get; set; }
        public int Page { get; set; }
        public int Line { get; set; }
        public int CharPosition { get; set; }
    }
}