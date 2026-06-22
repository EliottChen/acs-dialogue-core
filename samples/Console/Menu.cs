using System;
using ACSDlg.Core;

namespace ACSDlg.ConsoleApp
{
    /// <summary>
    /// Interactive startup menu. /langage picks the reading language (type a code), /play prompts
    /// for a .acsdlg and reads it through the real typewriter presenter in that language, /quit exits.
    /// The chosen language is held between plays.
    /// </summary>
    internal static class Menu
    {
        private static string _lang = Host.SourceLang;

        public static void Run()
        {
            Console.WriteLine("ACSDlg console — commandes : /play  /langage  /export  /quit");

            while (true)
            {
                Console.Write($"[{_lang}] > ");
                string lInput = (Console.ReadLine() ?? "/quit").Trim();

                switch (lInput)
                {
                    case "/play": Play(); break;
                    case "/langage": ChooseLanguage(); break;
                    case "/export": Export(); break;
                    case "/quit": case "": return;
                    default: Console.WriteLine("Commandes : /play  /langage  /export  /quit"); break;
                }
            }
        }

        private static void ChooseLanguage()
        {
            Console.WriteLine($"Actuelle : {_lang}   (source : {Host.SourceLang}). Tape le code d'une langue exportée (le suffixe du .xlf).");
            Console.Write("Code langue : ");
            string lCode = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();

            if (lCode.Length == 0)
            {
                Console.WriteLine("Code vide, inchangé.");
                return;
            }

            _lang = lCode;
            Console.WriteLine($"Langue réglée sur : {_lang}  (les lignes sans traduction retomberont sur {Host.SourceLang}).");
        }

        private static void Export()
        {
            Console.Write("Glissez-déposez un fichier .acsdlg, puis Entrée : ");
            string lPath = (Console.ReadLine() ?? "").Trim().Trim('"');

            Console.Write("Langue cible (ex. de) : ");
            string lLang = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            if (lLang.Length == 0 || lLang == Host.SourceLang)
            {
                Console.WriteLine($"Code invalide (la source est '{Host.SourceLang}', rien à exporter).");
                return;
            }

            string? lOut = Host.ExportXliff(lPath, lLang);
            if (lOut != null)
                Console.WriteLine($"XLIFF écrit : {lOut}\nRemplis les <target>, puis : /langage → {lLang}, /play.");
        }

        private static void Play()
        {
            Console.Write("Glissez-déposez un fichier .acsdlg, puis Entrée : ");
            string lPath = (Console.ReadLine() ?? "").Trim().Trim('"');

            DialogueGraph? lGraph = Host.LoadGraph(lPath);
            if (lGraph == null)
                return;

            DialogueLocalizer? lLocalizer = Host.BuildLocalizer(lPath, _lang);
            new ConsoleDialoguePresenter(lLocalizer).StartDialogue(lGraph);
        }
    }
}
