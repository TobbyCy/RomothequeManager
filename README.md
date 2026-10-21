# ROMothèque Manager

**Organiser sa collection de jeux rétro en conservant des informations fiables sur les jeux, leurs variantes et les supports.**

ROMothèque Manager est un projet de catalogue et d'inventaire local. Il distingue les métadonnées EmulationStation Desktop Edition (ES-DE), les observations d'un inventaire et la disponibilité actuelle des supports. Un disque hors ligne ne signifie pas que ses jeux sont supprimés.

## Disponibilité actuelle

| Fonction | État |
| --- | --- |
| Moteur Windows PowerShell 5.1, inventaire E1 en lecture seule | Code existant soumis à intégration et vérification technique ; **pas une application grand public disponible** |
| Dashboard, Collection, fiches et bibliothèques | [Conception](docs/ux/README.md), **non implémentée** |
| Opérations d'export, conversion, métadonnées en ligne | **Aucune opération réelle livrée ni autorisée** |
| Application Windows portable | **Aucun ZIP, installateur ou GitHub Release disponible** |
| Tests synthétiques | Résultats du dépôt final à vérifier ; une CI verte ne prouve pas un accès sûr à un disque réel |

Consultez l'[état des fonctionnalités](docs/product-status.md) et la [roadmap](docs/roadmap.md). Les prochaines étapes ne constituent ni dates promises ni fonctions livrées.

## Utilisation et téléchargement

**Il n'existe actuellement aucun téléchargement à installer ou lancer.** Les scripts techniques ne doivent pas être essayés sur une collection personnelle. Une future préversion devra fournir un ZIP Windows autonome, son SHA-256, des notes de version et une vérification sur une machine externe sans runtime .NET séparé. Cet objectif n'est pas encore atteint.

Pour comprendre les futures vues : [parcours UX](docs/ux/README.md), [lexique](docs/user/lexique.md), [sécurité et limites](docs/user/securite-et-limites.md).

## Contribuer

Les contributions humaines sont organisées par Issues et pull requests : décrivez le besoin, des critères vérifiables, une reproduction sur fixtures **entièrement fictives** et les tests réellement exécutés. Consultez [CONTRIBUTING](CONTRIBUTING.md) et le [guide développeur](docs/developer/README.md).

Pour un signalement sensible, lisez [SECURITY](SECURITY.md) et ne divulguez pas de vulnérabilité dans une Issue. Pour toute question, voir [SUPPORT](SUPPORT.md).

Aucune ROM, aucun BIOS, XML ES-DE réel, chemin privé, StateRoot ou sauvegarde utilisateur ne doit être déposé ici. Les visuels soumis à vérification de droits sont exclus. La licence de redistribution n'a pas encore été adoptée.

**Le dépôt reste privé tant que sa mise à disposition publique n'a pas été autorisée.** Aucune Release n'est publiée.
