# Modular cross-engine dialogue system (ACSDlg) — Project context

## Goal

Build a **portable dialogue backend** reusable across several engines.
Immediate target: integration into **Corridor** (Unity game, Built-in pipeline, Unity 2022.3 LTS).
Long-term target: the same architecture under Godot (same C# core); for Unreal, ONE standard
C++ core (pure C++17, zero engine include, error handling by return values rather than
exceptions — Unreal disables them by default) developed first as a console app, then integrated
as-is as a third-party module; the Unreal adapter converts at the boundary
(std::string ↔ FString) and implements IRunnerController. Two cores in total (C#, C++),
never three. The C++ port also serves as a C++ learning path for Eliott
(possible differential: same .acsdlg, same piped inputs, diff the C#/C++ outputs).

The deliverable is the **dialogue system itself**: pure C# core + `.acsdlg` text file format
+ (later) Unity adapter and dialogue editor in Unity/Godot.

## Reference documents (docs/)

- `docs/ACSDlg_Spec_v0.1.md` — **spec of the** `.acsdlg` **text format** (nodes, lines, jump,
  choice, end, inline markup, choices with inline content + fall-through rule). It is the source
  of truth of the format. Any format change is decided THERE before touching the code.
- `docs/ACSDlg_Architecture_v0.3.md` — **class map** (MVP + core/engine boundary + localization).
  Source of truth of the architecture.

On code/docs divergence: report it, then align the docs (they describe the intent).

## Architecture — golden rule

**The core is pure C#, zero engine dependency.**

- `Core/` targets `netstandard2.0`. "Agnostic" = NO engine reference (`UnityEngine`/`Godot`)
  and NO host concern (`System.IO`, `Console`, reading the clock). The **netstandard BCL is
  fine** — `System`, `System.Collections.Generic`, `System.Linq`, `System.Xml.Linq` (used by
  `Xliff`). Linq or XML being in the BCL does not make the core engine-bound.
- The core reads no file (the host reads the file, the core parses the **string**), draws
  nothing, never reads the clock (the `TypewriterAnimator` receives a deltaTime).
- Needing an engine reference or a host concern = the code belongs to an ADAPTER, not the core.

### MVP reading (architecture doc v0.2)

```
CORE (netstandard2.0)
├── Model/        pure AST, produced by the parser, immutable after parsing
│     DialogueGraph → DialogueNode → List<IDialogueContent>
│     contents: LineContent / ChoiceContent / JumpContent / EndContent
│     LineContent: ParsedLine (display) + SourceText (raw, = translation key)
│     ChoiceOption: jump form (TargetId) OR block form (InlineContent) — never both
│     ParsedLine (CleanText + MarkupTag[]) — markup parsed ONCE, at construction
├── FrontEnd/     source text → Model
│     Lexer (char by char, tokens with line/column, TEXT mode after the 1st ':')
│     Parser (recursive descent over tokens, target validation at parse time)
│     MarkupParser (cursor model: speed/pause/tp/emit, aliases p/s, unknown tag = error)
├── Presenter/    the flow
│     DialogueRunner: drives the View through IRunnerController (injected in the constructor).
│     Optional DialogueLocalizer (also injected) → resolves lines/labels before ShowLine.
│     Read head = STACK of ReadFrame (content list + index):
│     entering an inline block = push; end of list = pop (Ink-style fall-through);
│     jump = clear the stack; end = stop regardless of depth.
│     Anti-cycle guard: >1000 consecutive jumps with no line/choice → End() then throw.
│     IRunnerController: DialogueStart / ShowLine / ShowChoices / Emit / DialogueEnd
├── Animation/    TypewriterAnimator: passive cursor driven by deltaTime, returns
│     (visible character count + crossed emits); Skip() reveals everything and defers
│     the emits to the next Update. One instance per View. Deterministic, testable.
└── Localization/ source text = key (gettext); XLIFF 1.2 exchange format
      ILocaleProvider (host tells the core the active locale)
      DialogueLocalizer (per-locale catalogs, re-parses a translation's markup, source fallback)
      Xliff (Export/Import, System.Xml.Linq, stable FNV id)
```

**Existing views / host:** `samples/Console/ConsoleDialoguePresenter` (implements
IRunnerController, Stopwatch loop — a preview of the Unity coroutine);
host `samples/Console/Program.cs` + `Menu`/`Host`/`AutoPlayer` (read file + parse + run,
XLIFF export/import, locale selection; `TestDialogue.acsdlg` copied next to the exe).

## LOCKED design decisions (do not revisit)

1. **Instantiable runner — NO singleton/static.** One conversation = one instance.
   Independent simultaneous dialogues (Paper Mario case). Zero static field.
2. **The Runner talks to the View through `IRunnerController` only** — a capability
   interface (display/choose/emit), not content types, so that a new instruction type
   does not widen the interface.
3. **Content types are pure data** (no `Read()` running engine code).
   Extension = new `IDialogueContent` class, never modifying the existing ones.
   NO behavioural hierarchy on the data (rejected: node subtypes).
4. **Timing lives in the adapter.** The Animator computes, does not render; the View applies.
5. **Validation at parse time, not at runtime** (Eliott's decision): `#start` resolved, duplicate
   nodes, existing jump/choice targets (recursive inside inline blocks), unknown markup tags
   → `FormatException` with line/column. Error messages name the expectation
   (“Expected X, got Y”) and the context (owning node/choice).
6. **AST model + runtime stack** (no flattening into an instruction stream): the AST
   maps 1:1 to the source file → it is the right model for the future visual editor.
   “Compile to a flat stream” = a possible optimisation LATER, downstream, without touching
   the format. Marked Someday/Maybe.
7. **Runtime anti-cycle jump guard**: degrades cleanly (End() closes the dialogue and
   frees the player via OnDialogueEnded) THEN throws with the node id. The criterion: a
   loop that consumes its input (lexer/parser/validation over a finite structure) needs no
   guard; a loop that navigates cyclic data (jumps) does.

## `.acsdlg` format — summary (the spec is authoritative)

```
#start Debut
// comment
node Debut {
    : anonymous narration ;
    Eliott : line [p:0.5] with inline markup [speed:2] [emit:Shake] ;
    choice "Jump form" jump Cible ;
    choice "Block form" {
        Eliott : inline content, nesting choices is allowed ;
    }
    Eliott : convergence point (fall-through of blocks without jump/end) ;
    jump Suite ;        // or end ;
}
```

- Whitespace/line breaks have no meaning; structure is carried by `node jump choice end
  { } : ;`. The first `:` of the statement = speaker/text separator; `\;` escapes the `;`.
- Cursor markup, no closing tag: `[speed:x]`/`[s:x]`, `[pause:x]`/`[p:x]`,
  `[tp]`, `[emit:Name]` (tag name case-insensitive, emit value preserved).
- Known hazard: a missing `;` mid-file swallows the text up to the next `;`
  (spec behaviour). A “jump/end detected inside text” lint is planned, not done.

## Scope

**Done:** lexer/parser/validation, stack runner (Ink fall-through), animator, console view,
nested inline-content choices, localization (gettext-style keying, XLIFF 1.2 export/import,
runtime resolution via `DialogueLocalizer` + `ILocaleProvider`).
**Next:** Unity adapter (`noEngineReferences` asmdef for the core,
MonoBehaviour View + coroutine pulsing the animator, emit → UnityEvent mapping,
`.acsdlg` ScriptedImporter). The pattern: the Unity View mirrors
`ConsoleDialoguePresenter`.
**Later (spec “out of scope”):** `if/else`, variables (`set`), choice guards
— expressions will then justify a dedicated expression parser plugged into
`ApplyInstruction`/`ParseContent`. Static jump-cycle lint at import.

## Code conventions (dev preferences)

- Code comments in **English**. Docs/spec in **English** too — **always English**.
- Prefixes: `l` for locals, `p` for parameters, `_` for private fields.
- Principles: KISS, DRY, SOLID. Composition over inheritance where it pays off.
  Fix any over-engineered suggestion quickly.
- Refactor and feature NEVER mix in the same step: extract, verify non-regression on
  `TestDialogue.acsdlg`, then plug in the new code.
- Test in the console via redirected stdin: the view handles `Console.IsInputRedirected`
  (empty lines = Advance, digit lines = SelectChoice).
- Eliott is learning C# in depth on this codebase: when he writes code himself,
  explain the pitfalls encountered (struct/class semantics, continue/return paths,
  implicit invariants) rather than silently fixing them.
