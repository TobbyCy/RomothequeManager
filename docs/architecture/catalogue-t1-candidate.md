# T1 — catalogue synthétique candidat, cartographie et recette

**Branche expérimentale** `feat/t1-catalogue-synthetic-integration`, dérivée du HEAD vérifié de #27 `355ea45bc72190a61e43b2570982dcfaa716d982`. Les PR #26/#27/#28 restent intactes. **Contrat candidat**, non normatif pour #28, `0.1.0-ui-catalogue-t1-candidate`. Aucun accès aux ROM, aucun scan ni persistance dans le module.

## Cartographie de réutilisation

| Composant | Fonction / structure | Usage T1 |
|---|---|---|
| #26 `Modules/SnapshotComparison.psm1` | `Compare-RmInventorySnapshots` ; six classes et preuves de couverture | **Non dupliqué**, comparaison A/B conservée dans #27 |
| #27 `Modules/LibrarySnapshotProjection.psm1` | `New-RmLibraryProjection`, `Get-RmLibraryComparisonView` ; `libraryId`, `snapshotId`, `generationId`, partitions `console/sourceKey/rootKey/domain`, `coverageProof`, `scopeIsExhaustive` | Validation préalable et métadonnées de bibliothèque/génération |
| #27 `tests/fixtures/SyntheticLibrarySnapshots.ps1` | Fabriques d'unités, snapshots et registre fictifs | Fixtures T1 |
| #27 `tests/Export-SyntheticUiProjection.ps1` | JSON `0.1.0-synthetic`, sept vecteurs, six comparaisons | **Conservé**, aucun changement de contrat |
| #28 `prototype/ui11/catalogue-adapter.js` | DTO `0.1.0-ui-catalogue-candidate` | Contrat **candidat différent**, à rapprocher par Dev 2, sans prétendre compatibilité immédiate |

## Code T1

- `Modules/CatalogueProjection.psm1` : `New-RmCatalogueProjection -Registry` et `Search-RmCatalogue -Catalogue`. **Read-only, objets mémoire**, n'ouvre aucun fichier. Le registre doit satisfaire le validateur #27. Le catalogue utilise le **dernier snapshot daté** de chaque bibliothèque (avec bris d'égalité ordinal sur snapshotId), sans le confondre avec `lastVerifiedCompleteRef` : si la couverture du dernier snapshot n'est pas prouvée, le jeu reste `PARTIALLY_OBSERVED`.
- Identité du jeu : **`libraryId + platform + gameId`**, avec segments à longueur préfixée et comparateur `StringComparer.Ordinal`. Un `romUnitId` désigne une unité observée ; les variantes sont des `variantId` distincts, et `discNumber` optionnel (entier strictement positif). Les observations ne deviennent pas automatiquement des jeux. Une identité `gameId` ou `variantId` absente produit `CATALOG_IDENTITY_AMBIGUOUS`, sans association inventée.
- Une même unité ROM ne peut apparaître deux fois dans un snapshot choisi : `CATALOG_DUPLICATE_ROM_ID`. Les plateformes non déclarées et schémas snapshots incompatibles sont refusés.
- Qualité : `KNOWN`, `STALE`, `PARTIALLY_OBSERVED`, `CONTRADICTORY`; titre absent → `null` + `CATALOG_TITLE_UNKNOWN`; index inaccessible → **aucun jeu extrapolé**, diagnostic `CATALOG_INDEX_INACCESSIBLE`, `countKnown=false`, `gameCount=null` (même si des bibliothèques accessibles fournissent des jeux). Les informations non observées ne sont pas une preuve d'absence.
- Recherche : filtres `Text,Platform,LibraryId,Availability,CoverageStatus,VariantId`, `Offset/Limit` (1..1000), texte `OrdinalIgnoreCase`, IDs et filtres `Ordinal`, ordre des jeux et unités `Ordinal`. Les titres Unicode sont conservés sans normalisation implicite.

## Enveloppe JSON candidate

`{schemaVersion,readOnly,libraries[],games[],diagnostics[],countKnown,gameCount,limitations[]}`. Chaque jeu : `libraryId,platform,gameId,title,quality,availability,freshness,coverageStatus,variants[],units[],provenance`. Chaque unité : `romUnitId,variantId,discNumber,provenance`. Provenance : `libraryId,snapshotId,generationId,observedAtUtc`. `games[]` n'est **pas** un miroir des fichiers physiques. Pas de chemins locaux exposés. **Ce DTO n'est pas injecté automatiquement dans #28** : adapter/versionner après décision du Contrôleur.

## Recette Windows PowerShell 5.1 (non exécutée)

Depuis un checkout privé autorisé de la branche T1, sans données réelles :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiSnapshotComparison.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiLibraryProjection.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiUiProjectionSerialization.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiCatalogueProjection.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Export-SyntheticCatalogue.ps1 -OutputJson "$env:TEMP\rm-t1-catalogue.json"
```

**Statut : CODE_COMMITTED / STATIC_REVIEW_REQUIRED / PS51_NOT_EXECUTED / CI_NOT_EXECUTED.** Ne pas transférer un ancien PASS à cette branche. L'exporteur existant #27 est inchangé ; le nouvel exporteur T1 est un chemin candidat séparé.

## Matrice compacte (tous NOT_EXECUTED)

| Invariant | Fixture/test | Attendu |
|---|---|---|
| Jeu + provenance du dernier snapshot | `New-T1Registry` / `Invoke-CiCatalogueProjection.ps1` | `gameCount=1`, `snapshotId=b` |
| Recherche Unicode/casse | `Étoile` vs `éTOILE` | 1 résultat ; filtre `LIB-FIXTURE` → 0 |
| Pagination | `Limit=1`, `Limit=0` | 1 élément ; `CATALOG_PAGE_INVALID` |
| Deux disques + variante légitime | `Export-SyntheticCatalogue.ps1` | 1 jeu, 3 unités, 2 variantes, `discNumber=1/2` |
| Identité ROM dupliquée | Mutation `units[]` | `CATALOG_DUPLICATE_ROM_ID` |
| Identité de jeu ambiguë | Mutation `gameId=null` | Aucun jeu inféré ; `CATALOG_IDENTITY_AMBIGUOUS` |
| Couverture non exhaustive | Mutation `scopeIsExhaustive=false` | `INCOMPLETE`, `PARTIALLY_OBSERVED` |
| Preuve de partition manquante | Mutation `coverageProof=[]` | `INCOMPLETE` |
| Index inaccessible | Mutation `indexAccessible=false` | `gameCount=null`, `countKnown=false` |
| Schéma incompatible | Mutation `schemaVersion=9.9` | `LIBRARY_SCHEMA_UNSUPPORTED` |
| JSON déterministe | Deux invocations exporteur | Sortie octet-identique |

## Limites et décisions réservées

1. **Pas de raccordement automatique à #28** : son DTO `0.1.0-ui-catalogue-candidate` diffère du DTO T1. Dev 2 doit examiner les champs et le mapping avant décision de version.
2. Le catalogue T1 consulte uniquement les unités du **dernier snapshot** ; il ne prétend pas reconstruire un historique complet ni un catalogue persistant multi-sources. L'identité inter-bibliothèques n'est pas déduite.
3. Pas de chemins de localisations physiques : `libraryId` et références de provenance sont les localisations **logiques** disponibles. Une future localisation par `sourceKey/rootKey` exige une relation prouvée au niveau de l'unité ; ne pas l'inventer.
4. L'absence de titre et l'ambiguïté sont diagnostiquées ; les règles métier de rapprochement inter-variantes et les enrichissements de métadonnées restent hors périmètre.
5. Les scripts T1 ne sont pas ajoutés à la CI stricte existante : une modification de l'allowlist, du compte de modules 20→21 et du workflow est à examiner avant intégration, **sans contournement de gate**. La présence du module supplémentaire sur la branche expérimentale peut faire échouer les contrôles statiques existants : **blocage d'intégration explicite**.
6. La chaîne complète de comparaisons reste fournie par #26/#27, **pas** par un second comparateur T1. Recette PS5.1 obligatoire, puis parité Node/navigateur avec #28 sur mêmes fixtures et SHA exacts.
