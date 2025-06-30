using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    public class SemanticPluginOptions
    {
        public int DelayBetweenQueuedTasksMs { get; set; } = 50;
        public int AnnotationIntervalMs { get; set; }
        public int TestId { get; set; }
        public List<string> ValidGameCodes { get; set; }
        public bool UseBuffer { get; set; } = true;
    }
}
