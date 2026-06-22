# ACSDlg — cross-engine dialogue system

A **portable dialogue backend**, written in pure C# (`netstandard2.0`, zero engine dependency),
reusable under Unity, Godot, or as a console application. It provides:

- a **text format** `.acsdlg` (nodes, lines, `jump`, `choice`, `end`, inline markup `[speed]`/`[pause]`/`[tp]`/`[emit]`);
- a **pipeline** Lexer → Parser → `DialogueGraph` → `DialogueRunner` that drives display through the `IRunnerController` interface;
- built-in, engine-agnostic **localization**: the source text is the key (gettext model),
  exchange happens in **XLIFF 1.2** (the format translation agencies use).

> The core reads no file and draws nothing: the host reads the file and implements display.
> The `samples/Console` project is a reference host to do everything from the terminal.

## Quick start

Prerequisite: the .NET SDK. Run the commands **from the repository root**.

```bash
# Interactive menu: read a dialogue, switch language, export a translation
dotnet run --project samples/Console
```

## Commands (console host)

| Command | Effect |
|---|---|
| `dotnet run --project samples/Console` | Launches the **interactive menu** (`/play`, `/langage`, `/export`, `/quit`). |
| `dotnet run --project samples/Console -- --auto <file.acsdlg> [lang]` | Plays the dialogue **with no interaction** (auto-advances, takes the 1st choice). No lang code → source language; with one (`en`, `de`, …) → translated. Ideal for a quick check. |
| `dotnet run --project samples/Console -- --export <file.acsdlg> <lang>` | Generates `<name>.<lang>.xlf` **next to the dialogue** (UTF-8). Empty targets to fill; an already-translated file is **preserved** (non-destructive re-export). |
| `dotnet run --project samples/Console -- --selftest` | Runs the localizer assertions (resolution, fallback, broken markup). |

Examples:

```bash
dotnet run --project samples/Console -- --export samples/Console/TestDialogue.acsdlg de   # creates TestDialogue.de.xlf
dotnet run --project samples/Console -- --auto   samples/Console/TestDialogue.acsdlg de   # plays in German
```

## Localization: the workflow

1. **Export**: `--export <dialogue> <lang>` (or `/export` in the menu) → an `.xlf` with empty `<target>` elements.
2. **Translate**: fill each `<target>`, **without touching** the `<source>` or the `[...]` tags (keep them in place inside the translated sentence). Can be sent as-is to an agency.
3. **Place**: the `.xlf` must stay **in the same folder** as the `.acsdlg`, named `<name>.<lang>.xlf`
   (e.g. `TestDialogue.acsdlg` → `TestDialogue.de.xlf`).
4. **Play**: `--auto <dialogue> <lang>`, or in the menu `/langage` then `/play`.

Useful rules:

- **An empty `<target>` = the line shows in the source language** (fallback), never a hole or a crash.
- **Identical lines are merged**: you translate “Oui.” only once.
- The **language code is free** on the tooling side (it is just the file suffix). To wire Unity,
  name the `.xlf` with the code your locale will return (`zh-Hans`, not `cn`).

## Interactive menu

```
[fr] > /langage      pick the reading language (type a code: en, de, …)
[de] > /export       generate the .xlf of a language to translate
[de] > /play         read a .acsdlg (drag-and-drop the file) in the current language
[de] > /quit         quit
```

The current language is kept between two reads (shown in the `[xx] >` prompt).

## Connecting the language to a game engine (Unity)

The core is engine-agnostic, so wiring localization into Unity is three steps. Build the core
(`dotnet build -c Release`) and drop `ACSDlg.Core.dll` into `Assets/Plugins/`, then:

**1. Tell the core which language is active** — implement `ILocaleProvider` (a plain class,
no `MonoBehaviour` needed):

```csharp
using ACSDlg.Core;
using UnityEngine.Localization.Settings;

public sealed class UnityLocaleProvider : ILocaleProvider
{
    // Whatever the Unity Localization package has selected: "en", "fr", "de", "zh-Hans"…
    public string GetLocale() => LocalizationSettings.SelectedLocale.Identifier.Code;
}
```

**2. Load the catalogs and inject the localizer** — ship each `.xlf` as a `TextAsset`
(rename it `.xlf.txt`/`.bytes`, or add a tiny `ScriptedImporter`), then:

```csharp
using ACSDlg.Core;
using UnityEngine;

public class DialogueBootstrap : MonoBehaviour
{
    [System.Serializable] public struct Catalog { public string Locale; public TextAsset Xlf; }
    [SerializeField] Catalog[] _catalogs;   // e.g. { "en", TestDialogue.en.xlf }, { "de", … }

    DialogueLocalizer _localizer;

    void Awake()
    {
        _localizer = new DialogueLocalizer(new UnityLocaleProvider());
        foreach (Catalog lCat in _catalogs)
            _localizer.SetCatalog(lCat.Locale, Xliff.Import(lCat.Xlf.text));   // host reads the file, core sees strings
    }

    // Pass the localizer to the runner when you start a dialogue (your View implements IRunnerController):
    public void Play(DialogueGraph pGraph, IRunnerController pView)
        => new DialogueRunner(pView, _localizer).StartDialogue(pGraph);
}
```

**3. That's it for language switching.** `DialogueLocalizer` reads the locale from
`ILocaleProvider` on **every line**, so when the player changes language (Unity's
`LocalizationSettings.SelectedLocaleChanged`), the next line is already in the new language —
as long as that locale's catalog was loaded in step 2. Nothing to rebuild at runtime.

Notes:

- The locale **code must match** between `GetLocale()` and the `SetCatalog` key (`zh-Hans`, not `cn`).
- The `.xlf` **filename is irrelevant** inside Unity — you map code → `TextAsset` yourself; the
  `<name>.<lang>.xlf` convention only matters for the console host and the export step.
- The Unity Localization package handles UI strings, fonts, and locale selection; ACSDlg owns
  the **dialogue** strings. They don't overlap.
- For CJK, use a TextMeshPro font with a dynamic atlas covering the glyphs.

## Architecture & format

See `CLAUDE.md` (project map) and `docs/` (`.acsdlg` format spec and class architecture),
which are authoritative on the format and the core/engine boundary.
