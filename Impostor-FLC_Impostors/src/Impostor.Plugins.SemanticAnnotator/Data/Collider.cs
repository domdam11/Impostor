using System.Collections.Generic;
using System.Numerics;

namespace Data
{
    public class Collider
    {
        public string? Name { get; set; }
        public List<Vector2> Points { get; set; } = new List<Vector2>();
        public Rect? Bounds { get; set; } 
    }
}
