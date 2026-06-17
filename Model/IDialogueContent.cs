namespace ACSDlg.Core
{
    /// <summary>
    /// Type marker for every instruction a <see cref="DialogueNode"/> can contain.
    /// The concrete type is the discriminant the runner reads to decide what to do.
    /// Content types are pure data — no behaviour, no engine reference. Extension means
    /// adding a new implementing class, never modifying the existing ones.
    /// </summary>
    public interface IDialogueContent { }
}
