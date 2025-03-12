using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    /// <summary>
    /// Represents a custom movement of a player.
    /// </summary>
    public class CustomMovement
    {
        public System.Numerics.Vector2 Position { get; set; } // The position of the player
        public DateTimeOffset Timestamp { get; set; } // The timestamp of the movement

        /// <summary>
        /// Constructor to create a new movement entry.
        /// </summary>
        public CustomMovement(System.Numerics.Vector2 position, DateTimeOffset timestamp)
        {
            Position = position;
            Timestamp = timestamp;
        }
    }
}
