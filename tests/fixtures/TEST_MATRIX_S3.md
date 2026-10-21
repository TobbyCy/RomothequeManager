# Stabilisation S3 — matrice de tests synthétiques du noyau

**Portée :** PR #26 comparateur (lecture seule) et PR #27 projection/JSON (canaris renforcés ici). **Aucun test PS5.1 exécuté dans cette session.** Les codes de refus sont des chaînes attendues et non des résultats observés. Ne pas assimiler audit statique, présence d'un canari ou compteurs attendus à un PASS runtime.

| Invariant | Fixture synthétique | Test qui invoque le composant | Résultat attendu | Exécution |
|---|---|---|---|---|
| Identifiants ordinaux sensibles à la casse | `caseRegistry`, `caseDuplicate` | `Invoke-CiLibraryProjection.ps1` → `New-RmLibraryProjection` | Deux identités distinctes | NOT_EXECUTED |
| Doublons snapshotId / generationId | `caseCollision`, `generationCollision` | Même harness → `New-RmLibraryProjection` | `LIBRARY_DUPLICATE_SNAPSHOT_OR_GENERATION` | NOT_EXECUTED |
| Partitions source/racine avec séparateur `|` | `rootReg` | Même harness → `Get-RmLibraryComparisonView` | `INCOMPLETE`, aucune fusion des partitions | NOT_EXECUTED |
| Sources/domaines multiples sans preuves exhaustives | `multiReg` | Même harness → `Get-RmLibraryComparisonView` | `INCOMPLETE`, `UNKNOWN` | NOT_EXECUTED |
| A/B partiels, scan failed, scope non exhaustif | `partialReg`, `unknownRegistry`, `noExhaustProjection` | Même harness → projection/comparaison | `INCOMPLETE`, pas de faux `ADDED` / `UNCHANGED` | NOT_EXECUTED |
| Dernier snapshot non prouvé | `invalidLast` | Même harness → `New-RmLibraryProjection` | `LIBRARY_LAST_VERIFIED_SCOPE_NOT_COMPLETE` | NOT_EXECUTED |
| Index inaccessible | `noIndex`, vecteur `index-inaccessible` | Harness bibliothèque + `Assert-UiDto` | `LIBRARY_INDEX_INACCESSIBLE`, `countKnown=false`, count `null`, pas de comparaison | NOT_EXECUTED |
| Zéro unité sans preuve | `zeroReg`, `zero-unproven` | Harness bibliothèque → `Get-RmLibraryComparisonView` | `INCOMPLETE`, avertissement `ZERO_OBSERVED_UNITS_NOT_PROOF_OF_EMPTY_LIBRARY` | NOT_EXECUTED |
| SnapshotId et generationId distincts | `caseCollision`, `generationCollision` | Harness bibliothèque → `New-RmLibraryProjection` | Refus des doublons par champ | NOT_EXECUTED |
| Compteurs falsifiés / dénominateur | DTO `ADDED=999`, `countDenominator=999`, `ADDED=-1` | `Invoke-CiUiProjectionSerialization.ps1` → `Assert-DtoRejected` → `Assert-UiDto` | `DTO_COUNT_MISMATCH`, `DTO_COUNT_INVALID` | NOT_EXECUTED |
| Preuve effacée | DTO `coverageProof=@()` | Même harness → validateur DTO | `DTO_PROOF_MISSING` | NOT_EXECUTED |
| Référence A/B falsifiée | DTO `beforeSnapshotId='nonexistent-snapshot'`, `after.snapshotId='forged-b'` | Même harness → validateur DTO | `DTO_REFERENCE_SNAPSHOT_MISMATCH`, `DTO_REFERENCE_AB_MISMATCH` | NOT_EXECUTED |
| Compteur inaccessible falsifié à zéro | DTO `index-inaccessible.observedUnitCount=0` | Même harness → validateur DTO | `DTO_INDEX_COUNT_NOT_UNKNOWN` | NOT_EXECUTED |
| JSON déterministe | Deux appels exporteur identiques | Même harness → `Export-SyntheticUiProjection.ps1` | JSON identique bit à bit | NOT_EXECUTED |
| Moteur #26 : absence ≠ suppression, six classes | `before` / `after`, `partialAfter`, `failedAfter` | `Invoke-CiSnapshotComparison.ps1` → `Compare-RmInventorySnapshots` | Six compteurs exhaustifs, aucun `DELETED` | NOT_EXECUTED |

## Audit ciblé des faiblesses et limites

- **Avant S3 :** certains canaris JSON ne vérifiaient que des propriétés dans une boucle distincte, sans envoyer les mutations de références A/B au validateur de refus. S3 soumet explicitement les mutations à `Assert-DtoRejected`.
- **Redondance acceptable :** deux canaris `LIBRARY_INDEX_INACCESSIBLE` vérifient le même refus via des fixtures indépendantes ; utile contre une régression d'assemblage mais ne doit pas être compté comme deux preuves runtime distinctes.
- **Limite persistante :** le validateur DTO est un validateur de **tests**, non une nouvelle API métier de validation JSON. La vérification de la complétude des partitions reste celle du noyau, pas un second moteur. Un token JS trouvé par `Invoke-CiUiAdapterContract.ps1` n'est pas une validation sémantique.
- **Recette Windows obligatoire :** exécuter sur Windows PowerShell 5.1, dans le dépôt privé et sans ROM réelles : `tests/Invoke-CiSnapshotComparison.ps1`, `tests/Invoke-CiLibraryProjection.ps1`, `tests/Invoke-CiUiProjectionSerialization.ps1`. Conserver les commandes, codes retour, SHA et messages complets ; ne déclarer PASS qu'après exécution effective.
