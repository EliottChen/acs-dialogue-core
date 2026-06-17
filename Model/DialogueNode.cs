using System.Collections.Generic;

namespace ACSDlg.Core
{
    /// <summary>
    /// Named container of an ordered sequence of contents. The runner reads these contents
    /// in order. No logic here: it is a labelled list.
    /// </summary>
    public class DialogueNode
    {
        /// <summary>Unique identifier, referenced by jump instructions and choice targets.</summary>
        public string Id;

        /// <summary>Ordered sequence of instructions (lines, choices, jumps, end).</summary>
        public List<IDialogueContent> Contents = new List<IDialogueContent>();

        public DialogueNode(string pId)
        {
            Id = pId;
        }
    }
}
