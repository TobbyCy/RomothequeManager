# Journal des changements

Ce document décrit les évolutions **effectivement intégrées**. Les fonctionnalités proposées figurent dans la [roadmap](docs/roadmap.md) et ne sont pas annoncées comme livrées.

## Non publié — développement

- Guides du produit, de la contribution et de la sécurité, lexique et documentation d'architecture.
- Parcours d'interface et critères d'acceptation décrits pour les travaux futurs.
- Préparation des contrôles du moteur PowerShell 5.1 sur données synthétiques.

**Aucune application Windows, ZIP ni GitHub Release n'a encore été publiée.** Les PR non fusionnées ne constituent pas des fonctionnalités disponibles.

## En préparation — Issue #9 (PR Draft non fusionnée)

- Contrat de snapshots v1 en mémoire : générations datées, bibliothèque logique, plateforme/couverture et états de capture distincts de la disponibilité Offline/Stale.
- Composant PowerShell 5.1 de comparaison des identités persistantes `romUnitId` avec `gameId`/`variantId` ; `fileId` ne sert jamais d'identité inter-scans.
- Corrections C1/C2 suite à contre-revue : intersection explicitement complète **dans A et B** (scan et plateforme) requise pour toute conclusion, y compris unité commune et ajout ; six classifications distinctes `ADDED`, `CHANGED`, `UNCHANGED`, `NOT_OBSERVED_IN_B_COVERED_SCOPE`, `UNKNOWN` (données insuffisantes) et `NOT_COMPARABLE` (périmètre non déclaré ou incompatibilité isolable).
- Harness de fixtures synthétiques et canaris de refus raccordé à CI-0, sans modification des 33 sources historiques.
- Le digest d'observation décrit seulement les champs d'observation sourcés ; `CHANGED`/`UNCHANGED` **n'attestent pas le changement ou l'identité des octets ROM**. Les fixtures C1/C2 et la répétabilité déterministe sont définies, mais ne sont pas exécutées.

**Validation : NON EXÉCUTÉE** sur le HEAD Draft dans GitHub Actions (service privé bloqué) ; aucune affirmation de tests Pester, Windows, E1 réel ou Release réussis. L'intégration fonctionnelle et sa revue restent à effectuer.

## En préparation — Sprint #10 Bibliothèques / snapshots (PR Draft empilée #27)

- Registre de bibliothèques **synthétiques** en mémoire : `libraryId` persistant, nom, plateformes, sources/rootKey/domaines, disponibilité live datée, fraîcheur, index consultable, dernier snapshot complet référencé et historique de générations.
- Adaptateur read-only `Get-RmLibraryComparisonView` utilisant **sans duplication** le comparateur #26, exposant refs A/B, couvertures par plateforme et partitions de provenance, six états, raisons, compteurs sur A∪B et limitations explicites, y compris **zéro unité avec couverture insuffisante**.
- Fail-closed : couverture `complete` dérivée uniquement d'un manifeste de partitions complet et exhaustif, jamais de la disponibilité ONLINE ; multi-racines/domaines non prouvés → UNKNOWN / INCOMPLETE. Offline/Stale historique conservé lorsque l'index est accessible.
- Fixtures synthétiques et canaris négatifs ajoutés à CI-0, allowlist limitée au chemin exact de fixture ; intégrité des 18 modules historiques conservée, deux modules de fonctionnalité explicitement approuvés. Aucune politique désactivée.

**Validation Windows PowerShell 5.1/Pester/CI : NOT_EXECUTED.** PR #27 dépend de #26 non fusionnée ; pas de GUI, données réelles, application publiée ni Release.

## En préparation — Sprint #14 Stabilisation noyau et export UI (#27)

- Identités `libraryId/snapshotId/generationId/sourceKey/rootKey` traitées en ordinal sensible à la casse ; partitions `console/sourceKey/rootKey/domain` encodées par segments de longueur, sans collision de séparateur `|`.
- Index inaccessible : `countKnown=false` et `observedUnitCount=null` ; aucune fausse bibliothèque vide. Scan failed ou preuve exhaustive absente → couverture non complète.
- Producteur synthétique `tests/Export-SyntheticUiProjection.ps1` → `{registry,comparisons}` JSON `0.1.0-synthetic` et affectation JS optionnelle `window.RM_PROJECTION_DATA`, via le **vrai** comparateur #26 ; six scénarios de référence et harness de sérialisation.
- Contrat `docs/architecture/contrat-json-ui-projection.md` pour Dev 2 #28 ; allowlist chemins précis et gate CI-0 de sérialisation ajoutés sans désactiver les protections.

**Tests PowerShell 5.1 et Actions : NOT_EXECUTED.** Sortie JSON synthétique non générée en runtime dans cette session ; adaptation UI nullable à recevoir. PR Draft empilée sur #26 ; pas de fusion ni Release.

## Convention

Après chaque changement intégré, préciser le comportement, la compatibilité, les risques, les tests réellement exécutés et la référence de l'Issue ou PR de **ce produit**. Une version publiée nécessite un tag, des artefacts vérifiés et des notes de limites. Ne jamais annoncer un test non exécuté comme PASS.
