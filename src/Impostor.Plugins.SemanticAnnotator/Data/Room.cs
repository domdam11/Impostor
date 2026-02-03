#nullable enable
using System.Collections.Generic;

namespace Data
{
    public class Room
    {
        public string? Name { get; set; }
        public List<Collider> Colliders { get; set; } = new List<Collider>();
    }
}
