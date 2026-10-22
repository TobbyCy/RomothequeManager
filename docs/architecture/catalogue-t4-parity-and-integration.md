# T4-A — Traçabilité contrat catalogue, audit et recette (CANDIDATE)

**Rôle : DEV 1.** Lot courant **T4-A**, distinct de T1 et T3-A historiques. Branches vérifiées le 22.10.2026 : #26 `6d79205a`, #27 `355ea45b`, #28 `e3fc250d`, #29 initial `e3873677`, #30 **`f641fb40`** (le HEAD `b46dd5d` du mandat est historique). Aucun merge/rebase. **Ce document ne constitue pas une adoption normative de la version 0.2.**

## A. Différences champ par champ et décision candidate

| Sujet | T1 `0.1.0-ui-catalogue-t1-candidate` | T3/T4 `0.2.0-ui-catalogue-candidate` | Règle/consommation Dev 2 |
|---|---|---|---|
| Racine | `{schemaVersion,readOnly,libraries,games,diagnostics,countKnown,gameCount,limitations}` | `{schemaVersion,readOnly,registry,games,diagnostics,countKnown,gameCount}` | `registry` contient la projection #27 `0.1.0-synthetic`. Les limitations sont rapportées par diagnostic/doc, pas une preuve autonome |
| `libraryId` | Sur cartes/jeux | Identifiant sur `registry.libraries`, jeux, provenance et observations | Exact, ordinal/casse sensible ; aucune identité dérivée d'une lettre USB |
| `snapshotId` | Sur `provenance` objet et `units[].provenance` | Sur `registry.generations`, `provenance[]`, `observations[]` | L'identifiant du snapshot **n'est jamais** un alias de `generationId` |
| `generationId` | Dans provenance objet | Référence explicite et distincte de `snapshotId` dans provenance/observations | Vérifiée contre la génération cible |
| `gameId` | `games[].gameId` | Idem, avec identité composée `(libraryId,platform,gameId)` | Plusieurs bibliothèques peuvent porter le même ID logique sans collision |
| `variantId` | `variants[]` chaînes ; `units[].variantId` | `variants:[{variantId,label}]` et `observations[].variantId` | Le label reste identique à l'ID dans les fixtures tant qu'aucune métadonnée vérifiée ne le précise |
| `romUnitId` | `units[].romUnitId` | `observations[].romUnitId` | Un jeu peut avoir plusieurs ROM, disques ou variantes ; ne jamais assimiler unité ROM et jeu |
| `units` vs `observations` | Unités et provenance interne | Observation structurée `{libraryId,snapshotId,generationId,romUnitId,variantId,discNumber,observedTitle,observationStatus}` | `discNumber` null/entier positif, `observedTitle` null/texte, statut OBSERVED/PARTIAL |
| `provenance` | Objet unique | Tableau d'un enregistrement `{libraryId,snapshotId,generationId,observedAtUtc,source:'SYNTHETIC_SNAPSHOT'}` | Renvoi exact au registre, date UTC exacte, snapshot **le plus récent** selon timestamp et tie-break ordinal ; ceci est une limitation du prototype T4 |
| `title` | Texte ou `null` | Texte ou `null` | Aucune invention si titre inconnu/contradictoire ; titre observé conservé par unité |
| `quality` | KNOWN/STALE/PARTIALLY_OBSERVED/CONTRADICTORY/UNKNOWN | Idem, indépendant de disponibilité | KNOWN exige preuves et fraîcheur FRESH ; contradiction produit `title=null` |
| `coverageStatus` | COMPLETE/INCOMPLETE | `coverStatus=KNOWN/UNKNOWN` et `status=KNOWN/INCOMPLETE` | Le statut global est **UNKNOWN** si ne serait-ce qu'une partition requise n'est pas couverte ; données partielles observées restent visibles |
| Disponibilité | `availability` sur chaque jeu | Idem + `registry.currentAvailability` | ONLINE/OFFLINE/INACCESSIBLE/UNKNOWN ; disponibilité actuelle **distincte** des preuves historiques |
| Fraîcheur | `freshness` par jeu | Idem + `registry.freshness` | FRESH/STALE/UNKNOWN ; STALE et UNKNOWN ne deviennent jamais KNOWN |
| Compteurs | `countKnown`, `gameCount` | Idem + par génération `observedUnitCount` | Si inexhaustif, inaccessible ou identité ambiguë : `countKnown=false`, `gameCount=null` ; aucune confusion null/0 |
| Localisation physique | Non prouvée | **Aucune voie locale inventée** ; partitions déclarées `sourceKey/rootKey/domain` restent dans le registre | Localisation logique, pas chemin ROM/SSD |
| Couverture | `coverageStatus` dérivée de #27 | `registry.generations[].coverageProof[]` + `scopeIsExhaustive` + `scanStatus` + `coverage[]` | Validateur indépendant recalcule chaque console à partir des partitions `console/sourceKey/rootKey/domain` |

**Règle d'ordre :** tri ordinal `StringComparer.Ordinal` pour identités, plateformes, sources, domaines, snapshots, preuves et groupes ; recherche `OrdinalIgnoreCase` côté PowerShell ; JS utilise ordre stable et `toLowerCase()` (différences Unicode de culture à comparer en recette). Le plus récent est déterminé par date UTC **chronologique**, puis `snapshotId` ordinal en cas d'égalité. Aucun rapprochement entre bibliothèques ni suppression implicite.

## B. Défauts observés sur T3-A et corrections de T4-A

| Fichier / fonction | Risque identifié | Correction T4 |
|---|---|---|
| `CatalogueProjection.psm1` / `New-RmCatSet`, `Sort-RmCatOrdinal` | Énumération implicite de HashSet/SortedDictionary, tableaux JSON potentiellement imbriqués ou vides | Retour scalaire explicite du set, matérialisation `List.ToArray()`, tri `Ordinal` |
| Idem / choix du snapshot | Comparaison lexicographique `12:00:00Z` vs `12:00:00.900Z` peut inverser la chronologie | `DateTimeOffset` UTC, identifiant ordinal en second critère ; canari horodatage fractionnaire |
| `CatalogueDtoContract.psm1` / preuves | Un unique `allComplete` global refusait une console GBA complète si PS1 partielle, ou acceptait des couvertures déclarées incohérentes | Recalcul **par plateforme** de chaque preuve déclarée, détection doublons, statuts/partitions exhaustifs |
| Idem / provenance | Référence à un snapshot historique autre que le dernier acceptée pour « catalogue actuel » | Vérification du dernier snapshot effectif et de `generationId` et date exacts |
| Idem / jeu/observation | Identité ROM dupliquée **entre deux jeux différents** pas rejetée | Clé par `(libraryId,snapshotId,romUnitId)` à travers les jeux |
| Idem / qualité | Disponibilité/fraîcheur du jeu non recoupées avec la bibliothèque | Rejet `CATDTO_LIBRARY_STATE_FORGED` |
| Idem / compteurs | `gameCount` seul ne garantit pas cohérence du nombre d'unités observées ; int JSON lu parfois Int64 sous PS5.1 | Compteurs de ROM comparés à `observedUnitCount`, null si incomplet ; Int32/Int64 bornés, ni float ni négatif |
| `catalogue-t3-consumer.js` / normalisation | Couverture globale et snapshot historique insuffisamment validés, sources d'incohérence T2 | Validation fail-closed per-console, références, titres, unités, compteurs et statuts |
| `tests/Invoke-CiCatalogueProjection.ps1` | Oracle erroné : ambiguïté testée comme `gameCount=0` au lieu de `null` | Correction du canari ; pas de faux zéro |
| `.github/workflows/ci.yml` | Les nouveaux tests n'étaient pas lancés par la CI | Harness PS5.1 et Node ajoutés à CI-0, gates et jobs existants conservés |

**Limite des preuves :** ces corrections sont vérifiées par inspection des blobs/versioning. PowerShell 5.1, Node, navigateur réel et CI **NOT_EXECUTED sur le HEAD T4**, sauf preuve de run produite ultérieurement.

## C. Matrice de non-régression ciblée

| Invariant | Fixture / harness qui appelle réellement le composant | Attendu | Refus ou état | Exécution |
|---|---|---|---|---|
| Jeu, variante, multidisque | `Export-SyntheticCanonicalCatalogue.ps1` → projection/conversion/validation | 1 jeu, 3 ROM, 2 variantes | ACCEPT | NOT_EXECUTED |
| JSON commun OFFLINE/STALE | `CatalogueT3CanonicalSample.json` → PS `Assert-RmCanonicalCatalogueDto` ET Node `RM_T3_CATALOGUE.normalize` | Même jeu et références, deux disques | ACCEPT | NOT_EXECUTED |
| Unicode et pagination | `Invoke-CiCatalogueProjection.ps1`, `catalogue_t3_consumer.test.cjs` | Texte Unicode, ordre/casse/limite définis | ACCEPT / PAGE_INVALID | NOT_EXECUTED |
| Multi-bibliothèques GBA/PS1 | `Invoke-CiCanonicalCatalogue.ps1` | Deux jeux distincts sans fusion | ACCEPT | NOT_EXECUTED |
| GBA complète, PS1 non observée | Même harness + test Node | GBA `complete`, PS1 `unobserved`, jeu INCOMPLETE, count null | ACCEPT (jamais COMPLETE global) | NOT_EXECUTED |
| Index inaccessible, OFFLINE, STALE, UNKNOWN | Harness PS producteur + JS | Compteur inconnu/null ; historique conservé | ACCEPT | NOT_EXECUTED |
| Snapshot historique présenté comme actuel | Mutation PS/JS provenance+observations simultanées | Refus | `CATDTO_HISTORICAL_SNAPSHOT_AS_LATEST` / `HISTORICAL_SNAPSHOT` | NOT_EXECUTED |
| Références génération/UTC falsifiées | Mutation DTO puis validateur | Refus | `CATDTO_GENERATION_PROVENANCE_MISMATCH`, `CATDTO_UTC_REQUIRED` | NOT_EXECUTED |
| Identité ROM trans-jeux dupliquée | Deux jeux distincts partageant même ROM | Refus | `CATDTO_CROSS_GAME_DUPLICATE_ROM` | NOT_EXECUTED |
| Couverture mensongère / racine ajoutée sans preuve | Mutation preuve, scan, coverage, champs statut | Refus | `CATDTO_COMPLETE_UNPROVEN`, `CATDTO_COVERAGE_STATUS_MISMATCH` | NOT_EXECUTED |
| Compteurs falsifiés | `observedUnitCount` élevé/abaissé, `gameCount=999` | Refus | `CATDTO_SNAPSHOT_OBSERVATION_COUNT_MISMATCH`, `CATDTO_OBSERVATION_COUNT_EXCEEDS_SNAPSHOT`, `CATDTO_COUNT_MISMATCH` | NOT_EXECUTED |
| Titres absents/contradictoires | mutations titres observés et présentés | Null ou refus | `CATDTO_TITLE_UNKNOWN_FORGED`, `CATDTO_TITLE_CONFLICT_FORGED` | NOT_EXECUTED |
| Structure / JSON invalide | suppression champ, `ConvertFrom-Json` tronqué, schéma 999 | Refus | `CATDTO_FIELD_MISSING`, `CATDTO_SCHEMA_UNSUPPORTED`, parse error | NOT_EXECUTED |
| Variations d'ordre des sources/snapshots/preuves | Deux entrées synthétiques sémantiquement identiques réordonnées | JSON canonique octet-identique | ACCEPT | NOT_EXECUTED |
| A/B #26/#27 inchangés | Harness existants `Invoke-CiSnapshotComparison.ps1`, `Invoke-CiLibraryProjection.ps1` | Six classes identiques, aucune suppression déduite | NON-RÉGRESSION À PROUVER | NOT_EXECUTED |

## D. Parité et raccordement T2

**Observation actualisée #30 HEAD `f641fb40` :** `t2-catalogue-model.js` contient désormais une branche `normalizeT3(dto)` activée par `0.2.0-ui-catalogue-candidate`, mais elle exige **`RM_T3_CATALOGUE` ET `RM_PROJECTION`**. `prototype/ui11/index.html` n'inclut pas actuellement le script `adapters/shared/catalogue-t3-consumer.js` depuis #29. Le chemin HTML `t2-catalogue.html` est une redirection vers l'interface principale. **Parité réelle T2 = NON DÉMONTRÉE** ; le travail de Dev 2 est plus avancé que le constat initial du mandat, sans être intégrable sans coordination des branches.

Étapes de validation croisées : (1) faire exécuter l'exporteur PS5.1 en mode synthétique depuis le HEAD fixé ; (2) enregistrer JSON brut et SHA, vérifier indépendamment en PS ; (3) injecter le JSON sous Node dans le consommateur JS candidat et `normalizeT3` de Dev 2 ; (4) comparer *valeurs métier*, six classifications A/B depuis le noyau #26 lorsqu'elles sont pertinentes, états connus/inconnus, compteurs, références et refus ; (5) tester `file://`, clavier et écran réel ; (6) revue externe puis décision propriétaire sur la version canonique. Aucun script ne copie les ROM ni ne recalculera le comparateur dans le navigateur.

## E. CI et plan exact d'intégration

- **Socle historique** : les 18 modules approuvés et leurs SHA d'intégrité ne sont pas touchés ; #26 + #27 ajoutent deux modules ; #29 en ajoute deux (`CatalogueProjection`, `CatalogueDtoContract`) => **22** modules nommément autorisés, pas de wildcard pour extensions.
- `tests/Invoke-CiStatic.ps1` : liste fermée des modules, fichiers tests et fixture JSON ; `tests/ci_policy.py` : ensemble nominal fermé de modules ; les chemins non classés continuent à router en mode `INFRA_SECURITY_UNKNOWN`/FULL, pas de skip.
- `.github/workflows/ci.yml` : contrôle PS5.1 du catalogue et canaris Node **ajoutés après les tests #26/#27**, sans supprimer aucun contrôle. L'absence de Node provoque un refus, pas un skip silencieux.
- **Dépendance stricte** : `#26 → #27 → #29`. La base #27 dépend d'un ancien HEAD de #26 ; comparer les 3 commits divergents, préserver les SHA originaux, effectuer `range-diff`/revue des merges **uniquement après mandat d'intégration**. Pas de rebase/push-force automatique.
- **Branche UI indépendante** : `#28 → #30`; aligner seulement après les validations et décision sur 0.2, sans modifier #30 depuis Dev 1.
- **Blocage actuel** : `tests/ci_policy.py` refuse des PR dont la base est différente de `main`. Les PR empilées ne peuvent pas afficher de CI intégration verte sans réalignement autorisé. **Aucun contournement** par modification des règles de base.

### Commandes futures dans un checkout privé autorisé (aucune donnée réelle)

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiSnapshotComparison.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiLibraryProjection.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiCatalogueProjection.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-CiCanonicalCatalogue.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Export-SyntheticCanonicalCatalogue.ps1 -OutputJson "$env:TEMP\rm-t4-canonical.json"
node tests/catalogue_t3_consumer.test.cjs
```

### Risques restant ouverts

1. **Authenticité non démontrable** : un acteur pouvant falsifier de manière cohérente toutes les sources et preuves peut fabriquer un DTO structurellement cohérent ; ce validateur n'est pas une signature cryptographique.
2. **Dernier snapshot ≠ dernier snapshot vérifié complet** : les données du dernier scan partiel sont visibles avec limites, ne sont pas une observation actuelle ni une absence globale.
3. **Preuve de rattachement unité→source/racine** : l'inventaire synthétique actuel ne fournit pas une localisation physique vérifiable par ROM ; le DTO ne l'invente pas.
4. **Surfaces T2** : T2 dispose d'une branche 0.2 source mais ni injection intégrée de #29 ni réception commune. Son propre modèle ancien 0.1 demeure candidat pour la démo.
5. **Compatibilité PS5.1** : collections génériques, retour d'objet, conversions JSON et gardes syntaxiques doivent encore être exécutés sur Windows 5.1. Les helpers `New-RmOrdinalSet`/`New-RmOrdinalMap` hérités de #27 méritent eux aussi une vérification runtime ciblée avant fusion ; toute correction à apporter à #27 doit être coordonnée avec son propriétaire.
6. **Modèle d'identité** : `romUnitId` est actuellement unique par snapshot/bibliothèque ; toute décision de réutilisation entre partitions requiert un arbitrage explicite, sans inférence.
7. **Gates** : revues indépendantes, CI sur les SHA/base effectivement intégrés et CI `push main` post-fusion restent obligatoires.

**STATUT T4-A :** CODE_COMMITTED / CONTRACT_0_2_CANDIDATE / STATIC_INSPECTION_ONLY / PS51_NOT_EXECUTED / NODE_NOT_EXECUTED / BROWSER_NOT_EXECUTED / CI_NOT_EXECUTED / DEV2_REVIEW_PENDING / HOLD_MERGE.
