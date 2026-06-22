using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace ACSDlg.Core
{
    /// <summary>
    /// Bridges a <see cref="DialogueGraph"/> and the translation industry's interchange format
    /// (XLIFF 1.2 — what CAT tools and agencies consume). Gettext-style keying: the raw source
    /// text IS the identity, carried in &lt;source&gt;; the &lt;target&gt; is what comes back translated.
    /// The trans-unit id is a stable content hash, an opaque handle for tools — never the lookup key.
    /// XML only (<see cref="System.Xml.Linq"/>), no third-party dependency.
    /// </summary>
    public static class Xliff
    {
        private static readonly XNamespace Ns = "urn:oasis:names:tc:xliff:document:1.2";

        /// <summary>
        /// Emit an XLIFF document for one dialogue. Empty targets by default (a template to send out);
        /// pass <paramref name="pTranslations"/> (source → target) to pre-fill known translations
        /// — the standard "re-export keeping what's already translated" workflow.
        /// </summary>
        public static string Export(DialogueGraph pGraph, string pSourceLang, string pTargetLang,
                                    string pOriginal = "", IReadOnlyDictionary<string, string>? pTranslations = null)
        {
            XElement lBody = new XElement(Ns + "body");

            foreach (string lSource in SourceStrings(pGraph))
            {
                string lTarget = "";
                if (pTranslations != null && pTranslations.TryGetValue(lSource, out string? lValue) && lValue != null)
                    lTarget = lValue;

                lBody.Add(new XElement(Ns + "trans-unit",
                    new XAttribute("id", Hash(lSource)),
                    new XElement(Ns + "source", lSource),
                    new XElement(Ns + "target", lTarget)));
            }

            XDocument lDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(Ns + "xliff",
                    new XAttribute("version", "1.2"),
                    new XElement(Ns + "file",
                        new XAttribute("original", pOriginal),
                        new XAttribute("source-language", pSourceLang),
                        new XAttribute("target-language", pTargetLang),
                        new XAttribute("datatype", "plaintext"),
                        lBody)));

            return lDoc.Declaration + Environment.NewLine + lDoc.ToString();
        }

        /// <summary>
        /// Read a translated XLIFF back into a runtime catalog (source text → translation). Units with
        /// an empty or missing target are skipped — they fall back to the source line at runtime.
        /// </summary>
        public static Dictionary<string, string> Import(string pXliff)
        {
            Dictionary<string, string> lCatalog = new Dictionary<string, string>();

            foreach (XElement lUnit in XDocument.Parse(pXliff).Descendants(Ns + "trans-unit"))
            {
                string? lSource = lUnit.Element(Ns + "source")?.Value;
                string? lTarget = lUnit.Element(Ns + "target")?.Value;

                if (!string.IsNullOrEmpty(lSource) && !string.IsNullOrEmpty(lTarget))
                    lCatalog[lSource!] = lTarget!;
            }

            return lCatalog;
        }

        // Every translatable string in document order, deduplicated (identical lines share one unit).
        private static IEnumerable<string> SourceStrings(DialogueGraph pGraph)
        {
            List<string> lOut = new List<string>();
            HashSet<string> lSeen = new HashSet<string>();
            foreach (DialogueNode lNode in pGraph.Nodes.Values)
                Collect(lNode.Contents, lOut, lSeen);
            return lOut;
        }

        private static void Collect(IReadOnlyList<IDialogueContent> pContents, List<string> pOut, HashSet<string> pSeen)
        {
            foreach (IDialogueContent lContent in pContents)
            {
                switch (lContent)
                {
                    case LineContent lLine:
                        if (pSeen.Add(lLine.SourceText)) pOut.Add(lLine.SourceText);
                        break;

                    case ChoiceContent lChoice:
                        foreach (ChoiceOption lOpt in lChoice.Options)
                        {
                            if (pSeen.Add(lOpt.Text)) pOut.Add(lOpt.Text);
                            if (lOpt.InlineContent != null) Collect(lOpt.InlineContent, pOut, pSeen);
                        }
                        break;
                }
            }
        }

        // FNV-1a 64-bit: deterministic across processes (unlike string.GetHashCode), good enough as
        // an opaque tool handle. The lookup key is the source text, so collisions can't corrupt a translation.
        private static string Hash(string pText)
        {
            ulong lHash = 14695981039346656037UL;
            foreach (char lChar in pText)
            {
                lHash ^= lChar;
                lHash *= 1099511628211UL;
            }
            return lHash.ToString("x16");
        }
    }
}
