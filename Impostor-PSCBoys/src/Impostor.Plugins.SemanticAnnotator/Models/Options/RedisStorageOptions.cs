using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models.Options
{
    public class RedisStorageOptions
    {
        public bool Enabled { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
    }
}
