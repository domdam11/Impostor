using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    /// <summary>
    /// Represents summary data related to an OWL annotation.
    /// </summary>
    public class AnnotationData
    {
        /// <summary>
        /// The serialized OWL description.
        /// </summary>
        public string OwlDescription { get; set; }

        /// <summary>
        /// Number of OWL individuals.
        /// </summary>
        public int NumIndividuals { get; set; }

        /// <summary>
        /// Number of unique entities (classes, properties, etc.).
        /// </summary>
        public int NumEntities { get; set; }

        /// <summary>
        /// Size of the annotation in bytes.
        /// </summary>
        public int SizeInBytes { get; set; }

        public bool IsEmpty()
        {
            return NumIndividuals == 0;
        }
    }
}
