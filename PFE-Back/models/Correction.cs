namespace PFE_Back.models
{
    public class Correction
    {
        public string OriginalText { get; set; }
        public string CorrectedText { get; set; }
        public float Confidence { get; set; }
        public int PageNumber { get; set; }
        public int LineNumber { get; set; }
        public int CharPosition { get; set; }
    }
} 