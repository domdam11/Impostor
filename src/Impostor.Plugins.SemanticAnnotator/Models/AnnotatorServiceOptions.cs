using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    public class AnnotatorServiceOptions
    {
        public Thresholds Thresholds { get; set; }
        public int AnnotationIntervalMilliseconds { get; set; }
        public int ReplayMinWaitMilliseconds { get; set; }
        public List<string> ValidGameCodes { get; set; }
    }
}
