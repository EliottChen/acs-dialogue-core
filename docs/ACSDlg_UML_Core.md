# ACSDlg — C# Core UML

*Class diagram of the core, state: architecture v0.3 (inline choices + localization).*
*Renders on GitHub and in VS Code (Markdown preview). Update it together with the code.*

## Overview (pipeline)

```mermaid
flowchart LR
    SRC[".acsdlg<br/>(string, read by the host)"] --> LEX[Lexer]
    LEX -- "List&lt;Token&gt;" --> PAR[Parser]
    PAR -- "DialogueGraph (AST)" --> RUN[DialogueRunner]
    RUN -- "IRunnerController" --> VIEW["View (Console / Unity / Godot)"]
    VIEW -- "Start / Update(dt) / Skip" --> ANIM[TypewriterAnimator]
    VIEW -- "Advance / SelectChoice / Stop" --> RUN
    RUN -. "ResolveLine / ResolveLabel" .-> LOC[DialogueLocalizer]
    LOC -- "GetLocale()" --> LP["ILocaleProvider (host)"]
```

## Localization pipeline (offline, host-driven)

```mermaid
flowchart LR
    AST["DialogueGraph"] --> EXP["Xliff.Export"]
    EXP -- "&lt;name&gt;.&lt;lang&gt;.xlf<br/>(empty targets)" --> TR["translator / agency"]
    TR -- "filled .xlf" --> IMP["Xliff.Import"]
    IMP -- "source → translation" --> SC["DialogueLocalizer.SetCatalog"]
```

## Class diagram

```mermaid
classDiagram
    direction TB

    %% ===================== MODEL (AST) =====================
    class DialogueGraph {
        +string StartNodeId
        +IReadOnlyDictionary~string,DialogueNode~ Nodes
        +GetNode(pId) DialogueNode
    }

    class DialogueNode {
        +string Id
        +List~IDialogueContent~ Contents
    }

    class IDialogueContent {
        <<interface>>
    }

    class LineContent {
        +string? Speaker
        +ParsedLine Line
        +string SourceText
    }

    class ChoiceContent {
        +List~ChoiceOption~ Options
    }

    class ChoiceOption {
        +string Text
        +string? TargetId
        +List~IDialogueContent~? InlineContent
        +ChoiceOption(pText, pTargetId)
        +ChoiceOption(pText, pInlineContent)
    }

    class JumpContent {
        +string TargetId
    }

    class EndContent {
    }

    class ParsedLine {
        +string CleanText
        +IReadOnlyList~MarkupTag~ Tags
    }

    class MarkupTag {
        <<struct>>
        +MarkupKind Kind
        +int Position
        +float NumericValue
        +string? StringValue
    }

    class MarkupKind {
        <<enumeration>>
        Speed
        Pause
        Teleport
        Emit
    }

    %% ===================== FRONT-END =====================
    class Lexer {
        -string _src
        -int _idx / _line / _column
        +Tokenize() List~Token~
    }

    class Token {
        <<struct>>
        +TokenType Type
        +string Value
        +int Line
        +int Column
    }

    class Parser {
        -List~Token~ _tokens
        -int _pos
        +Parse() DialogueGraph
        -ParseNodeDeclaration()
        -ParseContentUntilEndBrace(pOwnerName)
        -ParseContent()
        -ParseChoiceGroup()
        -ParseLine()
        -ValidateTargets(pNodes)$
        -ValidateContents(pContents, pNodes, pNodeId)$
    }

    class MarkupParser {
        <<static>>
        +Parse(pRawText)$ ParsedLine
    }

    %% ===================== PRESENTER =====================
    class DialogueRunner {
        +State CurrentState
        -IRunnerController _controller
        -DialogueLocalizer? _localizer
        -Stack~ReadFrame~ _frames
        -DialogueGraph? _graph
        -ChoiceContent? _pendingChoice
        +DialogueRunner(pController, pLocalizer)
        +StartDialogue(pGraph)
        +Advance()
        +SelectChoice(pIndex)
        +Stop()
        -EnterNode(pNodeId)
        -ReadCurrentContent()
        -GetOptionLabels(pChoice)
    }

    class ReadFrame {
        <<private>>
        +List~IDialogueContent~ Contents
        +int Index
    }

    class IRunnerController {
        <<interface>>
        +DialogueStart()
        +ShowLine(pSpeaker, pLine)
        +ShowChoices(pOptions)
        +Emit(pSignal)
        +DialogueEnd()
    }

    %% ===================== LOCALIZATION =====================
    class ILocaleProvider {
        <<interface>>
        +GetLocale() string
    }

    class DialogueLocalizer {
        -ILocaleProvider _locale
        -Dictionary~string,IReadOnlyDictionary~ _catalogs
        +DialogueLocalizer(pLocale)
        +SetCatalog(pLocale, pTable)
        +ResolveLine(pLine) ParsedLine
        +ResolveLabel(pLabel) string
        -TryTranslate(pSource, out pTranslated) bool
    }

    class Xliff {
        <<static>>
        +Export(pGraph, pSourceLang, pTargetLang, pOriginal, pTranslations)$ string
        +Import(pXliff)$ Dictionary~string,string~
        -SourceStrings(pGraph)$
        -Collect(pContents, pOut, pSeen)$
        -Hash(pText)$ string
    }

    %% ===================== ANIMATION =====================
    class TypewriterAnimator {
        +float BaseSecondsPerCharacter
        -ParsedLine? _line
        -int _visibleCount / _tagIndex
        -float _speedMultiplier / _pauseRemaining
        -bool _instant
        +Start(pLine)
        +Update(pDeltaTime) TypewriterTick
        +Skip()
        +GetAnimationDone() bool
    }

    class TypewriterTick {
        <<struct>>
        +int VisibleCharacterCount
        +IReadOnlyList~string~ Emits
    }

    %% ===================== VIEW / HOST (out of core, example) =====================
    class ConsoleDialoguePresenter {
        <<adapter — out of core>>
        -DialogueRunner _runner
        -TypewriterAnimator _animator
        +ConsoleDialoguePresenter(pLocalizer)
        +StartDialogue(pGraph)
    }

    %% ----- Model relations -----
    DialogueGraph "1" *-- "many" DialogueNode : Nodes
    DialogueNode "1" o-- "many" IDialogueContent : Contents
    IDialogueContent <|.. LineContent
    IDialogueContent <|.. ChoiceContent
    IDialogueContent <|.. JumpContent
    IDialogueContent <|.. EndContent
    ChoiceContent "1" *-- "1..*" ChoiceOption : Options
    ChoiceOption "0..1" o-- "many" IDialogueContent : InlineContent (recursive)
    LineContent "1" *-- "1" ParsedLine
    ParsedLine "1" *-- "many" MarkupTag
    MarkupTag ..> MarkupKind

    %% ----- Front-end relations -----
    Lexer ..> Token : produces
    Parser ..> Token : consumes
    Parser ..> MarkupParser : delegates markup
    Parser ..> DialogueGraph : builds

    MarkupParser ..> ParsedLine : produces

    %% ----- Presenter / Animation relations -----
    DialogueRunner --> IRunnerController : drives (injected in ctor)
    DialogueRunner ..> DialogueLocalizer : resolves (optional, injected)
    DialogueRunner ..> DialogueGraph : reads
    DialogueRunner "1" *-- "many" ReadFrame : read stack
    ReadFrame o-- IDialogueContent : points into the AST
    TypewriterAnimator ..> ParsedLine : consumes
    TypewriterAnimator ..> TypewriterTick : produces

    %% ----- Localization relations -----
    DialogueLocalizer --> ILocaleProvider : asks the locale
    DialogueLocalizer ..> LineContent : reads SourceText
    DialogueLocalizer ..> MarkupParser : re-parses the translation
    Xliff ..> DialogueGraph : reads
    Xliff ..> LineContent : SourceText
    Xliff ..> ChoiceOption : Text

    %% ----- Engine boundary -----
    IRunnerController <|.. ConsoleDialoguePresenter
    ConsoleDialoguePresenter *-- TypewriterAnimator : owns the instance
    ConsoleDialoguePresenter --> DialogueRunner : commands (Advance/SelectChoice)
```

## Relation legend

| Arrow | Meaning |
|---|---|
| `*--` (composition, filled diamond) | owns and manages the lifecycle (the graph owns its nodes) |
| `o--` (aggregation, empty diamond) | references without owning (a ReadFrame points into the AST, does not own it) |
| `<|..` (realization, dashed) | implements the interface |
| `..>` (dependency, dashed) | uses / produces / consumes, without holding |
| `-->` (association) | holds a durable reference |

## The four lines of force to remember

1. **The one-way pipeline**: `string → Lexer → Parser → DialogueGraph → DialogueRunner → IRunnerController`. Nothing flows back up; the View answers only through the runner's public commands (`Advance`, `SelectChoice`, `Stop`).
2. **The engine boundary passes through three points only**: the interface `IRunnerController` (the runner drives the View without knowing it), the `ILocaleProvider` interface (the core learns the active locale without knowing the engine), and the `TypewriterAnimator` (core class, but its *instance* is owned and pulsed by each View with its own deltaTime). Everything else in the core is invisible to the engine.
3. **The Model recursion drives the runtime recursion**: `ChoiceOption.InlineContent` loops back to `IDialogueContent` (a block can contain choices); runner-side, that recursion is walked with the `ReadFrame` stack (push on entering a block, pop = Ink-style fall-through, clear on jump).
4. **Localization is optional and source-keyed**: the runner resolves through a `DialogueLocalizer` only when one is injected (`null` = source language, zero overhead). The lookup key is the raw `SourceText`; a translated line is re-parsed once by `MarkupParser`; the XLIFF `id` is an opaque tool handle, never the key.

## Invariant reminders (details in ACSDlg_Architecture_v0.3.md)

- The core references no engine, no file, no clock. The netstandard BCL (incl. `System.Xml.Linq`, used by `Xliff`) is allowed.
- Content types are pure data; behaviour lives in the runner (flow) and the View (rendering).
- `ChoiceOption`: exactly one of `TargetId` / `InlineContent` is set (guaranteed by the two constructors + the parser).
- Markup is parsed once at AST construction for the source language; a translated line is re-parsed once on resolution.
- Parse-time validation: jump/choice targets (recursive inside blocks), `#start`, duplicate nodes, unknown tags — `FormatException` with line/column.
- Translation identity is the source text (gettext model); empty XLIFF targets fall back to the source line at runtime.
```
