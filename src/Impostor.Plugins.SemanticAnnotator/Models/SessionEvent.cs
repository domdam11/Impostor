using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    public class SessionEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public string PlayerId { get; set; }
        public string Details { get; set; }
    }
}
