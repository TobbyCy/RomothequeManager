# Journal des changements

Ce document décrit les évolutions **effectivement intégrées**. Les fonctionnalités proposées figurent dans la [roadmap](docs/roadmap.md) et ne sont pas annoncées comme livrées.

## Non publié — développement

- VAL-01 / Issue #39 : smoke **Microsoft Edge réel sous Windows**, en mode headless sur les deux pages HTML `file://` et données fictives. Pilotage CDP par Node natif sans dépendance téléchargée ; gestes souris, navigation, fiches, comparaison, index inaccessible, OFFLINE/STALE/UNKNOWN, exceptions JavaScript et requêtes réseau non autorisées. Contrôle obligatoire dans CI-0 ; preuve Edge et gate à vérifier sur SHA exact, aucun navigateur humain ni Release présumés.

- Issue #2 : correction de l'indentation du commit conditionnel dans le test Python de routage CI (`tests/test_ci_policy.py`, lignes 555–556) ; remplacement d'un canari négatif de chemin devenu obsolète (`.github/CODEOWNERS`, autorisé) par `.github/CODEOWNERS.txt` (toujours interdit et non ignoré par Git). Huit chemins négatifs contrôlés sans assouplir l'allowlist. Une assertion PS5.1 Bibliothèques découvert après déblocage de Python conserve son critère strict et affiche désormais l'exception observée en cas de refus incorrect ; gate complet à revalider.

- Issue #37 : les canaris de collision d'identité et de preuve utilisaient des références partagées aux snapshots nominaux. Chaque registre volontairement muté utilise désormais deux snapshots indépendants ; assertions additionnelles contre les mutations croisées. Validateur, refus d'index et gates inchangés ; CI à vérifier sur le HEAD exact.

- Couverture Pester CI : contrôle fermé des 22 noms de modules actuellement suivis (18 historiques + 4 extensions), tout en conservant la mesure reproductible des 18 modules historiques et de l'export E1. Aucun seuil de couverture artificiel et aucune suppression de canari ; nouvelle preuve runtime attendue sur le HEAD final.

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

## Comparaison de snapshots synthétiques — Issue #9

- Module PowerShell de comparaison A/B avec six classifications et couverture prouvée des deux générations.
- Identités `romUnitId` comparées ordinalement et sensibles à la casse ; canaris synthétiques associés.
- Aucun effet sur les fichiers ROM ; tests PS5.1 et CI sur l'arbre consolidé **non vérifiés**.

## Projection synthétique des bibliothèques — Issue #10

- Registre de bibliothèques et générations, disponibilité et fraîcheur distinctes, couverture fiable ou déclarée inconnue.
- Exporteur JSON synthétique pour interface hors ligne et canaris négatifs ; aucun effet physique sur ROM.
- Tests PowerShell 5.1 et CI sur cette intégration : **non vérifiés**.

## Catalogue synthétique partagé — PR #29

- Modules PowerShell de projection et validation du catalogue, contrat DTO candidat 0.2, consommateur JS canonique, provenance et compteurs fail-closed.
- Tests et fixtures négatives versionnés ; contrat encore candidat, pas d'export réel de collection.
- Tests Node/PS5.1 et CI de l'arbre consolidé **non vérifiés**.

## Catalogue consultable et fiches — PR #30

- Extension du prototype UI aux fiches, recherche et filtres sur données synthétiques, avec validation JS partagée et contrôles négatifs.
- Tests UI supplémentaires versionnés ; V8 simulé historique ne certifie pas Node natif, Edge ni la chaîne PS5.1→JSON→JS sur cet arbre.
- Aucune application autonome ni Release.

## CI publique et sécurité de contribution — PR #31

- Raccordement de huit suites Node du prototype à la CI, dont la vérification Unicode alimentée par le producteur PowerShell.
- Vérification des événements et permissions minimales GitHub Actions, CODEOWNERS et gate final fail-closed.
- Règles d'administration de `main` à configurer après PUBLIC. Exécution Actions sur cet arbre non vérifiée.

## Convention

Après chaque changement intégré, préciser le comportement, la compatibilité, les risques, les tests réellement exécutés et la référence de l'Issue ou PR de **ce produit**. Une version publiée nécessite un tag, des artefacts vérifiés et des notes de limites. Ne jamais annoncer un test non exécuté comme PASS.
