# ACSDlg — Architecture v0.3

*Achroma Station Dialogue System — class map and responsibilities*

Reference document for the implementation. Lists the classes, their main signatures, and
their role. Method bodies are **not** filled in: this is a map, not the implementation.

**Changes since v0.2:** adds the **Localization** layer (gettext-style keying, XLIFF
import/export, runtime resolution), faithful to the current code — `LineContent.SourceText`,
the optional `DialogueLocalizer` on the runner, the real `IRunnerController` method names,
and `netstandard2.0` as the actual target.

---

## Guiding principle: MVP + core/engine boundary

The system follows a **Model-View-Presenter** reading, and layers a strict
**neutral core / engine adapter** boundary that coincides with the Presenter / View
boundary.

```
┌────────────────────────── CORE (netstandard2.0, NO engine) ───────────────────────────┐
│                                                                                        │
│   MODEL                         PRESENTER                       (shared tool)          │
│   ├─ DialogueGraph              └─ DialogueRunner               └─ TypewriterAnimator   │
│   ├─ DialogueNode                                                                      │
│   ├─ IDialogueContent (+types)  FRONT-END (text → Model)        LOCALIZATION           │
│   ├─ ParsedLine / MarkupTag     ├─ Lexer / Token / TokenType    ├─ ILocaleProvider      │
│   └─ ...                        ├─ Parser                       ├─ DialogueLocalizer    │
│                                 └─ MarkupParser                 └─ Xliff                │
│                                                                                        │
│                        ▲ IRunnerController (interface)   ▲ runner→view calls           │
│                        ▲ ILocaleProvider (interface)     ▲ localizer→host call         │
└────────────────────────┼────────────────────────────────┼─────────────────────────────┘
                         │                                │
┌────────────────────────┼────────────────────────────────┼─────────────────────────────┐
│   HOST / VIEW (Unity / Godot / Console — implements IRunnerController + ILocaleProvider) │
│   └─ e.g. UnityDialoguePresenter : MonoBehaviour, IRunnerController                      │
│       ├─ owns a TypewriterAnimator instance (scenario 1)                                 │
│       └─ feeds the catalog + current locale to the DialogueLocalizer                     │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

**Golden rule:** the core never references an engine. "Agnostic" means **no engine
reference** (`UnityEngine`, `Godot`) and **no host concern** (`System.IO`, `Console`,
reading the clock). The full **netstandard BCL is allowed** — `System`,
`System.Collections.Generic`, `System.Linq`, `System.Xml.Linq` (used by `Xliff`), etc.
The View talks to the core through `IRunnerController` and the runner's public API; the
host supplies the current locale through `ILocaleProvider` and loads catalog files itself
(the core only ever sees strings).

---

## 1. MODEL — pure data (AST)

Passive data, immutable after parsing, with no engine behaviour and no flow logic.

### `DialogueGraph`
```csharp
public class DialogueGraph
{
    public string StartNodeId;                    // target of #start
    public IReadOnlyDictionary<string, DialogueNode> Nodes;

    public DialogueNode GetNode(string pId);
}
```
**Responsibility:** root container of a parsed dialogue. Gives access to nodes by id and
knows the entry point. Produced by the `Parser`, consumed by the `DialogueRunner`.

### `DialogueNode`
```csharp
public class DialogueNode
{
    public string Id;
    public List<IDialogueContent> Contents;       // ordered sequence
}
```
**Responsibility:** named container of a sequence of contents. The runner reads these
contents in order. No logic: it is a labelled list.

### `IDialogueContent` (+ implementations)
```csharp
public interface IDialogueContent { }             // type marker, no member

public class LineContent : IDialogueContent
{
    public string? Speaker;                        // null/empty = anonymous narration
    public ParsedLine Line;                        // clean text + pre-parsed markup (source language)
    public string SourceText;                      // raw line WITH markup — identity for translation lookup
}

public class ChoiceContent : IDialogueContent
{
    public List<ChoiceOption> Options;
}

public class JumpContent : IDialogueContent
{
    public string TargetId;
}

public class EndContent : IDialogueContent { }
```
**Responsibility:** each type represents a dialogue instruction, as **pure data**. The
concrete type is the discriminant the runner reads to decide what to do. Extension = a new
class implementing the interface, never modifying the existing ones.

> Content types contain **no** behaviour (no `Read()` running engine code). Behaviour lives
> in the runner. This preserves the neutrality of the core.

> **`SourceText` (added in v0.3).** `Line` is the source-language `ParsedLine`, computed once
> at parse time. `SourceText` keeps the raw line *with* its `[...]` markup — it is the key the
> `DialogueLocalizer` looks up, and the string the `Xliff` exporter ships. Markup positions
> are tied to text length, so a translation must be re-parsed per language (see §6).

### `ChoiceOption`
```csharp
public class ChoiceOption
{
    public string Text;                            // label shown to the player (also the translation key)
    public string? TargetId;                       // jump form: choice "..." jump Target ;
    public List<IDialogueContent>? InlineContent;  // block form: choice "..." { ... }
}
```
**Responsibility:** one option of a choice. Exactly **one** of the two fields is set
(invariant guaranteed by the parser): `TargetId` for the jump form, `InlineContent` for the
block form ("inline-content choice" extension, see spec §5). The block may contain other
`ChoiceContent` — nesting is recursive. `Text` is also the lookup key for the label's
translation (labels carry no markup, so they are not re-parsed).

**Fall-through semantics (implemented).** An inline block ending without `jump` or `end`
falls through to the content following the choice menu in its parent container, recursively
up to the node. In `DialogueRunner`, the read head is a **stack of read positions**
(`ReadFrame`: content list + index), pushed when entering an inline block, popped at its end
to resume on the parent's next content. A `jump` clears the stack (restart on a node), an
`end` ends the dialogue regardless of depth. See §3 `DialogueRunner`.

### `ParsedLine` and `MarkupTag`
```csharp
public class ParsedLine
{
    public string CleanText;                       // text without the [...] tags
    public IReadOnlyList<MarkupTag> Tags;          // tags positioned in CleanText
}

public enum MarkupKind { Speed, Pause, Teleport, Emit }

public struct MarkupTag
{
    public MarkupKind Kind;
    public int Position;                           // index in CleanText (before this character)
    public float NumericValue;                     // for speed/pause; 0 otherwise
    public string? StringValue;                    // for emit (case preserved); null otherwise
}
```
**Responsibility:** result of parsing the inline markup of a line. `CleanText` is what
displays, `Tags` carries the positioned directives (speed, pause, tp, emit). Parsed
**once** at AST construction, never re-parsed for display in the source language. Consumed
by `TypewriterAnimator`.

---

## 2. FRONT-END — source text → Model

Turns a `.acsdlg` file into a `DialogueGraph`. Lives in the core. Never plays a dialogue
(no engine, no runner).

### `TokenType` / `Token`
```csharp
public enum TokenType
{
    Hash, KeywordNode, KeywordJump, KeywordChoice, KeywordEnd,
    Identifier, Colon, Semicolon, LBrace, RBrace, String, Text, EndOfFile
}

public struct Token
{
    public TokenType Type;                         // the label
    public string Value;                           // the value (unused for punctuation)
    public int Line;                               // 1-based position for errors
    public int Column;
}
```
**Responsibility:** lexical unit. `Type` says *what kind* of token, `Value` carries the
variable content (identifiers, text, labels). `Line`/`Column` feed error messages
(and, later, a visual editor).

### `Lexer`
```csharp
public class Lexer
{
    public Lexer(string pSource);
    public List<Token> Tokenize();                 // throws on the 1st problem (v0)
}
```
**Responsibility:** splits the source text into tokens, **char by char** in one pass (no
split). Handles: keyword case, the first-`:` rule (switch to TEXT mode until `;`), `\;`
escaping, `//` comments, `"..."` strings. Does not understand the grammar — it labels. The
content of `[...]` tags stays **inside** the TEXT token (not broken down here).

### `Parser`
```csharp
public class Parser
{
    public Parser(List<Token> pTokens);
    public DialogueGraph Parse();                  // throws on the 1st problem (v0)
}
```
**Responsibility:** consumes the tokens and builds the `DialogueGraph` (nodes + contents).
Recognises each instruction by its head token (`node`, `jump`, `choice`, `end`, otherwise a
spoken line). Instantiates the right `IDialogueContent` type. Triggers markup parsing of each
line (produces the `ParsedLine`) **and keeps the raw line text in `LineContent.SourceText`**.
Validates the structure (balanced braces, `;` present, consistent jump targets).

### `MarkupParser`
```csharp
public static class MarkupParser
{
    public static ParsedLine Parse(string pRawText); // raw text → CleanText + Tags
}
```
**Responsibility:** turns the raw text of a line (with its `[...]`) into a `ParsedLine`.
Linear "cursor" pass: advance, extract each tag, record it with its position, remove it from
the displayed text. Resolves aliases (`p`/`pause`, `s`/`speed`), tag-name case (an emit
**value** keeps its case), the optional decimal. No stack (state model, not scopes).
Also re-used at runtime by the `DialogueLocalizer` to parse a translated line (see §6).

---

## 3. PRESENTER — the dialogue flow

The brain. Reads the Model, holds the read head (presentation state), drives the View
through `IRunnerController`. Lives in the core, knows no concrete engine.

### `DialogueRunner`
```csharp
public class DialogueRunner
{
    public DialogueRunner(IRunnerController pController, DialogueLocalizer? pLocalizer = null);

    // API called BY the View (incoming commands):
    public void StartDialogue(DialogueGraph pGraph);
    public void Advance();                          // line consumed → next content
    public void SelectChoice(int pIndex);           // the player has chosen
    public void Stop();                             // forced interruption

    public State CurrentState { get; }              // NotStarted / Playing / WaitingForChoice / Done

    // Internal read state (view-state) — IMPLEMENTED:
    // - Stack<ReadFrame>: a frame = (List<IDialogueContent> Contents, int Index).
    //   The node body is the base frame; each inline block pushes a frame.
    // - optional DialogueLocalizer: resolves lines/labels before sending them to the View.
}
```
**Responsibility:** flow orchestration. On `StartDialogue`: positions on the `#start` node,
notifies `DialogueStart`, reads the first content. For each content, acts by type: a line →
`ShowLine`; a choice → `ShowChoices` + waits; a jump → changes node; an end → `DialogueEnd`.
**Pure-flow** instructions (jump, block fall-through, later set/if) never touch the View. The
read head belongs to it (presentation state, not Model). **Knows nothing about the
typewriter**: it sends a line, animating it is the View's business.

**Localization hook (added in v0.3).** When a `DialogueLocalizer` is injected, the runner
resolves each line through `localizer.ResolveLine(line)` and each choice label through
`localizer.ResolveLabel(text)` before handing them to the View. When the localizer is
`null`, the dialogue plays in its source language with zero overhead (the field stays null,
the `?.` short-circuits). The View's interface is unchanged — it still receives a
`ParsedLine` and label strings, translated or not.

**Stack read head (implemented).** `SelectChoice` on an `InlineContent` option: advances the
current frame's index past the menu THEN pushes a frame on the block (otherwise fall-through
would re-serve the same menu). End of frame → pop: if a parent remains, reading resumes at
its index (Ink-style fall-through); empty stack → implicit end. `jump` → clear the stack +
fresh frame on the target node. `end` → end regardless of depth. `ReadFrame` is a **class**
(mutated in place via `Peek()` — a struct would be copied).

**Anti-cycle guard (implemented).** More than 1000 consecutive jumps with no line or choice →
the runner closes the dialogue cleanly (`End()` → `DialogueEnd`, the player is never
softlocked) THEN throws `InvalidOperationException` naming a node of the probable cycle.

---

## 4. LOCALIZATION — translation, engine-agnostic (added in v0.3)

Lives in the core. Keying is **gettext-style**: the raw source text *is* the key. Exchange
format is **XLIFF 1.2** (what CAT tools and agencies consume). Files are read by the host;
the core only manipulates strings.

### `ILocaleProvider`
```csharp
public interface ILocaleProvider
{
    string GetLocale();                            // current locale code, e.g. "fr", "en", "de"
}
```
**Responsibility:** the seam through which the engine tells the core the active language.
Implemented host-side (Unity Localization, Godot, a test stub). The returned code must match
the keys passed to `DialogueLocalizer.SetCatalog`.

### `DialogueLocalizer`
```csharp
public class DialogueLocalizer
{
    public DialogueLocalizer(ILocaleProvider pLocale);

    public void SetCatalog(string pLocale, IReadOnlyDictionary<string, string> pTable); // source text → translation
    public ParsedLine ResolveLine(LineContent pLine);   // translated + re-parsed, or source line on any miss
    public string ResolveLabel(string pLabel);          // translated label, or the source label on any miss
}
```
**Responsibility:** resolves a line or a choice label into the active locale. Holds one
catalog per locale (`locale → (source text → translation)`). Reads the active locale from the
`ILocaleProvider` on **every** lookup, so a mid-game language switch takes effect on the next
line. For a line, the translated raw text is **re-parsed by `MarkupParser`** so tag positions
match the translated wording. Source locale, missing catalog, missing key, or markup that a
translator broke all fall back to the already-parsed source line — the dialogue keeps playing
rather than failing.

### `Xliff`
```csharp
public static class Xliff
{
    public static string Export(DialogueGraph pGraph, string pSourceLang, string pTargetLang,
                                string pOriginal = "", IReadOnlyDictionary<string, string>? pTranslations = null);
    public static Dictionary<string, string> Import(string pXliff);   // source text → translation
}
```
**Responsibility:** bridges a `DialogueGraph` and XLIFF 1.2. `Export` walks the graph,
collects every translatable string (line `SourceText` + choice `Text`, deduplicated), and
emits one `<trans-unit>` per string: `<source>` = raw source text (the identity), `<target>`
empty by default or pre-filled from `pTranslations` (non-destructive re-export). The
`trans-unit id` is a stable FNV-1a content hash — an opaque handle for tools, never the
lookup key (so a hash collision cannot corrupt a translation). `Import` reads the targets
back into a runtime catalog, skipping empty targets. Uses `System.Xml.Linq` only.

---

## 5. VIEW — the engine boundary

Interface implemented on the engine side. Contains no flow logic: it displays what it is told
and reports inputs back.

### `IRunnerController`
```csharp
public interface IRunnerController
{
    void DialogueStart();                           // prepare the UI, instantiate the bubble, freeze the context
    void ShowLine(string? pSpeaker, ParsedLine pLine); // display a line (null speaker = narration)
    void ShowChoices(IReadOnlyList<string> pOptions);  // present the choice menu
    void Emit(string pSignal);                      // relay a gameplay signal to the engine
    void DialogueEnd();                             // clean up the UI, restore the context
}
```
**Responsibility:** contract of the **capabilities** the engine must provide — thought in
terms of capabilities (display, choose, emit), not content types, so that adding an
instruction type does not widen the interface. Implemented once per engine.

> The `ParsedLine` passed to `ShowLine` and the labels passed to `ShowChoices` are already
> localized when a `DialogueLocalizer` is in use — the View needs no localization awareness.

### `UnityDialoguePresenter` (example implementation — Unity project side)
```csharp
public class UnityDialoguePresenter : MonoBehaviour, IRunnerController
{
    private DialogueRunner _runner;
    private TypewriterAnimator _animator;           // instance owned by the View (scenario 1)

    public void StartDialogue(DialogueGraph pGraph); // public wrapper: the only entry point for the rest of the game

    // IRunnerController: DialogueStart / ShowLine / ShowChoices / Emit / DialogueEnd
    // Animation loop (Unity coroutine) + dialogue input reading:
    // - pulse _animator.Update(Time.deltaTime), apply the character count
    // - relay tick emits via this.Emit(...)
    // - on "skip" input → _animator.Skip()
    // - when _animator.GetAnimationDone() && "confirm" input → _runner.Advance()
}
```
**Responsibility:** engine adapter. Owns the runner (private) and animates it. The Unity host
also implements `ILocaleProvider` (a plain class returning
`LocalizationSettings.SelectedLocale.Identifier.Code`), loads the `.xlf` catalogs, and feeds
them to the `DialogueLocalizer` it injects into the runner.

---

## 6. SHARED TOOL — the typewriter animation

### `TypewriterAnimator`
```csharp
public class TypewriterAnimator
{
    public void Start(ParsedLine pLine);            // arm on a pre-parsed line
    public TypewriterTick Update(float pDeltaTime); // pulsed by the View, passive
    public void Skip();                             // reveal everything, mark remaining emits
    public bool GetAnimationDone();                 // true when the line is fully revealed
}

public struct TypewriterTick
{
    public int VisibleCharacterCount;               // the View applies (TMP maxVisibleChars / Substring)
    public IReadOnlyList<string> Emits;             // emits crossed THIS tick (often empty)
}
```
**Responsibility:** computes the animation progress of **one** line. State cursor (current
speed, instant mode, remaining pause time) advanced by the received `deltaTime`. `Update`
consumes **all** the delta in an internal loop: it can cross several characters, several
emits, and a pause boundary in the same call (framerate-independent). Returns a **character
count** (no substring: zero alloc per frame) and the emits crossed this tick. Passive (never
reads time), neutral (no engine), so **deterministic and testable** with a fixed delta. One
instance per View.

> Localization note: the animator always receives a `ParsedLine`. Whether that line is the
> source or a translated one (re-parsed by the localizer) is invisible to it — markup
> positions are always correct for the text it animates.

---

## Full loop (runtime flow reminder)

```
1. View → runner.StartDialogue(graph)
2. runner → controller.DialogueStart()
3. runner reads the current content:
     • LineContent  → line = localizer?.ResolveLine(content) ?? content.Line
                       → controller.ShowLine(speaker, line)
                       → View: animator.Start(line)
                       → each frame: tick = animator.Update(delta)
                                     apply tick.VisibleCharacterCount
                                     relay tick.Emits via controller.Emit(...)
                       → skip input → animator.Skip()
                       → animator.GetAnimationDone() && confirm input → runner.Advance()
     • ChoiceContent → labels resolved via localizer?.ResolveLabel(...) → controller.ShowChoices(...) ; wait
                       → choice input → runner.SelectChoice(index)
     • JumpContent   → change node (pure flow, no View) → re-read content
     • EndContent    → controller.DialogueEnd()
4. Advance / SelectChoice → advance the read head → back to 3.
```

Translation pipeline (offline, host-driven):
```
.acsdlg → Lexer → Parser → DialogueGraph
                              │
              Xliff.Export ───┼──► <name>.<lang>.xlf (empty targets) ──► translator / agency
                              │                                            │
   runtime: localizer ◄── SetCatalog ◄── Xliff.Import ◄── filled <name>.<lang>.xlf
```

---

## Boundaries never to cross (invariant recap)

1. **The core references no engine.** Lexer, Parser, Model, Runner, Animator, Localization:
   zero `using UnityEngine`. The netstandard BCL (incl. `System.Xml.Linq`) is fine.
2. **The core touches no file and no clock.** The host reads `.acsdlg`/`.xlf` and feeds
   strings in; the animator receives a deltaTime.
3. **Content types are pure data.** No engine behaviour inside.
4. **The Runner talks to the View through `IRunnerController` only**; it learns the locale
   through `ILocaleProvider` only — never a concrete engine class.
5. **The Animator computes, does not render.** It returns a number + emits; the View applies.
6. **Markup is parsed once for the source language** (AST construction); a translated line is
   re-parsed once on resolution. Never re-parsed every frame.
7. **`ChoiceOption`: exactly one of `TargetId` / `InlineContent` is set** (guaranteed by the
   two constructors + the parser).
8. **Translation identity is the source text** (gettext model); the XLIFF `id` is an opaque
   tool handle, never the key.
