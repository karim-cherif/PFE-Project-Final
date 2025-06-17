namespace PFE_Back.enums
{
    public enum ProcessingStatus
    {
        Uploaded = 0,
        Processing = 1,
        OcrInProgress = 2,
        OcrComplete = 3,
        EnhancementInProgress = 4,
        Complete = 5,
        Completed = 5, // Alias for Complete
        Failed = 6
    }

    public enum SupportedLanguage
    {
        English = 0,
        French = 1,
        Arabic = 2
    }

    public enum DownloadType
    {
        Original = 0,
        Ocr = 1,
        Enhanced = 2,
        ExtractedText = 3  // Smart text extraction (Enhanced if available, otherwise OCR)
    }
}