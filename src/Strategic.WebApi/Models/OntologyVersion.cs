namespace Strategic.WebApi.Models
{
    public sealed class OntologyVersion
    {
        public string Id { get; set; } = default!;          // GUID
        public int Version { get; set; }                    // progressivo
        public string CreatedAt { get; set; } = default!;   // ISO timestamp
        public string OwlContent { get; set; } = default!;  // OWL originale
    }
}
