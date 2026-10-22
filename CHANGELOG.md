# Journal des changements

Ce document décrit les évolutions **effectivement intégrées**. Les fonctionnalités proposées figurent dans la [roadmap](docs/roadmap.md) et ne sont pas annoncées comme livrées.

## Non publié — développement

- Guides du produit, de la contribution et de la sécurité, lexique et documentation d'architecture.
- Parcours d'interface et critères d'acceptation décrits pour les travaux futurs.
- Préparation des contrôles du moteur PowerShell 5.1 sur données synthétiques.
- Licence MIT adoptée pour les éléments originaux autorisés (`SPDX-License-Identifier: MIT`, copyright 2026 Cyril Tobler) ; garde CI à chemin exact pour `LICENSE` et admission du script préflight synthétique déjà suivi `tests/Invoke-CiE1PreflightSynthetic.ps1`. Aucun droit tiers ou média externe concédé.

**Aucune application Windows, ZIP ni GitHub Release n'a encore été publiée.** Les PR non fusionnées ne constituent pas des fonctionnalités disponibles.

## En préparation — UI #11 (PR Draft #28, non fusionnée)

- Prototype **hors ligne HTML/CSS/JavaScript natifs**, situé dans `prototype/ui11/` : navigation Dashboard, Bibliothèques, Collection, Comparaison et inspecteur.
- Quatre bibliothèques **fictives** et historique daté ; états ONLINE/OFFLINE/INACCESSIBLE/UNKNOWN, FRESH/STALE/UNKNOWN, dernier complet vérifié indépendant de la dernière tentative.
- Sélection A/B, six classifications, filtres et détail ; couverture A/B toujours affichée, y compris en cas de liste vide sous scan partiel. Aucun libellé de suppression physique.
- Miroir JavaScript du contrat de comparaison #26 **pour démonstration uniquement**, non une exécution PowerShell ni une preuve de parité avec Dev 1.
- Test V8 à DOM simulé effectué en session ; smoke Node.js fourni mais **non exécuté**, pas de capture ni de test navigateur graphique, CI/Windows **NOT_EXECUTED**.
- **Non livré** sur `main`, aucun exécutable Windows, ZIP ou Release, aucune lecture/écriture ROM.


- Sprint #13, **PR Draft #28 uniquement** : adaptateur de projection JSON synthétique `0.1.0-synthetic`, deux modes, contrôle des six comptes, de la couverture et refus sans paire précalculée. **Non fusionné, non publié** ; aucune parité PS5.1 revendiquée.


- **Sprint #15, PR Draft #28** : contrat candidat catalogue de jeux et fiche projetée, provenance datée, variantes, observations ROM séparées, navigation vers snapshots ; sample synthétique hors ligne et smoke Node ajouté (NOT_EXECUTED). Adaptateur registre aligné sur `countKnown`/compteur null. 9 contrôles V8 avec DOM simulé PASS, aucune parité PS5.1 ni recette navigateur.

## Convention

Après chaque changement intégré, préciser le comportement, la compatibilité, les risques, les tests réellement exécutés et la référence de l'Issue ou PR de **ce produit**. Une version publiée nécessite un tag, des artefacts vérifiés et des notes de limites. Ne jamais annoncer un test non exécuté comme PASS.
