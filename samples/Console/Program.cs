using System;
using ACSDlg.Core;
using ACSDlg.ConsoleApp;

namespace MyApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Console défaut = code page non-UTF-8 → les CJK sortent en '?'. Affichage uniquement :
            // les données sont déjà en UTF-8. (Les glyphes restent tributaires de la police du terminal.)
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* sortie redirigée */ }

            // --selftest              run the localizer assertions
            // --export <file> <lang>  write "<name>.<lang>.xlf" next to the dialogue (UTF-8, no '>')
            // --auto   <file> [lang]  headless play-through, optionally translated
            // (no args)               interactive menu (/play, /langage, /export, /quit)
            switch (args.Length > 0 ? args[0] : "")
            {
                case "--selftest":
                    LocaleSelfTest.Run();
                    return;

                case "--export" when args.Length >= 3:
                    string? lOut = Host.ExportXliff(args[1], args[2]);
                    if (lOut != null) Console.WriteLine($"XLIFF written: {lOut}");
                    return;

                case "--auto" when args.Length >= 2:
                    AutoPlay(args[1], args.Length >= 3 ? args[2] : Host.SourceLang);
                    return;

                default:
                    Menu.Run();
                    return;
            }
        }

        static void AutoPlay(string pPath, string pLang)
        {
            DialogueGraph? lGraph = Host.LoadGraph(pPath);
            if (lGraph == null) return;

            AutoPlayer.Play(lGraph, Host.BuildLocalizer(pPath, pLang));
        }
    }
}
