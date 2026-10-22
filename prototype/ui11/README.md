# UI #11 — Prototype interactif hors ligne

**Statut : prototype de parcours, pas application Windows finale.** Branche / PR : [Produit #28](https://github.com/TobbyCy/RomothequeManager/pull/28). Aucun framework imposé ; HTML, CSS et JavaScript natifs, sans CDN, package, accès réseau ou dépendance externe.

## Démarrage

Ouvrir **`index.html`** dans un navigateur moderne (double-clic possible, URL `file://`). Les fichiers `fixtures.js`, `app.js` et `styles.css` doivent rester dans le même dossier. Aucun serveur n'est nécessaire. Pour exécuter les assertions facultatives en environnement de développement disposant de Node.js : `node prototype/ui11/smoke.cjs` depuis la racine du dépôt. **Node.js n'est pas requis pour consulter le prototype.**

## Écrans manipulables

1. **Dashboard** : nombre de bibliothèques, plateformes connues (borne minimale si une source n'a pas de plateformes déclarées), états OFFLINE/STALE et inconnus, derniers snapshots, navigation.
2. **Bibliothèques** : quatre bibliothèques fictives (ONLINE, OFFLINE, INACCESSIBLE, UNKNOWN), plateformes, fraîcheur, dernier snapshot et dernier complet vérifié, historique si index accessible.
3. **Comparaison** : sélectionner bibliothèque, génération A/B, lancer comparaison en mémoire, six classifications, filtres par classification, plateforme et recherche, inspecteur d'unité. Les couvertures A/B restent visibles. Un `entries=[]` sous couverture insuffisante n'est jamais affiché comme « aucune différence ».
4. **Collection** : grille/liste de titres fictifs issus des derniers snapshots complets vérifiés, recherche, filtre bibliothèque, inspecteur sans médias réels ni édition.

**Clavier** : tabulation/focus visible, Entrée ou Espace sur les lignes de comparaison. Interface responsive. Le libellé « Non retrouvé dans B » signifie une absence d'observation datée, **jamais une suppression physique**.

## Données / intégration Dev 1

Les fixtures sont définies dans `fixtures.js`. Le navigateur ne peut pas exécuter PowerShell : `app.js` expose `window.RM_COMPARE(before, after)`, un **miroir JS strictement synthétique** du contrat de la PR Produit #26. Ce miroir n'est pas un nouveau moteur canonique ; **le module PowerShell et l'adaptateur de Dev 1 restent la référence**. La projection attend `libraryId, name, platforms, availability, freshness, observedAtUtc, indexAvailable, lastVerifiedCompleteRef, snapshots[]`. Chaque snapshot respecte la forme v1 : `schemaVersion, libraryId, generationId, capturedAtUtc, scanStatus, sourceAvailability, freshness, coverage[], units[]`. Les unités portent les IDs persistants `romUnitId/gameId/variantId/console`, un `observationDigest` optionnel et un `title` **strictement UI**. La couverture doit être vérifiée pour **A et B**, sans extrapoler console à plusieurs racines ou domaines ROMS/MEDIAS.

**Limites connues** : absence de persistance, de registre réel, de scanner, de lecteur de snapshots sur disque, de médias autorisés, de fiches enrichies et d'exécutable Windows. Aucune association USB, conversion, copie, suppression, export, appel réseau, lecture ROM/SSD/StateRoot ni Release. Les disponibilités sont des **fixtures de démonstration**, non une détection physique.

## Tests et preuves

Le fichier `smoke.cjs` contient des assertions sur les six classes, le refus des identités/entrées invalides, OFFLINE/STALE, les zones partial/failed vides, et la navigation simulée. Un contrôle en V8 avec DOM simulé a été réalisé lors de la PR (voir commentaire de réception/checkpoint) ; il **ne constitue pas un test navigateur graphique ni un PASS CI Windows**. Les tests Node.js et visuels doivent être exécutés indépendamment et leurs résultats datés.

### À raccorder avant l'application

- Adaptateur DEV1 de lecture synthétique et mapping des preuves `coverage` / `lastVerifiedCompleteRef`.
- Contrôle de parité entre `RM_COMPARE` et le module PowerShell 5.1 sur mêmes fixtures.
- Choix technique Windows et packaging portable **sur mandat ultérieur**, tests d'accessibilité et navigateur, puis vérification externe avant tout ZIP/Release.


## Sprint #13 — mode projection du noyau

Le fichier `projection-data.js` vaut `null` par défaut : **mode démonstration** (fixtures + comparateur JS miroir). Pour **mode projection**, y placer un objet littéral `{registry: <JSON New-RmLibraryProjection>, comparisons: [<JSON Get-RmLibraryComparisonView>]}` obtenu **hors du navigateur**, exclusivement sur des fixtures synthétiques. `projection-adapter.js` valide le schéma `0.1.0-synthetic`, `readOnly=true`, les six comptes, leur dénominateur et les preuves de couverture ; `app.js` affiche les DTO précalculés et **n'appelle pas RM_COMPARE** dans ce mode. Si la paire A/B n'a pas de résultat précalculé, l'interface refuse sans recalculer. Aucune requête réseau ni lecture disque depuis le navigateur.

Les générations du registre sont des **métadonnées**, sans `units` : la Collection affiche donc un état vide non probant en mode projection jusqu'à fourniture d'un DTO catalogue distinct. `lastVerifiedCompleteRef` est un **snapshotId** (pas `generationId`). Le registre de Dev 1 exige des plateformes déclarées ; les valeurs inconnues doivent être représentées par un état de source inconnu, pas un faux zéro.

**Tests #13 :** évaluation V8 des fichiers réels (mode démonstration et registre projeté vide) effectuée ; tests navigateur, Node.js et PowerShell restent à confirmer. Le test de parité runtime n'est pas revendiqué.


## Sprint #15 — Collection projetée et fiche de jeu (candidat)

La PR #28 contient maintenant `catalogue-adapter.js`, `projection-sample.js` et `projection-smoke.cjs`. Le DTO **candidat UI**, non normatif pour Dev 1, est `{schemaVersion:'0.1.0-ui-catalogue-candidate',readOnly:true,games:[{gameId,platform,title,status,provenance,variants,observations}]}`. `status` vaut `KNOWN|INCOMPLETE|INACCESSIBLE`. `provenance` contient `libraryId,snapshotId,observedAtUtc,source`; les observations ROM contiennent leurs propres `romUnitId,variantId,libraryId,snapshotId`. Les jeux sont distincts des unités observées. Les références vers un index inaccessible sont refusées. Le DTO est **absent par défaut** et l'UI affiche « catalogue non disponible » plutôt qu'un faux zéro.

Pour essayer **sans PowerShell**, remplacer dans `index.html` uniquement la source du script `projection-data.js` par `projection-sample.js` (fixture entièrement inventée, **pas** un export Dev 1). Le mode projection expose alors Collection, filtres bibliothèque/plateforme/recherche, fiche, variantes, provenance, observations et navigation vers la comparaison précalculée. L'ouverture/fermeture d'une fiche conserve les filtres. L'adaptateur registre accepte `countKnown=false` et `observedUnitCount=null` pour un index inaccessible ; aucune complétude n'est déduite d'un nombre nul. Pour utiliser un export Dev 1, conserver le script `projection-data.js` et remplacer son `null` par le JSON synthétique généré par `tests/Export-SyntheticUiProjection.ps1` après exécution externe autorisée. Ce JSON **n'inclut pas de catalogue**, qui doit rester une entrée candidate séparée.

Exécution optionnelle des tests avec Node.js : `node prototype/ui11/smoke.cjs` puis `node prototype/ui11/projection-smoke.cjs`. **Aucune de ces deux commandes n'a été exécutée dans ce contexte** (pas de checkout privé disponible au runtime Node). Ne pas les confondre avec les 9 contrôles **V8/DOM simulé PASS** de Sprint #15, ni avec des tests navigateur ou PowerShell 5.1. Les sorties de `projection-sample.js` sont fabriquées à la main ; la **parité runtime** avec Dev 1 est **NOT_EXECUTED**.


## Sprint #17 — démonstration intégrée hors ligne

**Entrée directe** : ouvrir `demo-integree.html` (dans `prototype/ui11/`) par double-clic, URL `file://`. Ne pas modifier `index.html` pour cette démonstration : il reste le mode historique avec `projection-data.js=null` (démo JS). `demo-integree.html` charge `projection-integrated.js` au lieu de `projection-data.js`, puis les mêmes adaptateurs, vues et styles. Pas de CDN, `fetch()`, serveur, stockage ou lecture ROM.

**Provenance** : `projection-integrated.js` est une **fixture candidate écrite manuellement par Dev 2**. Elle **ne provient pas** d'un export PowerShell 5.1 réellement exécuté et ne démontre **pas** la parité runtime avec Dev 1. Le contrat de Dev 1 reste `tests/Export-SyntheticUiProjection.ps1` → `{registry,comparisons}`, sans `catalogue` normatif. L'UI ne calcule jamais les six classes en mode projection : elle exige la paire précalculée. `RM_COMPARE` demeure uniquement pour le mode démonstration historique.

**Parcours reproductible** : Dashboard → « Ouvrir les bibliothèques » → choisir **Bibliothèque multi-états** → « Comparer → » sur `mixed-gen-b` → « Lancer la comparaison » (6 unités / 6 classes / couverture GBA complète, PS1 partielle) → Collection → sélectionner **Mystère synthétique** → voir la provenance, la variante, l'observation ROM → « Voir le snapshot » → Comparaison. Le bouton « ← Retour » restitue la page précédente ; les filtres de Collection persistent quand une fiche est ouverte puis fermée. Autres cas : **Bibliothèque synthétique** OFFLINE/STALE avec historique utilisable ; **Source non consultable** sans index ni catalogue inventé ; **Zéro non probant** avec zéro unité mais couverture partielle et avertissement explicite.

**Sémantique** : six compteurs distincts, dénominateur affiché, limitations et périmètres A/B. `NOT_OBSERVED_IN_B_COVERED_SCOPE` n'est pas une suppression. Les snapshots et générations sont deux identités distinctes. `countKnown=false` impose `observedUnitCount=null` ; jamais zéro pour un index inaccessible. Les métadonnées de jeux sont indépendantes des observations des unités ROM. La validation rejette identités en doublon, partitions non déclarées ou manquantes pour une couverture dite complète, résultats A/B incohérents avec le registre, compteurs altérés et paires absentes.

**Commandes de recette (sur un checkout autorisé)** :

```text
node prototype/ui11/smoke.cjs
node prototype/ui11/projection-smoke.cjs
node prototype/ui11/integrated-smoke.cjs
```

**Preuves de cette livraison** : contrôle V8 JavaScript avec DOM simulé, chargement des cinq scripts et parcours Dashboard → Bibliothèques → Snapshot → Comparaison → Collection → Fiche → Retour, **32/32 contrôles PASS** sur le dernier lot exécuté (dont 5 chargements/syntaxe, 3 comparaisons, six classifications, six refus ciblés et le parcours complet). Cela n'est **pas** une exécution de `integrated-smoke.cjs` sous Node. Le conteneur de travail dispose de Node.js 22 et de Chromium mais **pas d'un checkout privé autorisé**, donc les trois scripts Node, l'ouverture `file://` et la recette navigateur/clavier/responsive sont **NOT_EXECUTED**. Aucun résultat CI ou PS5.1 revendiqué. Les vecteurs Dev 1 sont écrits dans le dépôt mais aucun export PS5.1 vérifié n'a été fourni pour cette parité.

**Préparation application Windows portable** : (1) exécution synthétique PS5.1 de l'export Dev 1 et injection du JSON pour parité, (2) recette Node + navigateur réel, clavier et responsive sur le checkout, (3) contrat producteur Catalogue arbitré avec Dev 1, (4) mandat distinct pour packaging autonome Windows. Aucune fusion, Release ou usage de ROM réelles autorisés.

### Réception Dev 1 — nouveau HEAD observé pendant Sprint #17

Après vérification initiale de `19be89fa`, la PR #27 a avancé à `7aa593f71884bcf519692d8863847fb8a2f3ecbf`. **CHANGES_REQUIRED sur ce nouveau HEAD** : `tests/Export-SyntheticUiProjection.ps1` contient une chaîne `-cnotmatch 'Z` tronquée et deux blocs exporteurs superposés (anciens contrôles 6/5 et nouveaux 7/6/7). Revue ciblée publiée dans [PR #27](https://github.com/TobbyCy/RomothequeManager/pull/27). Ce défaut bloque la preuve d'un export PowerShell utilisable, mais ne bloque pas la fixture UI candidate autonome. Dev 2 n'a pas modifié la branche Dev 1.

**Addendum compteurs #17** : la fixture « Source non consultable » comporte deux générations historiques métadonnées, chacune avec `countKnown=false` et `observedUnitCount=null` ; les cartes Bibliothèques affichent « Inconnu » plutôt que zéro. La fixture « Zéro non probant » affiche le compteur observé 0 mais conserve sa couverture partielle et son avertissement. Contrôle V8 supplémentaire effectué sur ces deux états ; aucune exécution PS5.1 ni navigateur.


## S4 — Matrice de recette navigateur Windows (à exécuter, aucun PASS anticipé)

Préconditions : checkout privé de la PR #28 au SHA indiqué dans le checkpoint, navigateur Windows lancé localement en `file://`, **uniquement** `demo-integree.html` et les fixtures candidates intégrées. Aucun serveur, ROM, disque externe, StateRoot ou accès réseau. Conserver version du navigateur, OS, résolution, SHA, capture et résultat par ligne. L'exécution Node avec DOM simulé est distincte de cette recette.

| ID | Parcours / action | Attendu vérifiable | État |
|---|---|---|---|
| B01 | Ouvrir `demo-integree.html` hors ligne | Dashboard affiché, aucun chargement externe | NOT_EXECUTED |
| B02 | Dashboard → Bibliothèques → multi-états | ONLINE et FRESH distincts ; dates et snapshots lisibles | NOT_EXECUTED |
| B03 | Bibliothèque OFFLINE/STALE → historique | Historique consultable malgré indisponibilité actuelle | NOT_EXECUTED |
| B04 | Index INACCESSIBLE → comparaison | Refus explicite ; aucun jeu ni compteur zéro inventé | NOT_EXECUTED |
| B05 | UNKNOWN et zéro observé non probant | « Inconnu » ≠ « 0 » ; pas « aucune différence » sans preuve | NOT_EXECUTED |
| B06 | Sélection A/B → comparer | Six classes, dénominateur, limites et preuves affichés | NOT_EXECUTED |
| B07 | Modifier A/B pour paire absente | Refus sans recalcul par comparateur JS | NOT_EXECUTED |
| B08 | Collection → recherche, filtres bibliothèque/plateforme → fiche → retour | Filtres et sélection conservés, provenance visible | NOT_EXECUTED |
| B09 | Fiche → observation ROM → snapshot | Jeu distinct de l'unité ROM ; navigation cohérente | NOT_EXECUTED |
| B10 | Tab / Shift+Tab / Entrée / Espace | Ordre logique, focus visible, sélecteurs et filtres opérables | NOT_EXECUTED |
| B11 | Écran 320, 375, 768 et 1280 px | Aucun contrôle essentiel inaccessible, pas de débordement bloquant | NOT_EXECUTED |
| B12 | Zoom 200 % et contraste | Libellés de statuts lisibles sans dépendre uniquement des couleurs | NOT_EXECUTED |
| B13 | Vue sans résultats / historique indisponible | Messages non ambigus et sans suppression physique supposée | NOT_EXECUTED |
| B14 | Charger projection de schéma incompatible | Rejet contrôlé, aucune bascule silencieuse vers la démo | NOT_EXECUTED |

**Trois suites distinctes** : `node prototype/ui11/smoke.cjs` (miroir JS de démonstration), `node prototype/ui11/projection-smoke.cjs` (fixture candidate simple), `node prototype/ui11/integrated-smoke.cjs` (fixture candidate intégrée, canaris S2/S4). Leurs DOM sont simulés et ne valident ni accessibilité réelle, ni responsive, ni runtime PowerShell. Les 7 bibliothèques / 6 comparaisons / 7 vecteurs de Dev 1 nécessitent un export PowerShell 5.1 exécuté avant toute revendication de parité. Les tests S4 ne modifient aucun moteur métier.

**Limite de confiance** : la projection UI compare le DTO de comparaison au registre déjà validé. Si un acteur falsifie simultanément **le registre lui-même** et le DTO avec des preuves concordantes mais mensongères, l'UI ne peut pas authentifier une observation physique ; seule la production contrôlée par le noyau et la recette de parité peuvent établir la provenance. Aucun contenu `COMPLETE` ne constitue une preuve de présence physique de ROM.


## T3-B — Collection unifiée et contrat catalogue T1 candidat (22.10.2026)

**Entrée unique :** ouvrir `index.html` et choisir **Collection** dans la navigation. `t2-catalogue.html` est une page de transition historique et ne contient plus de second catalogue actif. `t2-catalogue-model.js` est un **consommateur strict** du contrat candidat de Dev 1 `0.1.0-ui-catalogue-t1-candidate` (PR Produit #29) ; il n'exécute ni PowerShell, ni comparaison, ni scan, ni requête réseau.

- **Mode démonstration** : `projection-data.js=null`, `t2-catalogue-fixture.js` fournit un DTO T1 **écrit manuellement** et clairement fictif. Ses jeux/observations ne sont **pas** un export PowerShell. Bibliothèques/snapshots issus de `fixtures.js`. Les observations multidisques sont testées sur un DTO muté isolé, sans inventer un second disque dans le snapshot de démonstration.
- **Mode projection** : fournir hors navigateur `RM_PROJECTION_DATA={registry,comparisons,catalogue}`. `catalogue` doit être exactement la sortie T1 `New-RmCatalogueProjection` sérialisée, avec ses `libraries`, `games`, `diagnostics`, `limitations`, `countKnown` et `gameCount`. **Ne jamais** utiliser la fixture démo comme secours si `catalogue` manque ou est invalide. Le registre des bibliothèques doit être validé par `RM_PROJECTION.registry()`. Le catalogue est refusé si la provenance ne correspond pas aux snapshots et générations du registre, si les états contredisent la bibliothèque, si les IDs sont dupliqués, si la couverture ou le total sont falsifiés.
- **Identité du jeu** : `[libraryId,platform,gameId]` ; **unité ROM** : `romUnitId`, `variantId`, `discNumber` optionnel ; aucun rapprochement inter-bibliothèques ni déduction de titre. Les fiches présentent qualité, disponibilité actuelle, fraîcheur, couverture, provenance datée, snapshot/génération et unités séparées. Un titre `null` reste inconnu ; un titre contradictoire reste non établi.
- **Compteurs** : `countKnown=false` impose `gameCount=null` ; la liste filtrée compte seulement les **fiches observées**, pas les jeux physiques, ni un total global. Index inaccessible : aucun jeu extrapolé. Une couverture partielle n'implique ni suppression ni zéro jeu.
- **Recherche/tri** : recherche titre/ID/plateforme/variantes ; filtres bibliothèque, plateforme, qualité, couverture et tri titre/plateforme/qualité/dernière observation. Filtres conservés pendant l'ouverture et fermeture d'une fiche ; lien vers le snapshot associé.

**Tests préparatoires :** `node prototype/ui11/t2-catalogue.test.cjs` (19 cas positifs/négatifs, contrat T1). Recette navigateur : ouvrir `index.html` hors ligne, Dashboard → Bibliothèques → snapshot → Comparaison → Collection → fiche → snapshot, vérifier les filtres et retour, tester 320/375/768/1280 px, clavier Tab/Entrée/Espace, source OFFLINE/STALE, titre nul, contradiction, couverture incomplète, DTO invalide en mode projection. Consigner SHA, OS, navigateur, stdout et captures. **V8 simulé ≠ Node.js ≠ navigateur ≠ PS5.1**. Les résultats runtime PS5.1, navigateur et parité ne sont pas disponibles à ce stade.

**Limites** : le DTO T1 est **candidat**, pas normatif. Le producteur T1 ne fournit aucune localisation physique par unité, seulement une provenance logique ; ne pas fabriquer `sourceKey/rootKey`. Les tests de parité nécessitent un JSON réellement exporté par PS5.1 et un registre synthétique de même provenance, avant toute réception d'intégration.
