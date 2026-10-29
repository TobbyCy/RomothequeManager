# UX-CONTRACT-02 — comportements des bibliothèques (DEV 2)

**Statut : proposition documentaire / PR Draft / lecture seule / aucune opération sur collection réelle.**
**Portée :** contrat de **présentation, navigation et qualification des preuves** pour les bibliothèques fictives `IDEAL`, `CHAOS` et `MYSTERY`, sans nouveau DTO, protocole, stockage, moteur ni conception parallèle.

**Sources déjà autorisées et prioritaires :** [Issue #3 — éléments non observés par plateforme](https://github.com/TobbyCy/RomothequeManager/issues/3), [#8 — santé/statistiques](https://github.com/TobbyCy/RomothequeManager/issues/8), [#11 — doublons et variantes](https://github.com/TobbyCy/RomothequeManager/issues/11), [#65 — NEXT-02](https://github.com/TobbyCy/RomothequeManager/issues/65), [contrat NEXT-02 P1](../architecture/next-02-inventory-contract.md), [projection bibliothèques historique](../architecture/contrat-projection-bibliotheques.md), [comparaison A/B existante](../architecture/contrat-comparaison-snapshots.md), [UX bibliothèques](04-bibliotheques.md), [UX diagnostics](06-diagnostics.md), [recette existante](criteres-de-reception.md), [oracles DEMO-01](../../contracts/demo-library-01/oracles.json), [générateur DEV 1](../../tools/demo-library/README.md).

## 1. Invariants de lecture et de langage

1. **Une observation de fichier n'est pas un jeu identifié** : `FileObservation` NEXT-02 reste une observation de `ROMS` avec un identifiant local au scan, un type prudent, une plateforme éventuellement `UNKNOWN`, la provenance et l'heure. `ConfirmedGameCount` reste `null` tant qu'aucune preuve d'identité n'est produite par un mécanisme autorisé. Le référentiel DEMO-01 (`demo01-NNN`) est un **oracle de fixture**, pas une identité découverte par le runtime.
2. **Une entrée ES-DE est une référence de catalogue** : `EsDeGame` permet d'afficher un titre, une plateforme, des médias référencés et un indicateur de disponibilité, **sans certifier que le fichier ROM est jouable**, ni garantir un rattachement d'une observation NEXT-02. Le mode démonstration, l'import ES-DE et l'inventaire autonome restent distincts ; une interface commune future peut afficher des sections voisines, mais **pas de jointure inférée** par titre/extension/nom de dossier.
3. **Similitude ≠ doublon confirmé** : nom, extension, taille, chemin, date et ressemblance de jaquettes n'autorisent que `POTENTIAL_DUPLICATE`, accompagné du motif et de la provenance. Un doublon exact n'est présenté comme prouvé **que si une preuve de contenu vérifiable existe dans le mécanisme effectivement utilisé**, avec son périmètre identifié ; aucun hash de ROM réelle n'est demandé par ce contrat. Ne jamais fusionner, masquer une variante ou proposer une suppression comme résultat de la similitude.
4. **Absence d'observation ≠ suppression** : même avec une portée `COMPLETE`, afficher au maximum `Non retrouvé dans la portée couverte à la date T`, jamais `Supprimé`. Un dossier `PARTIAL`, `FAILED`, `OFFLINE` ou `STALE` ne peut produire aucune « absence certaine » pour ses zones non couvertes. **Une capture complète n'est ni une preuve de présence actuelle ni une base de collection « idéale » externe.**
5. **Inconnu ≠ zéro** : garder `null`/`Inconnu` pour un dénominateur non démontré, une plateforme hors portée, un index inaccessible, des jeux non identifiés et des médias attendus non déclarés ; zéro n'est permis que pour un fait effectivement mesuré avec une portée suffisante. Ne jamais afficher un pourcentage de complétude sans numérateur, dénominateur et référence de couverture communs.
6. **Données fictives ≠ collection utilisateur** : affichage DEMO-01 clairement marqué `Données fictives`. Le générateur de démonstration et les scans synthétiques validés par CI n'autorisent **aucun** scan personnel, appel réseau, scraping, écriture, copie, déplacement ou effacement. Toute action de lecture future sur une vraie racine reste derrière son GO distinct.

## 2. Informations et indicateurs d'une bibliothèque

L'inspecteur de bibliothèque (ou son résumé sur Dashboard) **distingue les sources de vérité** sans réinterpréter les compteurs. Les éléments non disponibles portent explicitement `Non disponible` ou `Inconnu`.

| Zone | À afficher si preuve présente | Jamais déduire |
|---|---|---|
| En-tête | Nom, `libraryId` logique, origine `DEMONSTRATION / ES-DE / INVENTORY` selon le contexte réellement connu ; sélection courante | Identité d'un SSD par sa lettre Windows, mode import à partir d'un nom |
| Santé de la source | `ONLINE / OFFLINE / INACCESSIBLE / UNKNOWN` + heure de la dernière vérification si disponible | OFFLINE faute de ping ; « vide » lorsque l'index est inaccessible |
| Fraîcheur & génération | `FRESH / STALE / UNKNOWN`, date UTC/locale explicite, dernière génération complète vérifiée et dernière tentative séparément | « synchronisé maintenant » depuis un snapshot historique |
| Portée | Domaine `ROMS`, partitions/plateformes effectivement couvertes et `COMPLETE / PARTIAL / FAILED` (éventuellement `unobserved` au niveau partition), provenance de la preuve | couverture des médias ou du disque entier à partir d'une seule énumération ROMS |
| Fichiers observés | Nombre de `FileObservation` réellement collectées, classification prudente, `ObservedFileCount` ; total complet seulement si `TotalFileCount` connu | convertir 32 fichiers (playlist comprise) en 32 jeux |
| Jeux référencés | Nombre d'entrées ES-DE du snapshot importé, distinct des fichiers ; titres et plateformes de la référence | nombre de jeux identifiés depuis l'inventaire autonome seul |
| Plateformes | Plateformes **observées ou référencées** avec indication de source, y compris `UNKNOWN` ; couverture par partition | « console complète » par simple présence d'un dossier |
| Médias | Médias ES-DE référencés (image, thumbnail, marquee), état d'ouverture/présence réellement contrôlé, fallback et diagnostics ; les autres rôles restent dans manifest de fixture | `média manquant` parce qu'aucun XML n'a déclaré les médias attendus ; disponibilité d'un PNG d'après sa seule URL |
| Anomalies | diagnostics stables `code`/sévérité/portée/date/provenance, référencés vers le contexte et le dernier essai | effacer une anomalie parce qu'une source a disparu de l'écran |
| Doublons/variantes | groupes `potentiels` et motifs ; si une identité métier prouvée existe, région/langue/édition/révision/source sous leurs valeurs connues | exactitude, suppression ou consolidation automatique |

**Règle de compteurs :** trois chiffres voisins sont acceptables uniquement avec trois libellés différents : `Fichiers observés`, `Jeux référencés ES-DE`, `Jeux identifiés` (`Inconnu` si non prouvé). `Médias déclarés` et `Médias physiquement vérifiés` sont deux compteurs différents. Un en-tête de bibliothèque ne doit jamais les additionner. Le nombre de **plateformes de la vérité terrain DEMO** ne vaut pas inventaire de plateformes détectées.

**Présentation d'un élément non retrouvé (#3)** : afficher `non observé dans la portée entièrement couverte` si la preuve précise **bibliothèque + source + domaine + plateforme + génération + date** existe ; sinon `inconnu / hors couverture`. Le statut reste observationnel, pas une opération de suppression. Une comparaison A/B s'appuie exclusivement sur le comparateur/projection déjà validés et leurs classifications `NOT_OBSERVED_IN_B_COVERED_SCOPE`, `UNKNOWN`, `NOT_COMPARABLE` ; l'UI ne recalcule aucun diff.

## 3. États orthogonaux et réactions

| État ou événement | Comportement attendu | Interdit |
|---|---|---|
| `ONLINE + COMPLETE + FRESH` | Afficher l'inventaire de la portée prouvée et ses compteurs qualifiés, datés ; le reste `Inconnu` | présenter la collection réelle comme entièrement reconnue |
| `OFFLINE + index accessible` | Conserver la dernière projection complète consultable **datée**, signaler `Support déconnecté` et `Données historiques` | remise à zéro, suppression, lancement de scan ou test de fichier silencieux |
| `OFFLINE + index inaccessible` | Bibliothèque visible si son identité est déjà connue, détails/compteurs `Indisponible/Inconnu`, diagnostic neutre | afficher `0 jeux` ou effacer la bibliothèque |
| `PARTIAL` (limite, annulation, erreur) | Séparer `Dernière tentative partielle` du `Dernier complet`, afficher observations partielles et zones non couvertes, total `Inconnu` | remplacer `Current` complet par la tentative ; marques `Supprimé` |
| `FAILED` ou racine absente | Garder le dernier `Current` accessible et ses dates ; `LastAttempt` échouée avec diagnostic ; nouveau total `Inconnu` | transformer l'échec en résultat complet vide |
| `STALE` | Présenter les valeurs historiques datées avec leur source et la mention « ancien inventaire » ; proposer seulement une action explicite future de vérification | scan automatique, « à jour » ni absence physique récente |
| `UNKNOWN` disponibilité ou fraîcheur | Afficher `Non vérifié` ; demander une confirmation ou une action utilisateur si un futur flux le permet | supposer ONLINE/OFFLINE à partir du silence |
| Aucun `gamelist.xml` (`MYSTERY`) | `Aucune métadonnée ES-DE` ; écran d'observations NEXT-02 si une observation existe ; `Jeux identifiés : Inconnu` | convertir les 32 fichiers en 30 jeux ou inférer 0 jeu existant |
| XML malformé / média inaccessible | Message diagnostic sûr, conservation du dernier import valide s'il existe ; média de remplacement clairement étiqueté | effacer l'index ou prétendre qu'un placeholder est le média d'origine |
| Source qui réapparaît / lettre USB différente | garder `libraryId` logique ; réassociation **uniquement après vérification suffisante** et confirmation si ambiguë ; statut non présumé mis à jour | relier automatiquement à partir de la lettre ou du titre |

**Précision :** disponibilité (`OFFLINE`) et fraîcheur (`STALE`) ne sont pas des valeurs de `InventoryScanStatus`. Les états `COMPLETE/PARTIAL/FAILED` sont **ceux d'un scan et d'une portée**, non une étiquette globale de disponibilité. `Current` et `LastAttempt` décrivent des générations distinctes. L'index local peut être consultable malgré un support absent, **si un index existe effectivement** ; pas de persistance nouvelle à implémenter par ce document.

## 4. Navigation, sélection et contexte

1. **Choisir une bibliothèque** via la liste/cartes `Bibliothèques` ; l'inspecteur affiche explicitement nom, `libraryId`, statut, portée et provenance. Même si l'implémentation actuelle présélectionne le premier élément fictif, une sélection utilisateur explicite reste visible, clavier et souris. Aucune sélection ne déclenche un inventaire de disque réel.
2. **Changement de bibliothèque** : ne jamais réutiliser le jeu sélectionné, les IDs de snapshots, filtres ou comparaison A/B d'une autre bibliothèque. `LibraryViewState.SelectLibrary` existant invalide `CurrentComparison` ; afficher une sélection explicite sans confusion de contexte. Une bibliothèque vide/désactivée garde son existence si enregistrée.
3. **Navigation Dashboard → Bibliothèques → Collection → Fiche → retour** : conserver une provenance et un identifiant de bibliothèque cohérents ; les tuiles ne sont pas des commandes de scan. Recherche et pagination opèrent sur les données **du mode courant** : `ImportedCatalogueUiState` offre actuellement une pagination de 50, aucune jointure à un inventaire sans contrat ultérieur.
4. **Plateformes** : filtrer par plateforme déclarée/observée avec source et couverture ; `UNKNOWN` n'est pas automatiquement regroupé sous une console au vu de `.bin`. Les plateformes hors couverture restent consultables comme `Non évaluées` si déclarées par le registre.
5. **Médias/diagnostics** : ouvrir uniquement les médias validés par le résolveur existant ; placeholder descriptif si absent. Un clic diagnostic montre code, source, limite et génération, puis retour au même contexte ; ne jamais fabriquer un lien vers un fichier hors de la racine autorisée.
6. **Comparaison A/B** : sélectionner deux générations **de la même bibliothèque** et dans l'ordre chronologique requis ; une comparaison absente ou non comparable donne un refus explicite. Afficher les périmètres non comparables avant les catégories de résultat. Ne jamais utiliser `demoId` oracle comme jointure implicite d'une comparaison runtime.
7. **Actualisation** : `Actualiser l'affichage` depuis un index existant est distinct de `Inventorier en lecture seule`. L'inventaire, lorsqu'il sera autorisé dans le Produit, doit demander une racine locale et un déclenchement volontaire, rester annulable et single-flight ; aucun bouton factice ne prétend scanner aujourd'hui.

## 5. Trois bibliothèques de démonstration — séparation oracle / écran

Le [contrat DEMO-01](../../contracts/demo-library-01/oracles.json) décrit une **vérité terrain des fixtures**, et non des valeurs que chaque moteur doit inférer. Les nombres ci-dessous servent à tester les libellés et à repérer les mélanges de provenances. Les interfaces ne doivent afficher un nombre que si leur source correspondante est réellement chargée et consultable.

| Source fictive (oracle) | Fichiers `ROMS` dans un scan `COMPLETE` sur la fixture | Entrées `<game>` ES-DE si import XML réussi | PNG physiques planifiés dans la fixture | Jeux **identifiés** par NEXT-02 seul |
|---|---:|---:|---:|---|
| `IDEAL` | 32 | 30 | 180 | `Inconnu` (`null`) |
| `CHAOS` | 33 | 30 | 166 | `Inconnu` (`null`) |
| `MYSTERY` | 32 | 0 **entrée XML car aucun XML** | 0 | `Inconnu` (`null`) |

- `IDEAL` : 13 plateformes dans le référentiel ; le catalogue ES-DE fournit 30 **références**, et Metal Deer Salad (`demo01-011`) utilise 1 playlist + 2 membres. Les 32 observations ne créent pas de 32e jeu. Six rôles médias sont prévus pour les 30 titres, mais l'importateur ES-DE n'expose aujourd'hui que `image`, `thumbnail`, `marquee` : **90 références de ces trois rôles**, si chargées ; les 90 autres PNG restent manifest-only, pas 180 éléments prouvés à l'écran.
- `CHAOS` : 30 références ES-DE mais deux ROM principales absentes (`demo01-005` et `demo01-017`), 3 fichiers candidats à la duplication (`004`, `013`, `027`) et 14 PNG manquants. Un fichier en double **candidat** n'est pas un doublon exact reconnu ; deux références avec ROM indisponible restent listées, avec diagnostic, pas « 28 jeux ». Les compteurs médias visibles sont bornés aux rôles et validations que l'UI sait réellement lire.
- `MYSTERY` : pas de fichier XML, aucun média déclaré par ES-DE, 32 observations dont les membres de playlist ; les 30 `demoId` ne figurent **que dans l'oracle**. Écran de fichiers et plateformes observées ; titres, jaquettes attendues et nombre de jeux identifiés **inconnus**, sans `0 jeu` ni `30 jeux reconnus`.

**IMPORTANT :** les nombres sont ceux du générateur/contrat de référence et des tests synthétiques sur un jeu fictif ; ne pas présumer que la Pre-release historique du 23 octobre inclut DEMO-01, ni que NEXT-02 est raccordé à l'interface Windows. Toute recette d'affichage est une cible future tant que le flux n'est pas implémenté.

## 6. Frontières DEV 1 / DEV 3

**DEV 1 — preuves et fixtures** : fournit les 3 dossiers synthétiques isolés, leurs manifestes et SHA des **fichiers créés**, les 13 plateformes, portées et états (COMPLETE, PARTIAL/FAILED et disponibilité simulée si nécessaire), diagnostics contrôlés, fichiers/entries XML/PNG, cas de non-observation, doublons seulement candidats et absence d'identité NEXT-02. N'introduit **aucun** hash de ROM personnelle, API de scan silencieux ni nouvelle identité métier pour satisfaire une maquette.

**DEV 3 — présentation Avalonia** : consomme les modèles/projections **déjà existants** et affiche leurs limites : cartes, liste/inspecteur, sélection, sources, dates, mode ES-DE vs observations, compteur inconnu, média placeholder, diagnostics, état Offline/Stale et refus de comparaison. **Ne crée pas** un `GameId` à partir d'un chemin, ni un nouvel adaptateur obligatoire sans mandat. Si un champ requis ne peut pas encore être obtenu, marquer le contrôle `NOT_IMPLEMENTED` (pas `PASS`), et documenter la dépendance plutôt qu'inventer une valeur.

**Devis de portée :** cette PR documentaire n'autorise aucun développement, modification de la UI, nouveau composant ou Release. La [recette fonctionnelle liée](library-behaviors-02-recipe.md) définit le test futur, mais le verdict de cette PR ne constitue pas un PASS des tests GUI.
