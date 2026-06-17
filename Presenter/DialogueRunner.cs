using System;
using System.Collections.Generic;

namespace ACSDlg.Core
{
    /// <summary>
    /// The brain. Reads the Model, holds the read head (presentation state), drives the View
    /// through <see cref="IRunnerController"/>. Pure-flow contents (jump, later set/if) never
    /// touch the View. Knows nothing about the typewriter: it sends a line, animating it is
    /// the View's business. Instanciable — one runner per concurrent conversation.
    ///
    /// The read head is a STACK of frames: one frame per content list being read (the node
    /// body, plus one per nested inline choice block). Entering a block pushes; reaching the
    /// end of a list pops and falls through to the parent (Ink-style); a jump clears the
    /// whole stack; an end terminates regardless of depth.
    /// </summary>
    public class DialogueRunner
    {
        public enum State
        {
            NotStarted,
            Playing,
            WaitingForChoice,
            Done,
        }

        public State CurrentState { get; private set; } = State.NotStarted;

        private readonly IRunnerController _controller;
        private readonly Stack<ReadFrame> _frames = new Stack<ReadFrame>();
        private DialogueGraph? _graph;
        private ChoiceContent? _pendingChoice;
        // Id of the node owning the current stack — kept for error messages only.
        private string _currentNodeId = "";

        public DialogueRunner(IRunnerController pController)
        {
            _controller = pController ?? throw new ArgumentNullException(nameof(pController));
        }

        // --- Commands called BY the View ---

        /// <summary>
        /// Start a dialogue, using a provided <see cref="DialogueGraph"/> that you need to construct.
        /// </summary>
        public void StartDialogue(DialogueGraph pGraph)
        {
            if (CurrentState == State.Playing || CurrentState == State.WaitingForChoice)
                return;

            _graph = pGraph;
            CurrentState = State.Playing;
            _controller.DialogueStart();
            EnterNode(pGraph.StartNodeId);
            ReadCurrentContent();
        }

        /// <summary>
        /// The current line has been consumed — move the read head to the next content.
        /// </summary>
        public void Advance()
        {
            if (CurrentState != State.Playing)
                return;

            _frames.Peek().Index++;
            ReadCurrentContent();
        }

        /// <summary>
        /// Selecting a choice using index, you need to get the choice from the <see cref="IRunnerController.ShowChoices(IReadOnlyList{string})"/>
        /// methods
        /// </summary>
        public void SelectChoice(int pIndex)
        {
            if (CurrentState != State.WaitingForChoice)
                return;
            if (_pendingChoice == null || pIndex < 0 || pIndex >= _pendingChoice.Options.Count)
                return;

            ChoiceOption lOption = _pendingChoice.Options[pIndex];
            _pendingChoice = null;
            CurrentState = State.Playing;

            if (lOption.InlineContent != null)
            {
                // Step past the choice menu in the current frame BEFORE pushing, so the
                // fall-through resumes on the content that follows the menu (not the menu again).
                _frames.Peek().Index++;
                _frames.Push(new ReadFrame(lOption.InlineContent));
            }
            else
            {
                // Jump form: the parser guarantees TargetId is set when InlineContent is null.
                EnterNode(lOption.TargetId!);
            }

            ReadCurrentContent();
        }

        /// <summary>Forced interruption (player walks away, cutscene cancelled…). Notifies the View and releases state.</summary>
        public void Stop()
        {
            if (CurrentState == State.Done || CurrentState == State.NotStarted)
                return;

            End();
        }

        // --- Read head ---

        // Replaces the whole stack with a fresh frame on the target node ("a jump cancels
        // every pending block"). Does NOT read — callers resume reading themselves.
        private void EnterNode(string pNodeId)
        {
            DialogueNode lNode = _graph!.GetNode(pNodeId);
            _currentNodeId = lNode.Id;
            _frames.Clear();
            _frames.Push(new ReadFrame(lNode.Contents));
        }

        // Acts on the current content according to its concrete type. Pure-flow steps
        // (jump, block fall-through) are resolved in the loop without involving the View.
        private void ReadCurrentContent()
        {
            int lJumpAmount = 0;
            while (lJumpAmount < 1000)
            {
                // Re-read the top frame every iteration: jumps and pops change it.
                ReadFrame lFrame = _frames.Peek();

                // Past the last content of this frame: pop back to the parent (fall-through).
                // An empty stack means the node body itself is exhausted — implicit end.
                if (lFrame.Index >= lFrame.Contents.Count)
                {
                    _frames.Pop();
                    if (_frames.Count == 0)
                    {
                        End();
                        return;
                    }
                    continue;
                }

                switch (lFrame.Contents[lFrame.Index])
                {
                    case LineContent lLine:
                        _controller.ShowLine(lLine.Speaker, lLine.Line);
                        return;

                    case ChoiceContent lChoice:
                        _pendingChoice = lChoice;
                        CurrentState = State.WaitingForChoice;
                        _controller.ShowChoices(GetOptionLabels(lChoice));
                        return;

                    case JumpContent lJump:
                        EnterNode(lJump.TargetId);
                        lJumpAmount++;
                        continue;

                    case EndContent _:
                        End();
                        return;

                    default:
                        throw new InvalidOperationException(
                            $"Unhandled content type '{lFrame.Contents[lFrame.Index].GetType().Name}' in node '{_currentNodeId}'.");
                }
            }

            // Degrade gracefully for the player (close the dialogue), then scream for the dev.
            string lNodeId = _currentNodeId;
            End();
            throw new InvalidOperationException(
                 $"More than 1000 consecutive jumps without a line or a choice — " +
                 $"probable jump cycle involving node '{lNodeId}'.");
        }

        private void End()
        {
            CurrentState = State.Done;
            _graph = null;
            _frames.Clear();
            _pendingChoice = null;
            _controller.DialogueEnd();
        }

        private static IReadOnlyList<string> GetOptionLabels(ChoiceContent pChoice)
        {
            string[] lLabels = new string[pChoice.Options.Count];
            for (int i = 0; i < lLabels.Length; i++)
                lLabels[i] = pChoice.Options[i].Text;
            return lLabels;
        }

        // One position in the read stack: a content list (node body or inline choice block)
        // and a cursor into it. A class, not a struct: frames are mutated in place through
        // Peek(), which would silently operate on a throwaway copy with value semantics.
        private class ReadFrame
        {
            public List<IDialogueContent> Contents;
            public int Index;

            public ReadFrame(List<IDialogueContent> pContents)
            {
                Contents = pContents;
                Index = 0;
            }
        }
    }
}
