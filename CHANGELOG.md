# Journal des changements

Ce document décrit les évolutions **effectivement intégrées**. Les fonctionnalités proposées figurent dans la [roadmap](docs/roadmap.md) et ne sont pas annoncées comme livrées.

## Non publié — développement

- Guides du produit, de la contribution et de la sécurité, lexique et documentation d'architecture.
- Parcours d'interface et critères d'acceptation décrits pour les travaux futurs.
- Préparation des contrôles du moteur PowerShell 5.1 sur données synthétiques.
- Licence MIT adoptée pour les éléments originaux autorisés (`SPDX-License-Identifier: MIT`, copyright 2026 Cyril Tobler) ; garde CI à chemin exact pour `LICENSE` et admission du script préflight synthétique déjà suivi `tests/Invoke-CiE1PreflightSynthetic.ps1`. Aucun droit tiers ou média externe concédé.

**Aucune application Windows, ZIP ni GitHub Release n'a encore été publiée.** Les PR non fusionnées ne constituent pas des fonctionnalités disponibles.

## Convention

Après chaque changement intégré, préciser le comportement, la compatibilité, les risques, les tests réellement exécutés et la référence de l'Issue ou PR de **ce produit**. Une version publiée nécessite un tag, des artefacts vérifiés et des notes de limites. Ne jamais annoncer un test non exécuté comme PASS.
