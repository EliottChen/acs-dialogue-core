namespace ACSDlg.Core
{
    /// <summary>Unconditional jump to another node. Pure flow — never touches the View.</summary>
    public class JumpContent : IDialogueContent
    {
        public string TargetId;

        public JumpContent(string pTargetId)
        {
            TargetId = pTargetId;
        }
    }
}
