using System.Collections.Generic;

namespace ACSDlg.Core
{
    /// <summary>
    /// Root container of a parsed dialogue. Gives access to nodes by id and knows the entry point.
    /// Produced by the <see cref="Parser"/>, consumed by the <see cref="DialogueRunner"/>.
    /// Immutable after parsing — the parser validates that <see cref="StartNodeId"/> and every
    /// jump/choice target exist, so <see cref="GetNode"/> cannot fail at runtime on a valid graph.
    /// </summary>
    public class DialogueGraph
    {
        /// <summary>Id of the entry node, declared by the '#start' directive.</summary>
        public string StartNodeId { get; }

        /// <summary>All nodes of the dialogue, indexed by their <see cref="DialogueNode.Id"/>.</summary>
        public IReadOnlyDictionary<string, DialogueNode> Nodes { get; }

        public DialogueGraph(string pStartNodeId, IReadOnlyDictionary<string, DialogueNode> pNodes)
        {
            StartNodeId = pStartNodeId;
            Nodes = pNodes;
        }

        public DialogueNode GetNode(string pId)
        {
            if (Nodes.TryGetValue(pId, out DialogueNode? lNode))
                return lNode;

            // Unreachable on a parser-validated graph; kept as a guard for hand-built graphs.
            throw new KeyNotFoundException($"Dialogue node '{pId}' does not exist in this graph.");
        }
    }
}
