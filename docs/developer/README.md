# Guide de démarrage pour développeurs

ROMothèque Manager n'a pas encore de version graphique installable. **Vérifiez les fichiers réellement présents dans votre branche avant toute commande de build ou de test.**

## Environnements

| Domaine | Outil ou prérequis | Disponibilité |
| --- | --- | --- |
| Moteur historique | Windows PowerShell 5.1 Desktop | Import technique en cours de revue |
| Tests métier | Pester 4.10.1 vérifié, fixtures synthétiques | Reproductibilité à prouver au HEAD intégré |
| GitHub CI | Classification Docs/PS/Shared/.NET et gate final | Jobs/applicabilité/runners à contrôler |
| Interface .NET | SDK et outils à choisir et documenter ultérieurement | Aucun projet UI à compiler actuellement |

## Contribuer en sécurité

1. Choisissez une Issue Produit et lisez [CONTRIBUTING](../../CONTRIBUTING.md).
2. Sur votre branche, vérifiez le contenu des répertoires Modules, Export, Preflight et tests avant d'exécuter un point d'entrée : les sources peuvent encore se trouver en PR technique.
3. Inspectez les changements avec les commandes Git usuelles : git status --short, git diff --check, git diff.
4. Quand les suites sont intégrées, exécutez **les commandes documentées et réellement testées dans ce dépôt** sous Windows PowerShell 5.1, sans inventer de commandes pour un script absent.
5. Dans votre PR Draft, précisez commit testé, version Pester, suites découvertes et exécutées, canaris, résultats réels et limites. Un job qui n'a pas démarré n'est pas PASS.

## Références

[Architecture](../architecture/moteur-actuel.md), [contrat moteur](../architecture/contrat-moteur-historique.md), [ADR](../decisions/README.md), [tests Pester](pester-execution.md), [couverture](pester-coverage.md), [préflight synthétique](preflight-synthetique.md), [CI](ci-adaptative.md), [contribution Git](workflow.md).

Aucune bibliothèque ROM, BIOS, XML ES-DE, sauvegarde, StateRoot ni donnée sensible réelle ne doit entrer dans des fixtures, logs ou artefacts. Le runtime d'un futur ZIP autonome ne sera pas à installer séparément par l'utilisateur, mais le SDK de développement sera distinct ; **aucun ZIP n'existe à ce jour**.
