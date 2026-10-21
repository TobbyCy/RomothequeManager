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
- Cinq classifications : ajout prouvé, observation modifiée, inchangée, absence dans un inventaire complet (jamais suppression physique), non comparable lorsque les observations manquent ou sont incomplètes.
- Harness de fixtures synthétiques et canaris de refus raccordé à CI-0, sans modification des 33 sources historiques.

**Validation : NON EXÉCUTÉE** sur le HEAD Draft dans GitHub Actions (service privé bloqué) ; aucune affirmation de tests Pester, Windows, E1 réel ou Release réussis. L'intégration fonctionnelle et sa revue restent à effectuer.

## Convention

Après chaque changement intégré, préciser le comportement, la compatibilité, les risques, les tests réellement exécutés et la référence de l'Issue ou PR de **ce produit**. Une version publiée nécessite un tag, des artefacts vérifiés et des notes de limites. Ne jamais annoncer un test non exécuté comme PASS.
