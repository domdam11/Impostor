namespace Strategic.WebApi.Models
{
    public sealed class Correction
    {
        public string Id { get; set; } = default!;
        public string SessionId { get; set; } = default!;
        public string EventId { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public string CorrectStrategy { get; set; } = default!;
        public string Timestamp { get; set; } = default!;
    }
}
