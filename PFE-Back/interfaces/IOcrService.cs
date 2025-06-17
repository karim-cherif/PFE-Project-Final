using PFE_Back.models;

namespace PFE_Back.interfaces
{
    public interface IOcrService
    {
        Task<OcrResult> ProcessOcrAsync(string filePath, string language, CancellationToken cancellationToken = default);
    }

    public class OcrResult
    {
        public bool Success { get; set; } = true;  // Default to true since the OCR service doesn't return this
        public string ErrorMessage { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<TextCorrection> Corrections { get; set; } = new List<TextCorrection>();
        public float ProcessingTime { get; set; }
        public float Confidence { get; set; }
        public List<Suggestion> Suggestions { get; set; } = new List<Suggestion>();
        public string Language { get; set; } = "eng";
        public string TextDirection { get; set; } = "ltr";
        public int Pages { get; set; } = 1;
        public string Engine { get; set; } = "tesseract";
        public bool HasImages { get; set; } = false;
    }

    public class Suggestion
    {
        public string Word { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new List<string>();
        public int Start { get; set; }
        public int End { get; set; }
        public float Confidence { get; set; }
    }

    public class TextCorrection
    {
        public string Original { get; set; } = string.Empty;
        public List<string> Alternatives { get; set; } = new List<string>();
        public float? Confidence { get; set; }
        public int Page { get; set; }
        public int Line { get; set; }
        public int CharPosition { get; set; }
    }
}