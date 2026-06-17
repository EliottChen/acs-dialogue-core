# ACSDlg — UML du Core C#

*Diagramme de classes du core (`Core/src/`), état : architecture v0.2 + choix inline.*
*Se rend sur GitHub et dans VS Code (aperçu Markdown). Mettre à jour en même temps que le code.*

## Vue d'ensemble (pipeline)

```mermaid
flowchart LR
    SRC[".acsdlg<br/>(string, lue par le host)"] --> LEX[Lexer]
    LEX -- "List&lt;Token&gt;" --> PAR[Parser]
    PAR -- "DialogueGraph (AST)" --> RUN[DialogueRunner]
    RUN -- "IRunnerController" --> VIEW["View (Console / Unity / Godot)"]
    VIEW -- "Start / Update(dt) / Skip" --> ANIM[TypewriterAnimator]
    VIEW -- "Advance / SelectChoice / Stop" --> RUN
```

## Diagramme de classes

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
        -Stack~ReadFrame~ _frames
        -DialogueGraph? _graph
        -ChoiceContent? _pendingChoice
        +DialogueRunner(pController)
        +StartDialogue(pGraph)
        +Advance()
        +SelectChoice(pIndex)
        +Stop()
        -EnterNode(pNodeId)
        -ReadCurrentContent()
    }

    class ReadFrame {
        <<private>>
        +List~IDialogueContent~ Contents
        +int Index
    }

    class IRunnerController {
        <<interface>>
        +OnDialogueStart()
        +ShowLine(pSpeaker, pLine)
        +ShowChoices(pOptions)
        +Emit(pSignal)
        +OnDialogueEnded()
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

    %% ===================== VIEW (hors core, exemple) =====================
    class ConsoleDialoguePresenter {
        <<adapter — hors core>>
        -DialogueRunner _runner
        -TypewriterAnimator _animator
        +StartDialogue(pGraph)
    }

    %% ----- Relations Model -----
    DialogueGraph "1" *-- "many" DialogueNode : Nodes
    DialogueNode "1" o-- "many" IDialogueContent : Contents
    IDialogueContent <|.. LineContent
    IDialogueContent <|.. ChoiceContent
    IDialogueContent <|.. JumpContent
    IDialogueContent <|.. EndContent
    ChoiceContent "1" *-- "1..*" ChoiceOption : Options
    ChoiceOption "0..1" o-- "many" IDialogueContent : InlineContent (récursif)
    LineContent "1" *-- "1" ParsedLine
    ParsedLine "1" *-- "many" MarkupTag
    MarkupTag ..> MarkupKind

    %% ----- Relations Front-end -----
    Lexer ..> Token : produit
    Parser ..> Token : consomme
    Parser ..> MarkupParser : délègue le markup
    Parser ..> DialogueGraph : construit
    MarkupParser ..> ParsedLine : produit

    %% ----- Relations Presenter / Animation -----
    DialogueRunner --> IRunnerController : pilote (injecté au ctor)
    DialogueRunner ..> DialogueGraph : lit
    DialogueRunner "1" *-- "many" ReadFrame : pile de lecture
    ReadFrame o-- IDialogueContent : pointe dans l'AST
    TypewriterAnimator ..> ParsedLine : consomme
    TypewriterAnimator ..> TypewriterTick : produit

    %% ----- Frontière moteur -----
    IRunnerController <|.. ConsoleDialoguePresenter
    ConsoleDialoguePresenter *-- TypewriterAnimator : possède l'instance
    ConsoleDialoguePresenter --> DialogueRunner : commandes (Advance/SelectChoice)
```

## Légende des relations

| Flèche | Sens |
|---|---|
| `*--` (composition, losange plein) | possède et gère le cycle de vie (le graphe possède ses nodes) |
| `o--` (agrégation, losange vide) | référence sans posséder (une ReadFrame pointe dans l'AST, ne le possède pas) |
| `<|..` (réalisation, pointillés) | implémente l'interface |
| `..>` (dépendance, pointillés) | utilise / produit / consomme, sans détenir |
| `-->` (association) | tient une référence durable |

## Les trois lignes de force à retenir

1. **Le sens unique du pipeline** : `string → Lexer → Parser → DialogueGraph → DialogueRunner → IRunnerController`. Rien ne remonte ; la View répond uniquement par les commandes publiques du runner (`Advance`, `SelectChoice`, `Stop`).
2. **La frontière moteur passe par deux points seulement** : l'interface `IRunnerController` (le runner pilote la View sans la connaître) et le `TypewriterAnimator` (classe core, mais *instance* possédée et pulsée par chaque View avec son propre deltaTime). Tout le reste du core est invisible au moteur.
3. **La récursion du Model fait la récursion du runtime** : `ChoiceOption.InlineContent` boucle vers `IDialogueContent` (un bloc peut contenir des choix) ; côté runner, cette récursion se parcourt avec la pile de `ReadFrame` (push à l'entrée d'un bloc, pop = retombée à la Ink, clear sur jump).

## Rappels d'invariants (détail dans ACSDlg_Architecture_v0.2.md)

- Le core ne référence aucun moteur, aucun fichier, aucune horloge.
- Les types de contenu sont des données pures ; le comportement vit dans le runner (flux) et la View (rendu).
- `ChoiceOption` : exactement un de `TargetId` / `InlineContent` est renseigné (garanti par les deux constructeurs + le parser).
- Le markup est parsé une fois, à la construction de l'AST.
- Validation au parsing : cibles de jump/choice (récursif dans les blocs), `#start`, doublons de nodes, balises inconnues — `FormatException` avec ligne/colonne.
