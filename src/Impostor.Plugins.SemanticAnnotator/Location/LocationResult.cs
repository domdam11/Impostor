using System.Numerics;

namespace Location
{
    public class LocationResult
    {
        public required string RoomName { get; set; }
        public Vector2 RelativePosition { get; set; } 
        public required float ConfidenceScore { get; set; }
        public required string SemanticLabel { get; set; }
    }
}
