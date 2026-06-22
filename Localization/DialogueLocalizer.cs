using System;
using System.Collections.Generic;

namespace ACSDlg.Core
{
    /// <summary>
    /// Resolves a line or a choice label into the active locale, gettext-style: the raw source
    /// text IS the lookup key. Catalogs map source text → translated text, one per locale.
    /// The active locale is read on every lookup from the injected <see cref="ILocaleProvider"/>,
    /// so a mid-game language switch takes effect on the next line with no extra wiring.
    ///
    /// Lines carry inline markup whose tag positions are tied to the text length, so a translation
    /// must be re-parsed (<see cref="MarkupParser"/>) to recompute positions for its own wording.
    /// Choice labels carry no markup and are returned verbatim.
    ///
    /// Source locale, missing catalog, or missing key all fall back to the already-parsed source
    /// line — the dialogue keeps playing untranslated rather than failing.
    /// </summary>
    public class DialogueLocalizer
    {
        private readonly ILocaleProvider _locale;

        // locale code -> (raw source text -> translated text)
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _catalogs
            = new Dictionary<string, IReadOnlyDictionary<string, string>>();

        public DialogueLocalizer(ILocaleProvider pLocale)
        {
            _locale = pLocale ?? throw new ArgumentNullException(nameof(pLocale));
        }

        /// <summary>
        /// Register (or replace) the translation table for one locale. The host loads it however
        /// it wants (JSON, Unity tables, …) and hands the parsed pairs to the core.
        /// </summary>
        public void SetCatalog(string pLocale, IReadOnlyDictionary<string, string> pTable)
        {
            if (pLocale == null) throw new ArgumentNullException(nameof(pLocale));
            _catalogs[pLocale] = pTable ?? throw new ArgumentNullException(nameof(pTable));
        }

        /// <summary>Drop every loaded catalog. Call before loading a self-contained set (e.g. one
        /// dialogue's) so a previous dialogue's catalog can't leak a translation into this one.</summary>
        public void ClearCatalogs() => _catalogs.Clear();

        /// <summary>Translated, re-parsed line for the active locale, or the source line on any miss.</summary>
        public ParsedLine ResolveLine(LineContent pLine)
        {
            if (!TryTranslate(pLine.SourceText, out string lTranslated))
                return pLine.Line;

            try
            {
                return MarkupParser.Parse(lTranslated);
            }
            catch (FormatException)
            {
                // ponytail: a translator broke a [tag] → degrade to the source line instead of
                // crashing the player. Upgrade path: validate markup at XLIFF import time.
                return pLine.Line;
            }
        }

        /// <summary>Translated choice label for the active locale, or the source label on any miss.</summary>
        public string ResolveLabel(string pLabel)
        {
            return TryTranslate(pLabel, out string lTranslated) ? lTranslated : pLabel;
        }

        private bool TryTranslate(string pSource, out string pTranslated)
        {
            if (_catalogs.TryGetValue(_locale.GetLocale(), out IReadOnlyDictionary<string, string>? lTable)
                && lTable.TryGetValue(pSource, out string? lValue)
                && lValue != null)
            {
                pTranslated = lValue;
                return true;
            }

            pTranslated = pSource;
            return false;
        }
    }
}
