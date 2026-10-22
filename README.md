# ROMothèque Manager

**Organiser sa collection de jeux rétro en conservant des informations fiables sur les jeux, leurs variantes et les supports.**

ROMothèque Manager est un projet de catalogue et d'inventaire local. Il distingue les métadonnées EmulationStation Desktop Edition (ES-DE), les observations d'un inventaire et la disponibilité actuelle des supports. Un disque hors ligne ne signifie pas que ses jeux sont supprimés.

## Disponibilité actuelle

| Fonction | État |
| --- | --- |
| Moteur Windows PowerShell 5.1, inventaire E1 en lecture seule | Code existant soumis à intégration et vérification technique ; **pas une application grand public disponible** |
| Dashboard, bibliothèques, comparaison, Collection et fiches | Prototype HTML/CSS/JavaScript hors ligne intégré ; données exclusivement synthétiques, contrat catalogue 0.2 candidat |
| Opérations d'export, conversion, métadonnées en ligne | **Aucune opération réelle livrée ni autorisée** |
| Application Windows portable | **Aucun ZIP, installateur ou GitHub Release disponible** |
| Tests synthétiques | Résultats du dépôt final à vérifier ; une CI verte ne prouve pas un accès sûr à un disque réel |

La [démonstration HTML locale](prototype/ui11/README.md) permet d'explorer les vues sur des fixtures fictives. Elle n'est ni une application Windows autonome ni une Release ; PS5.1/Node/Edge de bout en bout restent à valider.

Consultez l'[état des fonctionnalités](docs/product-status.md) et la [roadmap](docs/roadmap.md). Les prochaines étapes ne constituent ni dates promises ni fonctions livrées.

## Utilisation et téléchargement

**Il n'existe actuellement aucun téléchargement à installer ou lancer.** Les scripts techniques ne doivent pas être essayés sur une collection personnelle. Une future préversion devra fournir un ZIP Windows autonome, son SHA-256, des notes de version et une vérification sur une machine externe sans runtime .NET séparé. Cet objectif n'est pas encore atteint.

Pour comprendre les futures vues : [parcours UX](docs/ux/README.md), [lexique](docs/user/lexique.md), [sécurité et limites](docs/user/securite-et-limites.md).

## Respect des droits d'auteur

ROMothèque Manager permet de gérer des collections locales de jeux vidéo rétro. Le projet **ne fournit ni ROM, ni BIOS, ni jeux commerciaux, ni lien de téléchargement vers des contenus protégés**. Les utilisateurs sont responsables des droits et autorisations applicables aux fichiers qu'ils utilisent. ROMothèque Manager est **indépendant des fabricants et éditeurs de jeux vidéo**.

La possession d'un jeu original n'autorise pas automatiquement le téléchargement d'une ROM ; inversement, certains contenus peuvent être librement utilisables, autorisés ou relever du domaine public. Leur statut dépend des droits, des fichiers et de la juridiction : voir la [notice légale](LEGAL.md).

## Licence et identité du projet

**Licence du code et de la documentation originale autorisée : MIT.**  
**Copyright (c) 2026 Cyril Tobler.** Mainteneur : **TobbyCy**.

La licence logicielle ne s'applique pas automatiquement aux ROM, BIOS, jaquettes, marques, logos et œuvres appartenant à des tiers. Les éléments officiels d'identité visuelle et les droits sur le nom sont traités séparément ; les cinq visuels V2 ne sont pas redistribués dans ce dépôt à ce stade.

Références : [LICENSE](LICENSE) · [LEGAL.md](LEGAL.md) · [BRANDING.md](BRANDING.md) · [CONTRIBUTING.md](CONTRIBUTING.md) · [SECURITY.md](SECURITY.md).

## Contribuer

Les contributions humaines sont organisées par Issues et pull requests : décrivez le besoin, des critères vérifiables, une reproduction sur fixtures **entièrement fictives** et les tests réellement exécutés. Consultez [CONTRIBUTING](CONTRIBUTING.md) et le [guide développeur](docs/developer/README.md).

Pour un signalement sensible, lisez [SECURITY](SECURITY.md) et ne divulguez pas de vulnérabilité dans une Issue. Pour toute question, voir [SUPPORT](SUPPORT.md).

Aucune ROM, aucun BIOS, XML ES-DE réel, chemin privé, StateRoot ou sauvegarde utilisateur ne doit être déposé ici. Les visuels soumis à vérification de droits sont exclus ; les droits sur ces visuels sont distincts de la licence MIT du logiciel.

**Le dépôt reste privé tant que sa mise à disposition publique n'a pas été autorisée.** Aucune Release n'est publiée.
