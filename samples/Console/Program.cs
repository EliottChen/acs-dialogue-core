using System;
using System.IO;
using ACSDlg.Core;
using ACSDlg.ConsoleApp;

namespace MyApp
{
    internal class Program
    {
        // File I/O lives here, in the host — the core only ever sees the source string.
        static void Main(string[] args)
        {
            string lPath = args.Length > 0 ? args[0] : "TestDialogue.acsdlg";

            if (!File.Exists(lPath))
            {
                Console.WriteLine($"Dialogue file not found: {lPath}");
                Environment.ExitCode = 1;
                return;
            }

            string lSource = File.ReadAllText(lPath);

            DialogueGraph lGraph;
            try
            {
                var lTokens = new Lexer(lSource).Tokenize();
                lGraph = new Parser(lTokens).Parse();
            }
            catch (FormatException lEx)
            {
                Console.WriteLine($"Parse error in '{lPath}': {lEx.Message}");
                Environment.ExitCode = 1;
                return;
            }

            new ConsoleDialoguePresenter().StartDialogue(lGraph);
        }
    }
}
