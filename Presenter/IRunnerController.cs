using System.Collections.Generic;

namespace ACSDlg.Core
{
    /// <summary>
    /// Contract of the capabilities the engine-side View must provide. Thought in terms of
    /// capabilities (display, choose, emit), not content types, so adding a new instruction
    /// type does not widen the interface. Implemented once per engine (Unity, Godot, console).
    /// </summary>
    public interface IRunnerController
    {
        /// <summary>Prepare the UI: instantiate the bubble, freeze the gameplay context.</summary>
        void DialogueStart();

        /// <summary>Display one line. Null speaker = anonymous narration.</summary>
        void ShowLine(string? pSpeaker, ParsedLine pLine);

        /// <summary>Present the choice menu. The View answers with DialogueRunner.SelectChoice(index).</summary>
        void ShowChoices(IReadOnlyList<string> pOptions);

        /// <summary>Relay a gameplay signal ([emit:...]) to the engine.</summary>
        void Emit(string pSignal);

        /// <summary>Clean up the UI, restore the gameplay context.</summary>
        void DialogueEnd();
    }
}
