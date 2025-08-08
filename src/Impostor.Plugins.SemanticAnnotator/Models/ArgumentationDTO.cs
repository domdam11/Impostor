using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    public class ArgumentationResponse
    {
        public Graph graph { get; set; }
        public List<SuggestedStrategy> suggestedStrategies { get; set; }
    }

    public class Graph
    {
        public List<Edge> edges { get; set; }
        public List<Node> nodes { get; set; }
    }

    public class Edge
    {
        public EdgeData data { get; set; }
    }

    public class EdgeData
    {
        public double label { get; set; }
        public int source { get; set; }
        public int target { get; set; }
    }

    public class Node
    {
        public NodeData data { get; set; }
    }

    public class NodeData
    {
        public int id { get; set; }
        public string label { get; set; }
    }

    public class SuggestedStrategy
    {
        public string name { get; set; }
        public double score { get; set; }
        public Explanation explanation { get; set; }
    }

    public class Explanation
    {
        public List<PathStrength> against { get; set; }
        public List<PathStrength> favor { get; set; }
    }

    public class PathStrength
    {
        public List<int> path { get; set; }
        public double strengthProduct { get; set; }
    }
}
