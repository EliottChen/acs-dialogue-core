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

## Architecture & format

See `CLAUDE.md` (project map) and `docs/` (`.acsdlg` format spec and class architecture),
which are authoritative on the format and the core/engine boundary.
