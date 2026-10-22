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
- INT-03 : tables des identifiants `romUnitId` et de leur union ordinale sensibles à la casse, avec deux canaris anti-collision (`Unit-A` / `unit-a`), tests encore non exécutés.
- Le digest d'observation décrit seulement les champs d'observation sourcés ; `CHANGED`/`UNCHANGED` **n'attestent pas le changement ou l'identité des octets ROM**. Les fixtures C1/C2 et la répétabilité déterministe sont définies, mais ne sont pas exécutées.

**Validation : NON EXÉCUTÉE** sur le HEAD Draft dans GitHub Actions (service privé bloqué) ; aucune affirmation de tests Pester, Windows, E1 réel ou Release réussis. L'intégration fonctionnelle et sa revue restent à effectuer.

## Convention

Après chaque changement intégré, préciser le comportement, la compatibilité, les risques, les tests réellement exécutés et la référence de l'Issue ou PR de **ce produit**. Une version publiée nécessite un tag, des artefacts vérifiés et des notes de limites. Ne jamais annoncer un test non exécuté comme PASS.
