using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    public class PlayerStruct
    {
        public List<CustomMovement> Movements { get; set; } = new List<CustomMovement>(); // Lista dei movimenti
        public int VoteCount { get; set; } = 0; // Vote counter
        public string State { get; set; } = "none"; // player status
        public string SessionCls { get; set; } = "Player0"; // player status
    }
}
