using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    /// <summary>
    /// Represents the state of a player within a game session.
    /// </summary>
    public class PlayerState
    {
        public string id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public bool IsDead { get; set; }
        public string State { get; set; } // E.g. "alive", "dead", "trusted", "suspected", "left"
        public List<CustomMovement> Movements { get; set; } // List of recorded movements
        public int VoteCount { get; set; }

        /// <summary>
        /// Constructor to initialize a player's state.
        /// </summary>
        /// <param name="name">Player's name.</param>
        /// <param name="role">Player's assigned role (e.g., "Crewmate" or "Impostor").</param>
        public PlayerState(string name, string role)
        {
            id = Guid.NewGuid().ToString();// Generates a unique ID for the player
            Name = name;
            Role = role;
            IsDead = false;
            State = "alive";// Default state when a player joins
            Movements = new List<CustomMovement>();
            VoteCount = 0;
        }
    }
}
