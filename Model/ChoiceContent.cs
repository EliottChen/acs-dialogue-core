using System.Collections.Generic;

namespace ACSDlg.Core
{
    /// <summary>
    /// A player-facing branching point. Consecutive 'choice' statements in the source
    /// are grouped by the parser into a single ChoiceContent (one menu).
    /// </summary>
    public class ChoiceContent : IDialogueContent
    {
        public List<ChoiceOption> Options = new List<ChoiceOption>();
    }
}
