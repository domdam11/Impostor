using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    public class Thresholds
    {
        public double FOV { get; set; }
        public double NextToTask { get; set; }
        public double NextToVent { get; set; }
        public double TimeShort { get; set; }
        public double TimeInspectSample { get; set; }
        public double TimeUnlockManifolds { get; set; }
        public double TimeCalibratedDistributor { get; set; }
        public double TimeClearAsteroids { get; set; }
        public double TimeStartReactor { get; set; }
    }
}
