using System;
using System.Collections.Generic;
using ACSDlg.Core;

namespace ACSDlg.ConsoleApp
{
    /// <summary>Constant locale, the simplest <see cref="ILocaleProvider"/> — what a real host
    /// replaces with "read the current language from the engine".</summary>
    internal sealed class FixedLocale : ILocaleProvider
    {
        private readonly string _code;
        public FixedLocale(string pCode) => _code = pCode;
        public string GetLocale() => _code;
    }

    /// <summary>
    /// Headless driver: auto-advances every line and always takes the first choice, printing the
    /// resolved text. No typewriter, no input — used to demo a full play-through in a given locale.
    /// ponytail: always picks option 0; fine for a linear demo, not a branching-coverage tool.
    /// </summary>
    internal sealed class AutoPlayer : IRunnerController
    {
        private enum Pending { None, Advance, Choose, Done }
        private Pending _pending = Pending.None;

        public static void Play(DialogueGraph pGraph, DialogueLocalizer? pLocalizer)
        {
            AutoPlayer lPlayer = new AutoPlayer();
            DialogueRunner lRunner = new DialogueRunner(lPlayer, pLocalizer);

            lRunner.StartDialogue(pGraph);
            while (lPlayer._pending != Pending.Done)
            {
                switch (lPlayer._pending)
                {
                    case Pending.Advance: lPlayer._pending = Pending.None; lRunner.Advance(); break;
                    case Pending.Choose:  lPlayer._pending = Pending.None; lRunner.SelectChoice(0); break;
                    default:              lPlayer._pending = Pending.Done; break; // nothing pending: stop
                }
            }
        }

        public void DialogueStart() { }

        public void ShowLine(string? pSpeaker, ParsedLine pLine)
        {
            Console.WriteLine(string.IsNullOrWhiteSpace(pSpeaker) ? pLine.CleanText : $"{pSpeaker}: {pLine.CleanText}");
            _pending = Pending.Advance;
        }

        public void ShowChoices(IReadOnlyList<string> pOptions)
        {
            for (int i = 0; i < pOptions.Count; i++)
                Console.WriteLine($"   [{i}] {pOptions[i]}");
            Console.WriteLine("   -> auto: 0");
            _pending = Pending.Choose;
        }

        public void Emit(string pSignal) { }   // headless: events aren't exercised in this demo

        public void DialogueEnd() { _pending = Pending.Done; }
    }
}
