using System.Text.Json.Serialization;
using PFE_Back.interfaces;

namespace PFE_Back.models
{
    public class OcrResponse
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("suggestions")]
        public List<PFE_Back.interfaces.Suggestion> Suggestions { get; set; }

        [JsonPropertyName("corrections")]
        public List<object> Corrections { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("processingTime")]
        public float ProcessingTime { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("metadata")]
        public Metadata Metadata { get; set; }
    }

    public class Metadata
    {
        [JsonPropertyName("engine")]
        public string Engine { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
} 