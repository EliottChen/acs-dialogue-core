# Système de dialogue modulaire cross-engine (ACSDlg) — Contexte projet

## Objectif

Construire un **backend de dialogue portable** réutilisable sur plusieurs moteurs.
Cible immédiate : intégration dans **Corridor** (jeu Unity, Built-in pipeline, Unity 2022.3 LTS).
Cible long terme : même architecture sous Godot (même core C#) ; pour Unreal, UN core C++
standard (C++17 pur, zéro include moteur, gestion d'erreur par retours plutôt que par
exceptions — Unreal les désactive par défaut) développé d'abord en app console, puis intégré
tel quel comme module tiers ; l'adaptateur Unreal convertit à la frontière
(std::string ↔ FString) et implémente IRunnerController. Deux cores au total (C#, C++),
jamais trois. Le port C++ sert aussi de parcours d'apprentissage C++ pour Eliott
(différentiel possible : mêmes .acsdlg, mêmes inputs pipés, diff des sorties C#/C++).

Le livrable est le **système de dialogue lui-même** : core C# pur + format de fichier texte
`.acsdlg` + (plus tard) adaptateur Unity et éditeur de dialogue dans Unity/Godot.

## Documents de référence (docs/)

- `docs/ACSDlg_Spec_v0.1.md` — **spec du format texte** `.acsdlg` (nodes, lignes, jump,
  choice, end, markup inline, choix à contenu inline + règle de retombée). C'est la source
  de vérité du format. Toute évolution du format se décide LÀ avant de toucher au code.
- `docs/ACSDlg_Architecture_v0.2.md` — **carte des classes** (MVP + frontière core/moteur).
  Source de vérité de l'architecture.

En cas de divergence code/docs : signaler, puis aligner les docs (ils décrivent l'intention).

## Architecture — règle d'or

**Le core est en C# pur, zéro dépendance moteur.**

- `Core/` cible `netstandard2.1`. AUCUN `using UnityEngine`/`Godot`, AUCUN `System.IO`,
  AUCUN `Console`. Uniquement `System` et `System.Collections.Generic`.
- Le core ne lit aucun fichier (le host lit le fichier, le core parse la **string**), ne
  dessine rien, ne lit jamais l'horloge (le `TypewriterAnimator` reçoit un deltaTime).
- Besoin d'un de ces imports = le code appartient à un ADAPTATEUR, pas au core.

### Lecture MVP (doc d'architecture v0.2)

```
CORE (netstandard2.1)
├── Model/        AST pur, produit par le parser, immuable après parsing
│     DialogueGraph → DialogueNode → List<IDialogueContent>
│     contenus : LineContent / ChoiceContent / JumpContent / EndContent
│     ChoiceOption : forme saut (TargetId) OU forme bloc (InlineContent) — jamais les deux
│     ParsedLine (CleanText + MarkupTag[]) — markup parsé UNE fois, à la construction
├── FrontEnd/     texte source → Model
│     Lexer (char par char, tokens avec ligne/colonne, mode TEXT après le 1er ':')
│     Parser (descente récursive sur tokens, validation des cibles au parsing)
│     MarkupParser (modèle curseur : speed/pause/tp/emit, alias p/s, balise inconnue = erreur)
├── Presenter/    le flux
│     DialogueRunner : pilote la View via IRunnerController (injecté au constructeur).
│     Tête de lecture = PILE de ReadFrame (liste de contenus + index) :
│     entrer dans un bloc inline = push ; fin de liste = pop (retombée à la Ink) ;
│     jump = clear de la pile ; end = fin quelle que soit la profondeur.
│     Garde anti-cycle : >1000 jumps consécutifs sans ligne/choix → End() puis throw.
│     IRunnerController : OnDialogueStart / ShowLine / ShowChoices / Emit / OnDialogueEnded
└── Animation/    TypewriterAnimator : curseur passif piloté au deltaTime, retourne
      (compte de caractères visibles + emits franchis) ; Skip() révèle tout et diffère
      les emits au prochain Update. Une instance par View. Déterministe, testable.
```

**Vues existantes :** `CsharpConsoleAppCore/ConsoleDialoguePresenter` (implémente
IRunnerController, boucle Stopwatch — préfiguration de la coroutine Unity) ;
host `CsharpConsoleAppSample/Program.cs` (lecture fichier + parse + run,
fichier de test `TestDialogue.acsdlg` copié à côté de l'exe).

## Décisions de design VERROUILLÉES (ne pas revenir dessus)

1. **Runner instanciable — PAS de singleton/static.** Une conversation = une instance.
   Dialogues simultanés indépendants (cas Paper Mario). Zéro champ static.
2. **Le Runner parle à la View via `IRunnerController` uniquement** — interface de
   capacités (afficher/choisir/émettre), pas de types de contenu, pour qu'un nouveau type
   d'instruction n'élargisse pas l'interface.
3. **Les types de contenu sont des données pures** (pas de `Read()` exécutant du moteur).
   Extension = nouvelle classe `IDialogueContent`, jamais modification des existantes.
   PAS de hiérarchie comportementale sur la donnée (rejeté : sous-types de nodes).
4. **Le timing vit dans l'adaptateur.** L'Animator calcule, ne rend pas ; la View applique.
5. **Validation au parsing, pas au runtime** (décision Eliott) : `#start` résolu, nodes
   dupliqués, cibles de jump/choice existantes (récursif dans les blocs inline), balises
   markup inconnues → `FormatException` avec ligne/colonne. Les messages d'erreur nomment
   l'attente (« Expected X, got Y ») et le contexte (node/choice propriétaire).
6. **Modèle AST + pile au runtime** (pas d'aplatissement en flux d'instructions) : l'AST
   correspond 1:1 au fichier source → c'est le bon modèle pour le futur éditeur visuel.
   « Compiler vers un flux plat » = optimisation possible PLUS TARD, en aval, sans toucher
   au format. Noté Someday/Maybe.
7. **Garde runtime anti-cycle de jumps** : dégrade proprement (End() ferme le dialogue et
   libère le joueur via OnDialogueEnded) PUIS throw avec l'id du node. Le critère : une
   boucle qui consomme son entrée (lexer/parser/validation sur structure finie) n'a pas
   besoin de garde ; une boucle qui navigue dans des données cycliques (jumps) si.

## Format `.acsdlg` — résumé (la spec fait foi)

```
#start Debut
// commentaire
node Debut {
    : narration anonyme ;
    Eliott : réplique [p:0.5] avec markup [speed:2] inline [emit:Shake] ;
    choice "Forme saut" jump Cible ;
    choice "Forme bloc" {
        Eliott : contenu inline, imbrication de choices autorisée ;
    }
    Eliott : point de convergence (retombée des blocs sans jump/end) ;
    jump Suite ;        // ou end ;
}
```

- Espaces/sauts de ligne sans signification ; structure portée par `node jump choice end
  { } : ;`. Premier `:` du statement = séparateur speaker/texte ; `\;` échappe le `;`.
- Markup à curseur, sans balise fermante : `[speed:x]`/`[s:x]`, `[pause:x]`/`[p:x]`,
  `[tp]`, `[emit:Nom]` (casse du nom de balise insensible, valeur d'emit préservée).
- Hazard connu : un `;` manquant en milieu de fichier avale le texte jusqu'au `;` suivant
  (comportement spec). Un lint « jump/end détecté dans un texte » est prévu, pas fait.

## Périmètre

**Fait :** lexer/parser/validation, runner à pile (retombée Ink), animator, vue console,
choix à contenu inline imbriqués.
**Prochain chantier :** adaptateur Unity (asmdef `noEngineReferences` pour le core,
MonoBehaviour View + coroutine pulsant l'animator, mapping emit → UnityEvent,
ScriptedImporter `.acsdlg`). Le pattern : la View Unity est le miroir de
`ConsoleDialoguePresenter`.
**Plus tard (spec « hors périmètre ») :** `if/else`, variables (`set`), gardes sur choix
— les expressions justifieront alors un parser d'expressions dédié branché dans
`ApplyInstruction`/`ParseContent`. Lint statique des cycles de jumps à l'import.

## Conventions de code (préférences dev)

- Commentaires de code en **anglais**. Docs/spec en français.
- Préfixes : `l` pour les locales, `p` pour les paramètres, `_` pour les champs privés.
- Principes : KISS, DRY, SOLID. Composition over inheritance là où ça paie.
  Corriger vite toute suggestion sur-ingéniérée.
- Refactor et feature ne se mélangent JAMAIS dans la même étape : extraire, vérifier la
  non-régression sur `TestDialogue.acsdlg`, puis brancher le nouveau.
- Tester en console via stdin redirigé : la vue gère `Console.IsInputRedirected`
  (lignes vides = Advance, lignes à chiffre = SelectChoice).
- Eliott apprend le C# en profondeur sur cette codebase : quand il code lui-même,
  expliquer les pièges rencontrés (sémantique struct/class, chemins continue/return,
  invariants implicites) plutôt que de corriger silencieusement.
