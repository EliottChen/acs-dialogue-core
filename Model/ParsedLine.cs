using System.Collections.Generic;

namespace ACSDlg.Core
{
    /// <summary>
    /// Result of parsing the inline markup of one line. <see cref="CleanText"/> is what gets
    /// displayed, <see cref="Tags"/> carries the positioned directives (speed, pause, tp, emit).
    /// Parsed once at AST construction, never re-parsed at display time.
    /// Consumed by <see cref="TypewriterAnimator"/>.
    /// </summary>
    public class ParsedLine
    {
        /// <summary>Display text with all [...] markup removed.</summary>
        public string CleanText { get; }

        /// <summary>Markup tags ordered by position in <see cref="CleanText"/>.</summary>
        public IReadOnlyList<MarkupTag> Tags { get; }

        public ParsedLine(string pCleanText, IReadOnlyList<MarkupTag> pTags)
        {
            CleanText = pCleanText;
            Tags = pTags;
        }
    }
}
