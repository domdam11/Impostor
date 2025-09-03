using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Strategic.WebApi.Models
{
    public sealed class Evaluation
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;                // $"{UserId}-{EventId}"

        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = default!;

        [JsonPropertyName("eventId")]
        public string EventId { get; set; } = default!;

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = default!;

        [JsonPropertyName("reaction")]
        public string? Reaction { get; set; }                     // "like" | "dislike" | null

        [JsonPropertyName("value")]
        public int Value { get; set; }                            

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
    }
}
