namespace ACSDlg.Core
{
    /// <summary>
    /// Engine-side source of the active locale. Keeps the core engine-agnostic: it only asks
    /// "which language now?" and the host answers (Unity Localization, Godot, a test stub).
    /// The returned code must match the keys passed to <see cref="DialogueLocalizer.SetCatalog"/>.
    /// </summary>
    public interface ILocaleProvider
    {
        /// <summary>Current locale code, e.g. "fr", "en", "de".</summary>
        string GetLocale();
    }
}
