using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ACSDlg.Core
{
    /// <summary>
    /// Transforms the raw text of one line (with its [...] tags) into a <see cref="ParsedLine"/>.
    /// Linear cursor pass: extracts each tag, records it with its position, removes it from
    /// the display text. Resolves aliases (p/pause, s/speed), tag-name case (an emit VALUE
    /// keeps its case), and the optional decimal part. No stack — state model, not scopes.
    /// Unknown tag names are a parse error (the markup set is closed in v0).
    /// </summary>
    public static class MarkupParser
    {
        public static ParsedLine Parse(string pRawText)
        {
            StringBuilder lClean = new StringBuilder();
            List<MarkupTag> lTags = new List<MarkupTag>();

            for (int i = 0; i < pRawText.Length; i++)
            {
                if (pRawText[i] != '[')
                {
                    lClean.Append(pRawText[i]);
                    continue;
                }

                int lClose = pRawText.IndexOf(']', i);
                if (lClose < 0)
                    throw new FormatException($"Unclosed '[' in line text: \"{pRawText}\"");

                string lInner = pRawText.Substring(i + 1, lClose - i - 1);
                lTags.Add(ParseTag(lInner, lClean.Length));
                i = lClose;
            }

            return new ParsedLine(lClean.ToString(), lTags);
        }

        // '[name]' or '[name:value]' → MarkupTag. Name lowercased + alias-resolved; value trimmed.
        private static MarkupTag ParseTag(string pInner, int pPosition)
        {
            string lName;
            string? lValue = null;

            int lColon = pInner.IndexOf(':');
            if (lColon >= 0)
            {
                lName = pInner.Substring(0, lColon).Trim().ToLowerInvariant();
                lValue = pInner.Substring(lColon + 1).Trim();
            }
            else
            {
                lName = pInner.Trim().ToLowerInvariant();
            }

            switch (lName)
            {
                case "speed":
                case "s":
                    return new MarkupTag { Kind = MarkupKind.Speed, Position = pPosition, NumericValue = RequireFloat(lValue, "speed") };

                case "pause":
                case "p":
                    return new MarkupTag { Kind = MarkupKind.Pause, Position = pPosition, NumericValue = RequireFloat(lValue, "pause") };

                case "tp":
                    if (lValue != null)
                        throw new FormatException("Tag [tp] takes no value");
                    return new MarkupTag { Kind = MarkupKind.Teleport, Position = pPosition };

                case "emit":
                    if (string.IsNullOrEmpty(lValue))
                        throw new FormatException("Tag [emit] requires a signal name");
                    return new MarkupTag { Kind = MarkupKind.Emit, Position = pPosition, StringValue = lValue };

                default:
                    throw new FormatException($"Unknown markup tag '[{pInner}]'");
            }
        }

        private static float RequireFloat(string? pValue, string pTagName)
        {
            if (pValue == null)
                throw new FormatException($"Tag [{pTagName}] requires a numeric value");
            if (!float.TryParse(pValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float lResult))
                throw new FormatException($"Tag [{pTagName}] has invalid numeric value '{pValue}'");
            return lResult;
        }
    }
}
