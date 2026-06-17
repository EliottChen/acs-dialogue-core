namespace ACSDlg.Core
{
    /// <summary>Kinds of inline markup the format supports. Closed set: an unknown tag is a parse error.</summary>
    public enum MarkupKind
    {
        Speed,
        Pause,
        Teleport,
        Emit,
    }

    /// <summary>
    /// One inline markup directive, positioned in the clean text of its line.
    /// Cursor model: the tag fires when the typewriter cursor reaches <see cref="Position"/>
    /// (before the character at that index) and never acts backwards.
    /// </summary>
    public struct MarkupTag
    {
        public MarkupKind Kind;

        /// <summary>Index in <see cref="ParsedLine.CleanText"/> (the tag fires before this character).</summary>
        public int Position;

        /// <summary>Value for Speed (multiplier) and Pause (seconds); 0 otherwise.</summary>
        public float NumericValue;

        /// <summary>Signal name for Emit (original case preserved); null otherwise.</summary>
        public string? StringValue;
    }
}
