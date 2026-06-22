using System;
using System.Collections.Generic;
using ACSDlg.Core;

namespace ACSDlg.ConsoleApp
{
    /// <summary>
    /// Assert-based self-check for <see cref="DialogueLocalizer"/>. No framework: run it with
    /// `dotnet run --project samples/Console -- --selftest`. Throws on the first broken invariant.
    /// </summary>
    internal static class LocaleSelfTest
    {
        // Mutable stub: lets the test flip the active locale mid-run.
        private sealed class StubLocale : ILocaleProvider
        {
            public string Code = "fr";
            public string GetLocale() => Code;
        }

        public static void Run()
        {
            const string lSource = "Bonjour[pause:1] tout le monde";
            LineContent lLine = new LineContent("Eric", MarkupParser.Parse(lSource), lSource);

            StubLocale lStub = new StubLocale();
            DialogueLocalizer lLoc = new DialogueLocalizer(lStub);
            lLoc.SetCatalog("de", new Dictionary<string, string>
            {
                [lSource] = "Hallo[pause:1] zusammen",
                ["Oui"] = "Ja",
                ["cassé"] = "kaputt[pause",   // markup volontairement cassé
            });

            // Langue source (pas de catalogue 'fr') → ligne source, positions intactes.
            Check(lLoc.ResolveLine(lLine).CleanText == "Bonjour tout le monde", "source fallback");

            // Bascule en 'de' → texte traduit ET markup re-parsé (le [pause] survit).
            lStub.Code = "de";
            ParsedLine lDe = lLoc.ResolveLine(lLine);
            Check(lDe.CleanText == "Hallo zusammen", "translated text");
            Check(lDe.Tags.Count == 1 && lDe.Tags[0].Kind == MarkupKind.Pause, "markup reparsed");

            // Label de choix : traduit si présent, verbatim sinon.
            Check(lLoc.ResolveLabel("Oui") == "Ja", "label hit");
            Check(lLoc.ResolveLabel("Non") == "Non", "label miss");

            // Traduction au markup cassé → on retombe sur la ligne source, pas de crash.
            LineContent lBroken = new LineContent(null, MarkupParser.Parse("cassé"), "cassé");
            Check(lLoc.ResolveLine(lBroken).CleanText == "cassé", "broken-markup fallback");

            // Clé absente du catalogue 'de' → ligne source.
            LineContent lUnknown = new LineContent(null, MarkupParser.Parse("Inédit"), "Inédit");
            Check(lLoc.ResolveLine(lUnknown).CleanText == "Inédit", "missing-key fallback");

            Console.WriteLine("LocaleSelfTest: OK");
        }

        private static void Check(bool pCondition, string pName)
        {
            if (!pCondition)
                throw new Exception($"LocaleSelfTest FAILED: {pName}");
        }
    }
}
