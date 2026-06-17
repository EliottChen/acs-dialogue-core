using System.Collections.Generic;

namespace ACSDlg.Core
{
    /// <summary>
    /// One option of a choice. Exactly ONE of <see cref="TargetId"/> / <see cref="InlineContent"/>
    /// is set (invariant guaranteed by the parser through the two constructors):
    /// jump form 'choice "Label" jump Target ;' or block form 'choice "Label" { ... }'.
    /// </summary>
    public class ChoiceOption
    {
        /// <summary>Label shown to the player.</summary>
        public string Text;

        /// <summary>Jump form: id of the node to enter when this option is selected. Null for the block form.</summary>
        public string? TargetId;

        /// <summary>
        /// Block form: contents played when this option is selected. Nestable (may contain
        /// further ChoiceContent). When the block ends without jump/end, the runner falls
        /// through to the content following the parent menu (Ink-style). Null for the jump form.
        /// </summary>
        public List<IDialogueContent>? InlineContent;

        /// <summary>Jump form: choice "Label" jump Target ;</summary>
        public ChoiceOption(string pText, string pTargetId)
        {
            Text = pText;
            TargetId = pTargetId;
        }

        /// <summary>Block form: choice "Label" { ... }</summary>
        public ChoiceOption(string pText, List<IDialogueContent> pInlineContent)
        {
            Text = pText;
            InlineContent = pInlineContent;
        }
    }
}
