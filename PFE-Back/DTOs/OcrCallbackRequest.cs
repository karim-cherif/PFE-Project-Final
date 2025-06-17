using System;
using System.Collections.Generic;

namespace PFE_Back.DTOs
{
    public class OcrCallbackRequest
    {
        public Guid DocumentId { get; set; }
        public string Text { get; set; }
        public List<Suggestion> Suggestions { get; set; }
        public List<Correction> Corrections { get; set; }
        public float Confidence { get; set; }
        public float ProcessingTime { get; set; }
    }

    public class Suggestion
    {
        public string Word { get; set; }
        public List<SuggestionItem> Suggestions { get; set; }
        public float Confidence { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
    }

    public class SuggestionItem
    {
        public string Text { get; set; }
        public float Confidence { get; set; }
    }

    public class Correction
    {
        public string Original { get; set; }
        public string Corrected { get; set; }
        public float Confidence { get; set; }
    }
}