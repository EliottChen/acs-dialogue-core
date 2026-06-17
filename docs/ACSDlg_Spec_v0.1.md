# ACSDlg — Spécification du format v0.1

*Achroma Station Dialogue System — format texte des fichiers `.acsdlg`*

Document de référence pour l'écriture des dialogues et l'implémentation du lexer/parser.
Cette version couvre le **socle linéaire et ramifié**. Conditions (`if`), variables et
commandes moteur viendront plus tard sans casser ces règles.

---

## 1. Principe général

Un fichier `.acsdlg` décrit un **graphe de dialogue** : des *nodes* nommés qui contiennent
une séquence de *lignes*. Le lecteur avance ligne par ligne dans un node, et change de node
uniquement via un saut (`jump`) ou un choix.

Règle d'or du format : **rien dans les espaces ni les sauts de ligne n'a de sens.**
Toute la structure est portée par des mots-clés et des caractères explicites
(`node`, `jump`, `{ }`, `:`, `;`). On peut indenter, aligner ou aérer comme on veut,
le parser s'en moque.

Le format privilégie la **verbosité explicite** : mots-clés lisibles plutôt que symboles,
pour faciliter la lecture par un designer et la coloration syntaxique dans un plugin IDE.

---

## 2. Les nodes

Un node est un conteneur nommé délimité par des accolades.

```
node PourquoiSeparateur {
    ... lignes ...
}
```

- `node` est un mot-clé.
- Le nom du node est un **identifiant** (voir §7) : lettres, chiffres, `_`, **sans espace**.
- Les accolades `{ }` délimitent le contenu. **Pas de `;` après la `}` fermante.**

Le point d'entrée du dialogue est déclaré en tête de fichier :

```
#start Debut
```

---

## 3. Les lignes (répliques et narration)

Chaque ligne suit le schéma :

```
<speaker> : <texte> ;
```

- Le **premier `:` de la ligne** sépare le speaker du texte. C'est toujours le premier,
  sans exception : tout `:` qui suit dans le texte est du texte normal.
- La ligne **se termine par `;`**.
- Le nom du speaker est *trimé* (les espaces autour sont ignorés) : `Eliott :`, `Eliott:`
  et `  Eliott  :` donnent tous le speaker `Eliott`.

### Narration anonyme

Un speaker **vide** = narration sans personnage attribué (`Speaker = null` côté code) :

```
: Ceci est de la narration ;
```

> Un personnage *nommé* « Narrateur » (`Narrateur : ...;`) est **distinct** de la narration
> anonyme (`: ...;`). Les deux coexistent volontairement et s'affichent différemment côté
> moteur (deux types de bulles).

### Exemples valides

```
Eliott : Bonjour, je suis le développeur du système ;
Narrateur : Pour que ça reste simple : il a suffi d'un deux-points en tête ;
: Le rideau se lève lentement ;
```

Dans la deuxième ligne, le `:` au milieu (« simple : il a suffi ») est du texte :
seul le premier compte.

---

## 4. Terminateur `;` et échappement

- **Chaque ligne et chaque instruction simple finit par `;`** (répliques, jumps, end).
- **Les blocs ne prennent pas de `;`** : pas de `;` après `}` (convention C#).
- Pour écrire un point-virgule **dans le texte**, l'échapper : `\;`.

```
Eliott : Il dit ceci \; puis cela \; et enfin voilà ;
```

> Un `;` non échappé dans le texte coupe la réplique. C'est le seul endroit du format où
> un writer peut casser une réplique silencieusement. En pratique, on a rarement besoin
> d'un point-virgule dans un dialogue parlé, mais le `\;` reste utile pour dire
> **explicitement** « ce `;` n'est pas une fin de ligne ».

---

## 5. Navigation entre nodes

La navigation utilise des **mots-clés verbeux**, pas des symboles.

### Saut inconditionnel

```
jump PourquoiSeparateur ;
```

Passe immédiatement au node cible. Pas de choix montré au joueur.

> Pas de collision possible avec un personnage qui s'appellerait « jump » : une réplique
> a toujours un `:` (`jump : ...;`), une instruction de saut n'en a pas (`jump Cible;`).
> Le `:` désambiguïse mécaniquement.

### Fin de dialogue

```
end ;
```

Termine le dialogue. (Un node qui n'a ni `jump`, ni choix, ni `end` à la fin se termine
aussi — mais `end` rend l'intention explicite.)

### Choix (branchement)

Un choix présente une option sélectionnable qui mène à un node :

```
choice "Le suivre" jump Suivre ;
choice "Rester ici" jump Rester ;
```

- Le libellé est entre guillemets.
- `jump` indique le node cible (même mot-clé que le saut inconditionnel).
- Plusieurs `choice` consécutifs forment le menu présenté au joueur.

### Choix à contenu inline (extension v0.2)

Au lieu d'un `jump`, un choix peut porter un **bloc de contenu** entre accolades. Le bloc
contient les mêmes contenus qu'un node : répliques, jumps, ends — et d'autres choix
(l'imbrication est autorisée, sans limite de profondeur).

```
choice "Je ne sais pas quoi dire"
{
    Eliott : Tu n'as pas besoin de savoir quoi dire ;
}
choice "Ne rien dire." jump Conclusion ;
```

Un choix a donc exactement **une** des deux formes : `choice "Libellé" jump Cible ;`
(forme saut) ou `choice "Libellé" { ... }` (forme bloc). Comme pour les nodes, **pas de
`;` après la `}` fermante**.

**Règle de retombée (à la Ink).** Quand un bloc inline se termine sans `jump` ni `end`,
la lecture **retombe sur le contenu qui suit le menu de choix** dans le conteneur parent
(node ou bloc englobant). La retombée est récursive : un bloc imbriqué retombe sur la
suite de son bloc parent, qui peut lui-même retomber, jusqu'au node. Conséquence : du
contenu placé après un menu de choix n'est plus du code mort — c'est le point de
convergence naturel des branches qui ne sautent pas ailleurs.

```
node Exemple {
    Eliott : Alors ? ;
    choice "Option A" { Eliott : Va pour A. ; }     // retombe ↓
    choice "Option B" { Eliott : Va pour B. ; }     // retombe ↓
    Eliott : Dans tous les cas, on continue ici. ;  // point de convergence
    jump Suite ;
}
```

Un `jump` ou un `end` **dans** le bloc court-circuite la retombée, comme partout ailleurs.
Les cibles des jumps internes aux blocs sont validées au parsing comme les autres.

---

## 6. Markup inline (effets de texte)

À l'intérieur du texte d'une réplique, des balises entre crochets pilotent l'affichage.
Elles sont parsées séparément et **retirées du texte affiché** ; le moteur les interprète.

### Modèle « à état », sans balise fermante

Le markup fonctionne comme un **curseur** qui lit le texte de gauche à droite. Une balise
n'entoure rien : elle agit **au moment où le curseur l'atteint**. Il n'existe donc
**aucune balise fermante** (`[/speed]` n'existe pas).

On distingue deux familles :

**Balises d'état** — modifient le curseur durablement, jusqu'à la prochaine balise qui
change le même réglage :

| Balise          | Effet                                                        |
|-----------------|-------------------------------------------------------------|
| `[speed:0.5]`   | Vitesse d'écriture à 0,5× à partir d'ici (ralentit)         |
| `[speed:2.0]`   | Vitesse à 2× à partir d'ici (accélère)                      |
| `[tp]`          | Vitesse instantanée à partir d'ici (le texte « téléporte ») |

**Balises ponctuelles** — déclenchent un évènement à l'instant atteint, sans rien changer
après :

| Balise          | Effet                                                        |
|-----------------|-------------------------------------------------------------|
| `[p:1.0]`       | Pause de 1,0 s, puis reprise au rythme courant              |
| `[emit:Shake]`  | Envoie le signal `Shake` au moteur (string **sans espace**) |

### Écriture souple des balises

Trois tolérances facilitent l'écriture, toutes résolues à la **lecture** (le parser
normalise avant d'interpréter) :

**1. Casse insensible.** Le nom de la balise est ramené en minuscules avant lookup.
`[SPEED:2]`, `[Speed:2]`, `[speed:2]`, `[sPeEd:2]` sont tous identiques.

**2. Nom complet ou alias court.** Chaque balise a un nom complet et un (ou plusieurs)
alias, déclarés dans une **table d'alias fixe** (voir ci-dessous). On peut écrire l'un
ou l'autre.

| Effet  | Nom complet | Alias | Exemples équivalents          |
|--------|-------------|-------|-------------------------------|
| Pause  | `pause`     | `p`   | `[pause:1.0]` = `[p:1.0]`     |
| Speed  | `speed`     | `s`   | `[speed:2.0]` = `[s:2.0]`     |
| Téléport (instantané) | `tp` | — | `[tp]`                     |
| Emit   | `emit`      | —     | `[emit:Shake]`                |

> **Les alias sont une table explicite, pas une règle « première lettre ».** Le parser
> ne *devine* jamais qu'une lettre seule correspond à une balise : il consulte la table.
> Raison : une initiale n'est unique que tant qu'aucune autre balise ne la partage. Le jour
> où une balise « Shake » ou « Sound » est ajoutée, `s` reste *explicitement* lié à Speed,
> et la nouvelle balise reçoit son propre alias choisi à la main (`sh`, `snd`…). Aucune
> collision rétroactive, aucune ambiguïté silencieuse.

**3. Virgule décimale optionnelle.** Pour les valeurs numériques, le `.0` est facultatif :
`[p:1]` = `[p:1.0]` (pause d'une seconde), `[s:2]` = `[s:2.0]`.

### Principe directionnel : le curseur ne revient jamais en arrière

Une balise n'agit **que sur ce qui la suit**, jamais sur ce qui la précède. Il n'y a donc
pas de « fin d'effet » : pour arrêter un effet, on ouvre l'état suivant. Concrètement,
pour qu'un seul mot s'affiche instantanément puis revenir au rythme normal, on **ouvre**
l'instantané avant le mot et on **rétablit** le rythme après :

```
Eliott : Je ne pense pas que... [tp] Oula ! [speed:1.0] Qu'est-ce que c'était ?! ;
```

- « Je ne pense pas que... » défile au rythme normal ;
- `[tp]` → à partir d'ici, instantané : « Oula ! » apparaît d'un coup ;
- `[speed:1.0]` → rétablit le rythme normal pour « Qu'est-ce que c'était ?! ».

> On n'« entoure » pas « Oula ! » avec une ouvrante et une fermante : on bascule en
> instantané *avant*, puis on rebascule en normal *après*. Le writer apprend une seule
> règle : **un effet dure jusqu'à ce qu'une autre balise le change.**

### Exemple combiné

```
Eliott : Je ne pense pas que... [p:0.8] Oula ! [tp][emit:Shake] Qu'est-ce que c'était ?! [speed:2.0] Filons d'ici ;
```

Lecture du curseur :
- texte au rythme normal jusqu'à `[p:0.8]` → pause 0,8 s, puis « Oula ! » au rythme normal ;
- `[tp]` → ce qui suit s'affiche instantanément ET `[emit:Shake]` signale `Shake` au moteur ;
- `[speed:2.0]` → repasse en mode animé mais 2× plus vite pour « Filons d'ici ».

> Le `:` *à l'intérieur* d'un crochet (`[p:0.8]`, `[emit:Shake]`) ne pose aucun problème :
> il est borné par les crochets, c'est un contexte isolé du parsing de ligne.

> Syntaxe unifiée : toutes les balises sont de la forme `[clé]` (sans valeur) ou
> `[clé:valeur]` (avec valeur). Pas de `=`, pas de fermeture, pas de balise « neutre ».

---

## 7. Identifiants

Noms de nodes et cibles de saut/choix.

- Composés de **lettres, chiffres et `_`**, commençant par une lettre.
- **Pas d'espace** : `PourquoiSeparateur` ou `pourquoi_separateur`, jamais `Pourquoi Separateur`.

---

## 8. Commentaires

```
// Commentaire sur une ligne — ignoré par le parser.
```

---

## 9. Récapitulatif des mots-clés et caractères réservés

| Token             | Rôle                                      | Pour l'écrire en texte |
|-------------------|-------------------------------------------|------------------------|
| `node`            | Déclare un node                           | (mot-clé)              |
| `jump`            | Saut vers un node / cible de choix        | (mot-clé)              |
| `choice`          | Option de branchement                     | (mot-clé)              |
| `end`             | Fin explicite du dialogue                 | (mot-clé)              |
| `:`               | Sépare speaker et texte (1er de la ligne) | libre après le 1er     |
| `;`               | Termine une ligne / instruction           | `\;`                   |
| `{` `}`           | Délimitent le contenu d'un node           | —                      |
| `[` `]`           | Markup inline                             | —                      |
| `//`              | Commentaire                               | —                      |
| `#`               | Directive (`#start`)                      | —                      |

---

## 10. Exemple complet

```
#start Debut

node Debut {
    : Achroma Station — première ébauche du système de dialogue ;
    Eliott : Bonjour, je suis le développeur derrière ce système ;
    Narrateur : Pour que ça reste simple : chaque ligne commence par « qui : » et finit par « ; » ;
    jump PourquoiSeparateur ;
}

node PourquoiSeparateur {
    Eliott : Le deux-points initial [p:0.5] délimite qui parle ;
    Eliott : Pour la narration, on laisse le speaker vide ;
    : Comme ceci ;
    end ;
}
```

---

## Hors périmètre v0.1 (prévu, non implémenté)

- `if (condition) { } else { }` — branchement conditionnel.
- Variables de dialogue mutables (`set`) et lecture de variables moteur (int uniquement).
- Commandes moteur additionnelles au-delà de `[emit:…]`.
- Garde sur un choix (`choice "…" if (…) jump …`).

Chacun s'ajoutera comme un **nouveau type de contenu** sans modifier les règles ci-dessus.
