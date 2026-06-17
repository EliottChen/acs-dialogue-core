# ACSDlg — Architecture v0.1

*Achroma Station Dialogue System — carte des classes et responsabilités*

Document de référence pour l'implémentation. Liste les classes, leurs signatures
principales et leur rôle. Les corps de méthodes ne sont **pas** remplis : c'est une
carte, pas l'implémentation.

---

## Principe directeur : MVP + frontière core/moteur

Le système suit une lecture **Model-View-Presenter**, et superpose une frontière stricte
**core neutre / adapter moteur** qui coïncide avec la frontière Presenter / View.

```
┌─────────────────────────── CORE (netstandard2.1, AUCUN moteur) ───────────────────────────┐
│                                                                                            │
│   MODEL                          PRESENTER                        (outil partagé)          │
│   ├─ DialogueGraph               └─ DialogueRunner                └─ TypewriterAnimator     │
│   ├─ DialogueNode                                                                           │
│   ├─ IDialogueContent (+ types)  FRONT-END (texte → Model)                                  │
│   ├─ ParsedLine / MarkupTag      ├─ Lexer / Token / TokenType                               │
│   └─ ...                         └─ Parser                                                  │
│                                                                                            │
│                         ▲ IRunnerController (interface)  ▲ appels runner→view              │
└─────────────────────────┼──────────────────────────────┼─────────────────────────────────┘
                          │                              │
┌─────────────────────────┼──────────────────────────────┼─────────────────────────────────┐
│   VIEW (Unity / Godot / Console — implémente IRunnerController)                             │
│   └─ p.ex. UnityDialoguePresenter : MonoBehaviour, IRunnerController                        │
│       └─ possède une instance de TypewriterAnimator (scénario 1)                            │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

**Règle d'or :** le core ne référence jamais un moteur. La View parle au core via
l'interface `IRunnerController` et l'API publique du runner. La classe `TypewriterAnimator`
vit dans le core (neutre, passive) mais ses **instances** sont possédées et pilotées par
chaque View.

---

## 1. MODEL — la donnée pure (AST)

Données passives, immuables après parsing, sans comportement moteur ni logique de flux.

### `DialogueGraph`
```csharp
public class DialogueGraph
{
    public string StartNodeId;                    // cible de #start
    public IReadOnlyDictionary<string, DialogueNode> Nodes;

    public DialogueNode GetNode(string pId);
}
```
**Responsabilité :** conteneur racine d'un dialogue parsé. Donne accès aux nodes par id
et connaît le point d'entrée. Produit par le `Parser`, consommé par le `DialogueRunner`.

### `DialogueNode`
```csharp
public class DialogueNode
{
    public string Id;
    public List<IDialogueContent> Contents;       // séquence ordonnée
}
```
**Responsabilité :** conteneur nommé d'une séquence de contenus. Le runner lit ces
contenus dans l'ordre. Aucune logique : c'est une liste étiquetée.

### `IDialogueContent` (+ implémentations)
```csharp
public interface IDialogueContent { }             // marqueur de type, aucun membre

public class LineContent : IDialogueContent
{
    public string? Speaker;                        // null/empty = narration anonyme
    public ParsedLine Line;                        // texte propre + markup pré-parsé
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
**Responsabilité :** chaque type représente une instruction de dialogue, en **données
pures**. Le type concret est le discriminant que le runner lit pour décider quoi faire.
Extension = nouvelle classe implémentant l'interface, jamais modification des existantes.

> Les types de contenu ne contiennent **aucun** comportement (pas de `Read()` exécutant du
> moteur). Le comportement vit dans le runner. Cela préserve la neutralité du core.

### `ChoiceOption`
```csharp
public class ChoiceOption
{
    public string Text;                            // libellé montré au joueur
    public string? TargetId;                       // forme saut : choice "..." jump Cible ;
    public List<IDialogueContent>? InlineContent;  // forme bloc : choice "..." { ... }
}
```
**Responsabilité :** une option d'un choix. Exactement **un** des deux champs est renseigné
(invariant garanti par le parser) : `TargetId` pour la forme saut, `InlineContent` pour la
forme bloc (extension « choix à contenu inline », voir spec §5). Le bloc peut contenir
d'autres `ChoiceContent` — l'imbrication est récursive.

**Sémantique de retombée (implémentée).** Un bloc inline qui se termine sans `jump` ni
`end` retombe sur le contenu qui suit le menu de choix dans son conteneur parent, récursivement
jusqu'au node. Côté `DialogueRunner`, la tête de lecture est une **pile de positions de
lecture** (`ReadFrame` : liste de contenus + index), empilée à l'entrée d'un bloc inline,
dépilée à sa fin pour reprendre au contenu suivant du parent. Un `jump` vide la pile (on
repart sur un node), un `end` termine le dialogue quelle que soit la profondeur.
Voir §3 `DialogueRunner` pour les détails d'implémentation.

### `ParsedLine` et `MarkupTag`
```csharp
public class ParsedLine
{
    public string CleanText;                       // texte sans les balises [...]
    public IReadOnlyList<MarkupTag> Tags;          // balises avec position dans CleanText
}

public enum MarkupKind { Speed, Pause, Teleport, Emit }

public struct MarkupTag
{
    public MarkupKind Kind;
    public int Position;                           // index dans CleanText (avant ce caractère)
    public float NumericValue;                     // pour speed/pause ; 0 sinon
    public string StringValue;                     // pour emit (casse préservée) ; null sinon
}
```
**Responsabilité :** résultat du parsing du markup inline d'une réplique. `CleanText` est ce
qui s'affiche, `Tags` porte les directives (vitesse, pause, tp, emit) positionnées. Parsé
**une fois** à la construction de l'AST, jamais re-parsé à l'affichage. Consommé par
`TypewriterAnimator`.

---

## 2. FRONT-END — texte source → Model

Transforme un fichier `.acsdlg` en `DialogueGraph`. Vit dans le core. Ne joue jamais de
dialogue (pas de moteur, pas de runner).

### `TokenType` / `Token`
```csharp
public enum TokenType
{
    Hash, KeywordNode, KeywordJump, KeywordChoice, KeywordEnd,
    Identifier, Colon, Semicolon, LBrace, RBrace, String, Text, EndOfFile
}

public struct Token
{
    public TokenType Type;                         // l'étiquette
    public string Value;                           // la valeur (inutilisé pour les ponctuateurs)
    public int Line;                               // position 1-based pour les erreurs
    public int Column;
}
```
**Responsabilité :** unité lexicale. `Type` dit *quelle sorte* de jeton, `Value` porte le
contenu variable (identifiants, texte, libellés). `Line`/`Column` servent aux messages
d'erreur (et, plus tard, à un éditeur visuel).

### `Lexer`
```csharp
public class Lexer
{
    public Lexer(string pSource);
    public List<Token> Tokenize();                 // lève une exception au 1er problème (v0)
}
```
**Responsabilité :** découpe le texte source en tokens, **caractère par caractère** en une
passe (pas de split). Gère : casse des mots-clés, règle du premier `:` (bascule en mode
TEXT jusqu'au `;`), échappement `\;`, commentaires `//`, chaînes `"..."`. Ne comprend pas
la grammaire — il étiquette. Le contenu des balises `[...]` reste **dans** le token TEXT
(non décortiqué ici).

### `Parser`
```csharp
public class Parser
{
    public Parser(List<Token> pTokens);
    public DialogueGraph Parse();                  // lève une exception au 1er problème (v0)
}
```
**Responsabilité :** consomme les tokens et construit le `DialogueGraph` (nodes + contenus).
Reconnaît la nature de chaque instruction par son token de tête (`node`, `jump`, `choice`,
`end`, sinon réplique). Instancie le bon type d'`IDialogueContent`. Déclenche le parsing du
markup de chaque réplique (produit les `ParsedLine`). Valide la structure (accolades
équilibrées, `;` présents, cibles de jump cohérentes).

### `MarkupParser`
```csharp
public static class MarkupParser
{
    public static ParsedLine Parse(string pRawText); // texte brut → CleanText + Tags
}
```
**Responsabilité :** transforme le texte brut d'une réplique (avec ses `[...]`) en
`ParsedLine`. Passe linéaire « à curseur » : avance, extrait chaque balise, l'enregistre
avec sa position, retire du texte affiché. Résout les alias (`p`/`pause`, `s`/`speed`), la
casse du nom de balise (la **valeur** d'un emit garde sa casse), la virgule optionnelle.
Aucune pile (modèle à état, pas à portée).

---

## 3. PRESENTER — le flux du dialogue

Le cerveau. Lit le Model, tient la tête de lecture (état de présentation), pilote la View
via `IRunnerController`. Vit dans le core, ne connaît aucun moteur concret.

### `DialogueRunner`
```csharp
public class DialogueRunner
{
    public DialogueRunner(IRunnerController pController);

    // API appelée PAR la View (commandes entrantes) :
    public void StartDialogue(DialogueGraph pGraph);
    public void Advance();                          // ligne consommée → contenu suivant
    public void SelectChoice(int pIndex);           // le joueur a choisi
    public void Stop();                             // interruption forcée (décision actée)

    public State CurrentState { get; }              // NotStarted / Playing / WaitingForChoice / Done

    // État de lecture (view-state) interne — IMPLÉMENTÉ :
    // - Stack<ReadFrame> : une frame = (List<IDialogueContent> Contents, int Index).
    //   Le corps du node est la frame de base ; chaque bloc inline empile une frame.
    // - (plus tard) variables $ mutées en cours de session
}
```
**Responsabilité :** orchestration du flux. Au `StartDialogue` : se place sur le node
`#start`, notifie `OnDialogueStart`, lit le premier contenu. Pour chaque contenu, agit selon
son type (switch ou délégation) : une réplique → `ShowLine` ; un choix → `ShowChoices` +
passe en attente ; un jump → change de node ; un end → `OnDialogueEnded`. Les instructions
de **flux pur** (jump, retombée de bloc, plus tard set/if) ne touchent pas la View. La tête
de lecture lui appartient (état de présentation, pas Model). **Ne connaît pas le typewriter** :
il envoie une ligne, l'animation est l'affaire de la View.

**Tête de lecture à pile (implémenté).** `SelectChoice` sur une option à `InlineContent` :
avance l'index de la frame courante au-delà du menu PUIS empile une frame sur le bloc
(sinon la retombée re-servirait le même menu). Fin de frame → pop : s'il reste un parent,
la lecture reprend à son index (retombée à la Ink) ; pile vide → fin implicite. `jump` →
clear de la pile + frame neuve sur le node cible. `end` → fin quelle que soit la profondeur.
`ReadFrame` est une **class** (mutée en place via `Peek()` — une struct serait copiée).

**Garde anti-cycle (implémenté).** Plus de 1000 jumps consécutifs sans réplique ni choix →
le runner ferme proprement le dialogue (`End()` → `OnDialogueEnded`, le joueur n'est jamais
softlocké) PUIS lève `InvalidOperationException` nommant un node du cycle probable.
Critère retenu : les boucles qui consomment leur entrée (lexer, parser, validation d'un AST
fini) n'ont pas besoin de garde ; les boucles qui naviguent dans des données cycliques
(les jumps) si.

---

## 4. VIEW — la frontière moteur

Interface implémentée côté moteur. Ne contient aucune logique de flux : elle affiche ce
qu'on lui dit et remonte les inputs.

### `IRunnerController`
```csharp
public interface IRunnerController
{
    void OnDialogueStart();                         // prépare l'UI, instancie la bulle, gèle le contexte
    void ShowLine(string? pSpeaker, ParsedLine pLine); // affiche une réplique (speaker null = narration)
    void ShowChoices(IReadOnlyList<string> pOptions);  // présente le menu de choix
    void Emit(string pSignal);                      // relaie un signal gameplay au moteur
    void OnDialogueEnded();                          // nettoie l'UI, rétablit le contexte
}
```
**Responsabilité :** contrat des **capacités** que le moteur doit fournir — pensé en termes
de capacités (afficher, choisir, émettre), pas en types de contenu, pour que l'ajout d'un
type d'instruction n'élargisse pas l'interface. Implémentée une fois par moteur.

### `UnityDialoguePresenter` (exemple d'implémentation — côté projet Unity)
```csharp
public class UnityDialoguePresenter : MonoBehaviour, IRunnerController
{
    private DialogueRunner _runner;
    private TypewriterAnimator _animator;           // instance possédée par la View (scénario 1)

    // Wrapper public : seule porte d'entrée pour le reste du jeu
    public void StartDialogue(DialogueGraph pGraph);

    // IRunnerController :
    public void OnDialogueStart();
    public void ShowLine(string? pSpeaker, ParsedLine pLine); // arme l'animator
    public void ShowChoices(IReadOnlyList<string> pOptions);
    public void Emit(string pSignal);
    public void OnDialogueEnded();

    // Boucle d'animation (coroutine Unity) + lecture des inputs dialogue :
    // - pulse _animator.Update(Time.deltaTime), applique le compte de caractères
    // - relaie les emit du tick via this.Emit(...)
    // - sur input "skip" → _animator.Skip()
    // - quand _animator.GetAnimationDone() && input "valider" → _runner.Advance()
    // - gère le state joueur (IsInDialogue) via le FSM moteur, si un joueur existe
}
```
**Responsabilité :** adapter moteur. Possède le runner (privé) et l'anime. Capte les inputs
*du dialogue* indépendamment du state du personnage (cohérent cinématique « dans le vide »).
Pilote l'animator avec le `deltaTime` Unity. Fait le pont avec le contexte moteur (bulle,
gel du joueur via son FSM) dans `OnDialogueStart` / `OnDialogueEnded`. **Seul `StartDialogue`
est public** ; les commandes en cours de dialogue (Advance, SelectChoice) sont appelées en
interne depuis les inputs captés.

---

## 5. OUTIL PARTAGÉ — l'animation typewriter

### `TypewriterAnimator`
```csharp
public class TypewriterAnimator
{
    public void Start(ParsedLine pLine);            // arme sur une ligne pré-parsée
    public TypewriterTick Update(float pDeltaTime); // pulsé par la View, passif
    public void Skip();                             // révèle tout, marque les emit restants
    public bool GetAnimationDone();                 // true quand la ligne est entièrement révélée
}

public struct TypewriterTick
{
    public int VisibleCharacterCount;               // la View applique (TMP maxVisibleChars / Substring)
    public IReadOnlyList<string> Emits;             // emit franchis CE tick (souvent vide)
}
```
**Responsabilité :** calcule l'avancement de l'animation d'**une** ligne. Curseur à état
(vitesse courante, mode instantané, temps de pause restant) avancé par le `deltaTime` reçu.
`Update` consomme **tout** le delta en une boucle interne : il peut franchir plusieurs
caractères, plusieurs emit et une frontière de pause dans le même appel (indépendant du
framerate). Retourne un **compte de caractères** (pas de sous-chaîne : zéro alloc par frame)
et la liste des emit franchis ce tick. Passif (ne cherche jamais le temps), neutre (aucun
moteur), donc **déterministe et testable** avec un delta fixe. Une instance par View.

Le TypeWriterAnimator contient une vitesse de lecture que les balises speed et pause et tp doivent modifier
Soit via TypeWriter directement, dans la fonction TypeWriterTick l'animator voir une balise et adapte sa vitesse
en fonction, ou fait une pause, la fonction Skip affiche le reste et envoie tous les balises emits en même temps

---

## Boucle complète (rappel du flux runtime)

```
1. View → runner.StartDialogue(graph)
2. runner → controller.OnDialogueStart()
3. runner lit le contenu courant :
     • LineContent  → controller.ShowLine(speaker, line)
                       → View : animator.Start(line)
                       → chaque frame : tick = animator.Update(delta)
                                        applique tick.VisibleCharacterCount
                                        relaie tick.Emits via controller.Emit(...)
                       → input skip → animator.Skip()
                       → animator.GetAnimationDone() && input valider → runner.Advance()
     • ChoiceContent → controller.ShowChoices(options) ; attente
                       → input choix → runner.SelectChoice(index)
     • JumpContent   → change de node (flux pur, pas de View) → relire contenu
     • EndContent    → controller.OnDialogueEnded()
4. Advance / SelectChoice → avance la tête de lecture → retour en 3.
```

---

## Frontières à ne jamais franchir (récapitulatif des invariants)

1. **Le core ne référence aucun moteur.** Lexer, Parser, Model, Runner, Animator : zéro
   `using UnityEngine`.
2. **Les types de contenu sont des données pures.** Pas de comportement moteur dedans.
3. **Le Runner parle à la View via `IRunnerController` seulement**, jamais à une classe
   moteur concrète (règle presenter/view de MVP = frontière cross-engine).
4. **L'Animator calcule, ne rend pas.** Il retourne un nombre + des emit ; la View applique.
5. **`Read()`/comportement moteur n'est jamais sur le Model.** Le « quoi faire » vit dans le
   Runner (flux) et la View (rendu).
6. **Le markup est parsé une fois** (construction de l'AST), jamais à l'affichage.
```
