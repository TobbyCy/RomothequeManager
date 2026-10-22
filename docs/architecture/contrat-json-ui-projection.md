# Échange noyau PowerShell → prototype UI #28 — v0.1.0-synthetic

**Producteur :** `tests/Export-SyntheticUiProjection.ps1`, synthétique uniquement, sans accès disque de collections, lecture des seules fixtures du dépôt. **Sortie** : JSON objet `{registry,comparisons}` compatible avec le point d'injection `window.RM_PROJECTION_DATA` de `prototype/ui11/projection-data.js` (#28). Un second fichier JS optionnel peut être généré par `-OutputJavaScript` : `window.RM_PROJECTION_DATA=<JSON>;`. Les sorties de référence ne sont **pas déclarées exécutées** avant exécution PS5.1 autorisée.

## Champs normatifs

- `registry.schemaVersion='0.1.0-synthetic'`, `readOnly=true`, `libraries[]`. Chaque bibliothèque : `libraryId`, `name`, `platforms[]`, `sources[]` (sourceKey/rootKey/domains), `currentAvailability`, `availabilityObservedAtUtc`, `freshness`, `indexAccessible`, `lastVerifiedCompleteRef` (**snapshotId**), `lastObservedAtUtc`, `generations[]`.
- Chaque génération : `snapshotId` **distinct de** `generationId`, `scanId`, `capturedAtUtc` UTC, `scanStatus`, `scopeIsExhaustive`, `coverage[]` (console/status/reason), `coverageProof[]` (console/sourceKey/rootKey/domain/status), `countKnown`, `observedUnitCount` (null si index inaccessible).
- `comparisons[]` : `schemaVersion`, `readOnly`, `library`, `before`, `after`, `scopeCompared[]`, `scopeNotComparable[]`, `coverageStatus`, `counts` avec les **six clés** `ADDED`, `CHANGED`, `UNCHANGED`, `NOT_OBSERVED_IN_B_COVERED_SCOPE`, `UNKNOWN`, `NOT_COMPARABLE`, `entries[]`, `countDenominator`, `limitations[]`. `entries[].classification` est l'une des six clés. `reason` provient du moteur #26, **pas de logique de comparaison JS**.

## Adaptateur explicite UI #28

L'adaptateur JS existant traduit `currentAvailability→availability`, `indexAccessible→indexAvailable`, `generations→snapshots`, et `availabilityObservedAtUtc→observedAtUtc`. **Ne pas renommer les champs du producteur**. L'adaptateur #28 actuel valide `observedUnitCount` comme entier obligatoire : **Dev 2 doit accepter `countKnown=false` et `observedUnitCount=null` pour l'index inaccessible**, sans fabriquer zéro. La validation des preuves `coverageProof` et `scopeIsExhaustive` doit rester stricte et leur absence doit produire un refus, jamais `complete`. Aucune conversion `NOT_OBSERVED_IN_B_COVERED_SCOPE→DELETED`.

## Vecteurs communs

- `online-complete` : A/B complets, UNCHANGED + ADDED + NOT_OBSERVED_IN_B_COVERED_SCOPE.
- `offline-stale` : même historique consultable malgré disponibilité OFFLINE et fraîcheur STALE.
- `stale-online` : source ONLINE mais historique STALE, indépendant de la disponibilité.
- `partial-b` : B partiel, UNKNOWN, couverture INCOMPLETE.
- `zero-unproven` : zéro unité, preuves insuffisantes, couverture INCOMPLETE.
- `multi-root-incomplete` : racines/domaines multiples sans preuves complètes, UNKNOWN.
- `index-inaccessible` : bibliothèque dans le registre, compteur inconnu/null, **pas** de comparaison autorisée.

Le payload exporté inclut `referenceVectors[]` (métadonnées de test ignorables par l'UI) en plus de `registry` et `comparisons`. Les vecteurs de référence contiennent leurs `scenarioId`, `libraryId`, `beforeSnapshotId`, `afterSnapshotId` et leurs invariants `expectedCoverageStatus` / `expectedCounts` dans `tests/fixtures/UiProjectionVectors.ps1`. L'exporteur valide les compteurs des six catégories et le dénominateur avant sérialisation. Aucun hash ROM ni accès aux collections réelles. Les scénarios d'erreur sont vérifiés par le harness dédié, pas injectés dans le payload UI.

**État de preuve :** code et fixtures versionnés, `PS51_NOT_EXECUTED` jusqu'à preuve contraire ; l'absence de JSON pré-généré n'est pas une réussite runtime.

## Reproduction contrôlée sur Windows PowerShell 5.1

Dans un clone privé autorisé de la branche #27 (sans données réelles), à la racine :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiLibraryProjection.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiUiProjectionSerialization.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Export-SyntheticUiProjection.ps1 -OutputJson "$env:TEMP\rm-synthetic-ui.json" -OutputJavaScript "$env:TEMP\rm-synthetic-ui.js"
```

Le fichier JS de sortie affecte `window.RM_PROJECTION_DATA` et peut être injecté par une **étape de build de confiance**, avant l'adaptateur UI, sans `fetch()` ni accès ROM. Ne pas remplacer le fichier `projection-data.js` de Dev 2 sur sa branche. La sortie stdout est également du JSON. Les scénarios `index-inaccessible` ont un registre visible mais aucune comparaison, et `observedUnitCount=null`.

Pour vérifier statiquement les sources UI de #28 après avoir récupéré leurs deux fichiers dans un dossier de travail **séparé** :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiUiAdapterContract.ps1 -UiAdapterPath "C:\temp\ui28\projection-adapter.js" -UiDataPath "C:\temp\ui28\projection-data.js"
```

Ce dernier contrôle est textuel et **ne prouve pas** le fonctionnement JavaScript. Les exports ne sont **pas** pré-calculés dans le dépôt tant qu'aucun environnement PS5.1 autorisé ne les a produits.

## Réalignement empilé sûr #26 → #27 (après réception)

1. Relever les SHA exacts des branches #26 et #27, `git status --porcelain` vide, et conserver une référence de sauvegarde locale du HEAD #27.
2. Comparer `git merge-base feat/inventory-snapshot-compare-9 feat/library-snapshot-projection-10` avec l'ancien parent `a7be7c2e...` ; constater la différence avec le nouveau HEAD #26 `6d79205a...`.
3. **Avant fusion de #26 :** conserver #27 empilée, préparer seulement le plan et la revue du delta CI, sans rebase destructif.
4. **Après fusion contrôlée de #26 par le Contrôleur :** relever le nouveau `main` ; sur clone de travail privé, vérifier `git log --oneline <ancien-parent>..feat/library-snapshot-projection-10`, puis `git rebase --onto main <ancien-parent> feat/library-snapshot-projection-10` **uniquement après autorisation et revue du graphe**. Conserver les références originales et vérifier `git range-diff`, `git diff --name-status main...HEAD`, les 18 modules historiques et les allowlists.
5. En cas de conflit CI : ne pas supprimer les contrôles de #26 ; résoudre explicitement 19→20 modules approuvés, conserver les manifestes et faire réviser avant toute mise à jour de branche distante. Aucun `push --force` automatique.
6. Exécuter les harness PS5.1 autorisés, publier les codes retour, puis demander réception et fusion par le Contrôleur.

**État actuel :** plan documenté, **aucun rebase/fusion effectué**.
