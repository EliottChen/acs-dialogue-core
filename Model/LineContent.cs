namespace ACSDlg.Core
{
    /// <summary>A spoken or narrated line. The runner forwards it to the View via ShowLine.</summary>
    public class LineContent : IDialogueContent
    {
        /// <summary>Name of the character speaking. Null or empty = anonymous narration.</summary>
        public string? Speaker;

        /// <summary>Clean display text plus pre-parsed inline markup.</summary>
        public ParsedLine Line;

        /// <summary>Raw line text WITH its [...] markup, as authored. Identity used for translation lookup.</summary>
        public string SourceText;

        public LineContent(string? pSpeaker, ParsedLine pLine, string pSourceText)
        {
            Speaker = pSpeaker;
            Line = pLine;
            SourceText = pSourceText;
        }
    }
}
