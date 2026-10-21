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
- `partial-b` : B partiel, UNKNOWN, couverture INCOMPLETE.
- `zero-unproven` : zéro unité, preuves insuffisantes, couverture INCOMPLETE.
- `multi-root-incomplete` : racines/domaines multiples sans preuves complètes, UNKNOWN.
- `index-inaccessible` : bibliothèque dans le registre, compteur inconnu/null, **pas** de comparaison autorisée.

Les vecteurs de référence contiennent leurs `scenarioId`, `libraryId`, `beforeSnapshotId`, `afterSnapshotId` et leurs invariants `expectedCoverageStatus` / `expectedCounts` dans `tests/fixtures/UiProjectionVectors.ps1`. L'exporteur valide les compteurs des six catégories et le dénominateur avant sérialisation. Aucun hash ROM ni accès aux collections réelles. Les scénarios d'erreur sont vérifiés par le harness dédié, pas injectés dans le payload UI.

**État de preuve :** code et fixtures versionnés, `PS51_NOT_EXECUTED` jusqu'à preuve contraire ; l'absence de JSON pré-généré n'est pas une réussite runtime.
