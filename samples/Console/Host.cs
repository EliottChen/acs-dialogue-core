using System;
using System.Collections.Generic;
using System.IO;
using ACSDlg.Core;

namespace ACSDlg.ConsoleApp
{
    /// <summary>
    /// Host-side glue: file I/O the core never does. Reads a .acsdlg into a graph, and builds a
    /// localizer from a sibling "&lt;name&gt;.&lt;lang&gt;.xlf" catalog. "fr" is the source language
    /// (the .acsdlg itself) → no catalog, localizer null.
    /// </summary>
    internal static class Host
    {
        public const string SourceLang = "fr";

        // Read + parse a .acsdlg, or print why it failed and return null.
        public static DialogueGraph? LoadGraph(string pPath)
        {
            if (!File.Exists(pPath))
            {
                Console.WriteLine($"Dialogue file not found: {pPath}");
                return null;
            }

            try
            {
                return new Parser(new Lexer(File.ReadAllText(pPath)).Tokenize()).Parse();
            }
            catch (FormatException lEx)
            {
                Console.WriteLine($"Parse error in '{pPath}': {lEx.Message}");
                return null;
            }
        }

        // Write an XLIFF next to the dialogue ("<name>.<lang>.xlf"), in UTF-8. If a file is already
        // there, its filled targets are preserved (re-export). Returns the written path, or null on
        // a load error. Writing from C# avoids the PowerShell '>' UTF-16 trap.
        public static string? ExportXliff(string pDialoguePath, string pLang)
        {
            DialogueGraph? lGraph = LoadGraph(pDialoguePath);
            if (lGraph == null)
                return null;

            string lDir = Path.GetDirectoryName(pDialoguePath) ?? ".";
            string lName = Path.GetFileNameWithoutExtension(pDialoguePath);
            string lXlfPath = Path.Combine(lDir, $"{lName}.{pLang}.xlf");

            IReadOnlyDictionary<string, string>? lExisting = File.Exists(lXlfPath)
                ? Xliff.Import(File.ReadAllText(lXlfPath))
                : null;

            File.WriteAllText(lXlfPath, Xliff.Export(lGraph, SourceLang, pLang, Path.GetFileName(pDialoguePath), lExisting));
            return lXlfPath;
        }

        // Localizer for the chosen language, or null to play in the source language. Missing catalog
        // file → warn and fall back to source rather than failing.
        public static DialogueLocalizer? BuildLocalizer(string pDialoguePath, string pLang)
        {
            if (pLang == SourceLang)
                return null;

            string lDir = Path.GetDirectoryName(pDialoguePath) ?? ".";
            string lName = Path.GetFileNameWithoutExtension(pDialoguePath);
            string lXlfPath = Path.Combine(lDir, $"{lName}.{pLang}.xlf");

            if (!File.Exists(lXlfPath))
            {
                Console.WriteLine($"No '{pLang}' translation for this dialogue ({lXlfPath}) → playing source.");
                return null;
            }

            DialogueLocalizer lLocalizer = new DialogueLocalizer(new FixedLocale(pLang));
            lLocalizer.SetCatalog(pLang, Xliff.Import(File.ReadAllText(lXlfPath)));
            return lLocalizer;
        }
    }
}
