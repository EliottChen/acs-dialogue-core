using ACSDlg.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ACSDlg.ConsoleApp
{
    /// <summary>
    /// Console implementation of <see cref="IRunnerController"/>. Owns the runner (private) and
    /// a <see cref="TypewriterAnimator"/> instance, pulses it with real elapsed time, applies the
    /// returned character counts to stdout, and relays animator emits through its own Emit().
    /// Only StartDialogue is public; in-dialogue commands (Advance, SelectChoice) are issued
    /// internally from the captured inputs.
    /// </summary>
    public class ConsoleDialoguePresenter : IRunnerController
    {
        private readonly DialogueRunner _runner;
        private readonly TypewriterAnimator _animator = new TypewriterAnimator();

        private ViewState _state = ViewState.Idle;
        private ParsedLine? _currentLine;
        private int _printedCount;
        private IReadOnlyList<string> _currentOptions = Array.Empty<string>();

        private enum ViewState
        {
            Idle,
            Animating,
            WaitAdvance,
            WaitChoice,
            Done,
        }

        public ConsoleDialoguePresenter(DialogueLocalizer? pLocalizer = null)
        {
            _runner = new DialogueRunner(this, pLocalizer);
        }

        /// <summary>Public entry point: plays the graph to completion on the calling thread.</summary>
        public void StartDialogue(DialogueGraph pGraph)
        {
            _runner.StartDialogue(pGraph);
            RunLoop();
        }

        // The console "frame loop": measures real elapsed time and dispatches on the view state.
        private void RunLoop()
        {
            Stopwatch lClock = Stopwatch.StartNew();

            while (_state != ViewState.Done)
            {
                switch (_state)
                {
                    case ViewState.Animating:
                        TickAnimation(lClock);
                        break;

                    case ViewState.WaitAdvance:
                        WaitAdvanceInput();
                        lClock.Restart(); // input blocked the thread; don't count it as animation time
                        break;

                    case ViewState.WaitChoice:
                        WaitChoiceInput();
                        lClock.Restart();
                        break;

                    case ViewState.Idle:
                        break;
                }
            }
        }

        // ======= ANIMATION =======

        private void TickAnimation(Stopwatch pClock)
        {
            float lDelta = (float)pClock.Elapsed.TotalSeconds;
            pClock.Restart();

            ApplyTick(_animator.Update(lDelta));

            // Skip input: Enter during animation reveals everything.
            if (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                ConsoleKeyInfo lKey = Console.ReadKey(intercept: true);
                if (lKey.Key == ConsoleKey.Enter)
                {
                    _animator.Skip();
                    // Zero-delta tick: prints the freshly revealed characters and collects
                    // the emits deferred by Skip() before the done-check below runs.
                    ApplyTick(_animator.Update(0f));
                }
            }

            if (_animator.GetAnimationDone())
            {
                Console.WriteLine();
                FlushPendingKeys();
                _state = ViewState.WaitAdvance;
                return;
            }

            Thread.Sleep(5);
        }

        private void ApplyTick(TypewriterTick pTick)
        {
            if (_currentLine != null && pTick.VisibleCharacterCount > _printedCount)
            {
                Console.Write(_currentLine.CleanText.Substring(_printedCount, pTick.VisibleCharacterCount - _printedCount));
                _printedCount = pTick.VisibleCharacterCount;
            }

            int lEmitCount = pTick.Emits.Count;
            for (int i = 0; i < lEmitCount; i++)
                Emit(pTick.Emits[i]);
        }

        // ======= INPUTS =======

        private void WaitAdvanceInput()
        {
            // Redirected stdin (piped test runs) has no key events: consume one line instead.
            if (Console.IsInputRedirected)
            {
                Console.In.ReadLine();
                _runner.Advance();
                return;
            }

            ConsoleKeyInfo lKey = Console.ReadKey(intercept: true);
            if (lKey.Key == ConsoleKey.Enter)
                _runner.Advance();
        }

        private void WaitChoiceInput()
        {
            string? lAnswer = Console.ReadLine();

            // ReadLine() returns null on EOF (e.g. piped input exhausted). Fall back to the first choice.
            if (lAnswer == null)
            {
                EchoChoice(0);
                _runner.SelectChoice(0);
                return;
            }

            string lDigitsOnly = new string(lAnswer.Where(char.IsDigit).ToArray());
            if (int.TryParse(lDigitsOnly, out int lIndex) && lIndex >= 0 && lIndex < _currentOptions.Count)
            {
                EchoChoice(lIndex);
                _runner.SelectChoice(lIndex);
            }
        }

        private void EchoChoice(int pIndex)
        {
            if (pIndex < _currentOptions.Count)
                Console.WriteLine($"> {_currentOptions[pIndex]}");
        }

        private static void FlushPendingKeys()
        {
            if (Console.IsInputRedirected)
                return;
            while (Console.KeyAvailable)
                Console.ReadKey(intercept: true);
        }

        // ======= IRunnerController (called by the runner) =======

        public void DialogueStart()
        {
            _state = ViewState.Idle;
            
        }

        public void ShowLine(string? pSpeaker, ParsedLine pLine)
        {
            Console.Clear();
            if (!string.IsNullOrWhiteSpace(pSpeaker))
                Console.Write($"{pSpeaker.Trim()} : ");

            _currentLine = pLine;
            _printedCount = 0;
            _animator.Start(pLine);
            _state = ViewState.Animating;
        }

        public void ShowChoices(IReadOnlyList<string> pOptions)
        {
            _currentOptions = pOptions;
            for (int i = 0; i < pOptions.Count; i++)
                Console.WriteLine($"   {i} - {pOptions[i]}");

            _state = ViewState.WaitChoice;
        }

        // Console stand-in for engine-side event mapping: just makes the signal visible.
        public void Emit(string pSignal)
        {
            ConsoleColor lPrevious = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"⟦event:{pSignal}⟧");
            Console.ForegroundColor = lPrevious;
        }

        public void DialogueEnd()
        {
            _state = ViewState.Done;
        }
    }
}
