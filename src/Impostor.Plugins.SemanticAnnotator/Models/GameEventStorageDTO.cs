using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    public class StrategicEventSummary
    {
        public int Id { get; set; }
        public string Timestamp { get; set; }
        public string Strategy { get; set; }
        public double Score { get; set; }
    }

    public class StrategicEventDetails
    {
        public string Id { get; set; }
        public ArgumentationResponse Metadata { get; set; }

        public string Annotation { get; set; }
    }

    public class GameSessionInfo
    {
        public string Id { get; set; }
        public string Timestamp { get; set; }
    }

}
