# T3-A — Contrat catalogue commun proposé (non normatif)

**Statut : CANDIDATE / DEV2_REVIEW_PENDING / PS51_NOT_EXECUTED.** PR #29 (producteur) vs PR #30 (consommateur). Aucune modification de #30 ou du contrat normatif #27/#28. Référence T2 lue : `prototype/ui11/t2-catalogue-model.js`, `t2-catalogue-fixture.js`, `t2-catalogue.test.cjs`, HEAD `87366d60bda5883536637680a6123f70fdfe7a98`.

## Décisions candidates

- **Version candidate nouvelle :** `0.2.0-ui-catalogue-candidate`. L'ancien `0.1.0-ui-catalogue-t1-candidate` et le `0.1.0-ui-catalogue-candidate` de T2 restent distincts et ne sont pas silencieusement remplacés.
- **Enveloppe :** `{schemaVersion,readOnly,registry,games[],diagnostics[],countKnown,gameCount}`. `registry` contient **exactement** la projection de bibliothèques `0.1.0-synthetic` (#27), avec `sources`, `platforms`, `generations`, `coverageProof` et `coverage`. Pas de snapshot reconstruit par le navigateur.
- **Identité d'un jeu :** triplet ordinal sensible à la casse `(libraryId,platform,gameId)`, pas `gameId` global. `variantId` et `romUnitId` restent distincts. Les unités multidisques ont des `discNumber` optionnels positifs ; aucune identité n'est inférée pour un jeu ambigu.
- **Jeu :** `{libraryId,platform,gameId,title,quality,status,coverStatus,availability,freshness,variants[],provenance[],observations[]}`. `title` peut être `null` si inconnu ou contradictoire, contrairement à T2 actuel. UI doit afficher un libellé d'indisponibilité sans transformer `null` en identité.
- **Provenance :** tableau (un élément dans ce candidat) `{libraryId,snapshotId,generationId,observedAtUtc,source:'SYNTHETIC_SNAPSHOT'}`. **Interdiction de considérer generationId comme alias de snapshotId**. La génération et la date doivent correspondre exactement à l'objet `registry.libraries[].generations[]` référencé.
- **Variantes :** `[{variantId,label}]` ; le label T1 est provisoirement l'ID sans enrichissement. **Observations :** `[{libraryId,snapshotId,generationId,romUnitId,variantId,discNumber,observedTitle,observationStatus}]`. L'observation renvoie à la provenance et à une variante déclarée ; titre observé conservé pour vérifier les contradictions. `units` reste interne au moteur T1, pas dans le DTO commun.
- **États :** `quality=KNOWN|STALE|PARTIALLY_OBSERVED|CONTRADICTORY` (état métier de qualité), `status=KNOWN|INCOMPLETE` (vue T2), `coverStatus=KNOWN|UNKNOWN` (vue T2), `observationStatus=OBSERVED|PARTIAL`. Le statut de disponibilité `ONLINE|OFFLINE|INACCESSIBLE|UNKNOWN` et la fraîcheur `FRESH|STALE|UNKNOWN` restent des axes indépendants. **OFFLINE n'efface pas un historique vérifié ; STALE n'est pas une observation actuelle.**
- **Comptage :** `gameCount` est entier uniquement si `countKnown=true`; sinon `null`. La couverture partielle, l'index inaccessible ou l'identité ambiguë empêchent une affirmation exhaustive. Les jeux observés restent affichables.
- **Couverture :** `KNOWN` exige `scanStatus=complete`, `scopeIsExhaustive=true`, toutes les partitions attendues `console/sourceKey/rootKey/domain` prouvées et toutes les plateformes couvertes. Une preuve manquante ne peut pas devenir COMPLETE. Les preuves et générations demeurent dans `registry` pour validation indépendante.

## Chaîne exécutable proposée

```powershell
powershell.exe -NoProfile -File .\tests\Invoke-CiCatalogueProjection.ps1
powershell.exe -NoProfile -File .\tests\Invoke-CiCanonicalCatalogue.ps1
powershell.exe -NoProfile -File .\tests\Export-SyntheticCanonicalCatalogue.ps1 -OutputJson "$env:TEMP\rm-t3-catalogue.json"
```

La sortie provient de `New-RmCatalogueProjection` → `ConvertTo-RmCanonicalCatalogueDto` → **`Assert-RmCanonicalCatalogueDto`** → sérialisation et relecture. Le validateur est indépendant du producteur et s'exécute sur le DTO réellement produit, puis sur les DTO mutés par les canaris. Il n'appelle ni `New-RmCatalogueProjection` ni `ConvertTo-RmCanonicalCatalogueDto`.

## Exemples de mutations invalides (sur la sortie réelle)

```text
schemaVersion = "999"                         -> CATDTO_SCHEMA_UNSUPPORTED
games[0].provenance[0].snapshotId = "ghost"    -> CATDTO_ORPHAN_SNAPSHOT
games[0].provenance[0].generationId = "ghost"  -> CATDTO_GENERATION_PROVENANCE_MISMATCH
games[0].observations[0].variantId = "ghost"   -> CATDTO_ORPHAN_VARIANT
games[0].coverStatus = "UNKNOWN"               -> CATDTO_COVERAGE_FORGED
registry.libraries[0].generations[1].coverageProof = [] -> CATDTO_COMPLETE_UNPROVEN
gameCount = 999                                -> CATDTO_COUNT_MISMATCH
```

## Matrice de couverture T3-A (résultats attendus, NON EXÉCUTÉS)

| Famille | Harness | Invariant | Attendu |
|---|---|---|---|
| Complet multidisque + variantes | `Export-SyntheticCanonicalCatalogue.ps1` | 1 jeu / 3 observations / 2 variantes | DTO accepté |
| Références A/B, génération et date | `Invoke-CiCanonicalCatalogue.ps1` | snapshot/generation/UTC non interchangeables | Rejet `CATDTO_ORPHAN_SNAPSHOT` / `CATDTO_GENERATION_PROVENANCE_MISMATCH` |
| Doublons | même | jeu, génération, unité observée | Rejet stable |
| Couverture forgée | même | scan failed, scope non exhaustif, preuve supprimée | `CATDTO_COMPLETE_UNPROVEN` |
| Jeu partiel avec unité observée | même | données présentes conservées, `gameCount=null` | `INCOMPLETE`, `PARTIAL` |
| Hors ligne / obsolète | même | observation historique conservée | `OFFLINE` / `STALE` |
| Index inaccessible | même | aucun jeu inféré, `gameCount=null` | `countKnown=false` |
| Deux bibliothèques / GBA+PS1 | même | identités par bibliothèque et plateforme | 2 jeux, 2 plateformes |
| Identité ambiguë, titre absent/contradictoire | `Invoke-CiCatalogueProjection.ps1` | pas de fusion ni de titre inventé | diagnostic / titre null |
| Déterminisme | deux exports | JSON identique | égalité stricte |
| Régression #26/#27 | harness existants | comparateur et couverture | à exécuter PS5.1 |

## Consommateur candidat fourni à Dev 2

`adapters/shared/catalogue-t3-consumer.js` expose `RM_T3_CATALOGUE.normalize(dto)` et `query(model,filters)` sans accès disque/réseau ; il accepte la version T3, conserve `title=null`, refuse les références orphelines, les fausses couvertures, les preuves de partition dupliquées et les compteurs incohérents. **Ce fichier est sur la branche #29, pas dans #30** : Dev 2 doit en auditer le comportement et décider s'il adapte son propre modèle, sans import silencieux. `tests/catalogue_t3_consumer.test.cjs` contient 14 assertions Node candidates : `node tests/catalogue_t3_consumer.test.cjs`. **NODE_NOT_EXECUTED**. Le contrôle JS ne remplace pas le validateur PowerShell indépendant ni l'attestation d'origine.

**Fixture JSON partagée :** `tests/fixtures/CatalogueT3CanonicalSample.json` est un exemple synthétique explicite (bibliothèque OFFLINE, snapshot STALE, couverture prouvée, jeu à deux disques, provenance distincte snapshot/generation). Les deux harness `Invoke-CiCanonicalCatalogue.ps1` et `catalogue_t3_consumer.test.cjs` lisent **le même fichier** et le soumettent à leurs validateurs respectifs. **Exemple assemblé statiquement, pas produit par une exécution PS5.1** : il ne remplace pas le futur test de parité sur la sortie de `Export-SyntheticCanonicalCatalogue.ps1`.

## Compatibilité avec T2 #30 : divergences explicites

T2 actuel **rejette** ce DTO : sa version est `0.1.0-ui-catalogue-candidate`, son `normalize(dto,registry)` exige un titre non vide, n'accepte pas le triplet bibliothèque/plateforme/jeu, assimile `generationId` et `snapshotId` dans une recherche de snapshot, et n'accepte pas `generationId` dans sa preuve de provenance. La conversion canonique T3 fournit désormais `status`, `coverStatus`, `observations`, `variants[]` et `provenance[]`, mais **la parité du consommateur n'est pas acquise** : Dev 2 doit proposer un adaptateur explicitement versionné, des tests de consommation de l'export réel et une politique UI pour `title=null`, `countKnown=false`, `PARTIALLY_OBSERVED`.

## Limites / garde-fous

- **Aucune signature cryptographique ni attestation externe :** un attaquant pouvant falsifier **à la fois** toutes les données, les partitions, la couverture et les identifiants de façon parfaitement cohérente peut produire un faux DTO valide. Le validateur prouve la **cohérence interne**, pas l'authenticité d'un scan.
- Le DTO est dérivé des **derniers snapshots en mémoire** ; pas de localisation physique déduite ni de migration d'index réel.
- Les métadonnées manquantes sont principalement représentées par `title=null` et diagnostics ; pas encore de registre de métadonnées externes.
- La branche #29 reste empilée sur #27, elle-même sur ancien HEAD #26. Aucun réalignement automatique.
- CI existante refuse les PR dont la base n'est pas `main` : la PR Draft empilée n'est **pas** une preuve CI verte. Les allowlists PS et Python ont été mises à jour de façon nominative pour **22 modules** sans toucher aux 18 historiques. Aucune exécution PS5.1 ou CI dans cet environnement.
