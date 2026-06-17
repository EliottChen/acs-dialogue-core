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
            // Drag a .acsdlg onto the .exe → its path arrives in args. Double-clicked → ask for one.
            string lPath = args.Length > 0 ? args[0] : Prompt();

            if (!File.Exists(lPath))
            {
                Console.WriteLine($"Dialogue file not found: {lPath}");
                Pause();
                return;
            }

            string lSource = File.ReadAllText(lPath);

            DialogueGraph lGraph;
            try
            {
                lGraph = new Parser(new Lexer(lSource).Tokenize()).Parse();
            }
            catch (FormatException lEx)
            {
                Console.WriteLine($"Parse error in '{lPath}': {lEx.Message}");
                Pause();
                return;
            }

            new ConsoleDialoguePresenter().StartDialogue(lGraph);
            Pause();
        }

        // Windows wraps a path dropped into the console in quotes — strip them.
        static string Prompt()
        {
            Console.WriteLine("Glissez-déposez un fichier .acsdlg dans cette fenêtre, puis Entrée :");
            return (Console.ReadLine() ?? "").Trim().Trim('"');
        }

        // Keep the window open so double-click users can read the output before it closes.
        static void Pause()
        {
            Console.WriteLine("\n— Entrée pour fermer —");
            Console.ReadLine();
        }
    }
}
