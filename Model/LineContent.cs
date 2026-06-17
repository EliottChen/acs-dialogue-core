namespace ACSDlg.Core
{
    /// <summary>A spoken or narrated line. The runner forwards it to the View via ShowLine.</summary>
    public class LineContent : IDialogueContent
    {
        /// <summary>Name of the character speaking. Null or empty = anonymous narration.</summary>
        public string? Speaker;

        /// <summary>Clean display text plus pre-parsed inline markup.</summary>
        public ParsedLine Line;

        public LineContent(string? pSpeaker, ParsedLine pLine)
        {
            Speaker = pSpeaker;
            Line = pLine;
        }
    }
}
