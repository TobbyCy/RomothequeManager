# ROMothèque Manager

**Une bibliothèque de jeux rétro compréhensible, organisée et préservée.**

ROMothèque Manager vise à inventorier des collections locales, distinguer les jeux et variantes, rapprocher les métadonnées EmulationStation Desktop Edition (ES-DE), montrer les incertitudes et préparer des opérations explicables. **Le projet est en développement : aucune application graphique Windows utilisable n'est encore livrée.**

## État vérifiable

- **Base technique existante** : modules Windows PowerShell 5.1, inventaire E1 en lecture seule, tests synthétiques ; le transfert du moteur historique vers ce dépôt indépendant est **en cours de validation**.
- **V2 envisagée** : interface Dashboard, Collection, fiches, bibliothèques, diagnostics ; spécifications documentées, **pas implémentées**.
- **Exports physiques, conversion et scraping** : aucune capacité réelle autorisée ni garantie par la présence des spécifications.
- **Distribution** : aucune GitHub Release publiée, aucun ZIP testé à l'extérieur, aucune licence de redistribution choisie.
- **Données** : aucun jeu, ROM, BIOS, contenu protégé, bibliothèque réelle ni identifiant matériel n'est fourni dans le dépôt ou les fixtures.

## Pour découvrir le projet

Consulter le [guide d'utilisation et les limites](docs/user/securite-et-limites.md), le [lexique](docs/user/lexique.md), les [parcours UX proposés](docs/ux/README.md) et la [roadmap](docs/roadmap.md). Les futurs téléchargements devront provenir d'une **Release officielle** avec ZIP autonome, empreinte SHA-256 et essai Windows extérieur vérifié ; ils ne sont **pas encore disponibles**.

## Pour contribuer

Lire [CONTRIBUTING](CONTRIBUTING.md), [SECURITY](SECURITY.md) et [SUPPORT](SUPPORT.md). Signaler un bug ou une idée avec une reproduction **entièrement fictive ou expurgée**. Ne pas joindre de ROM/BIOS, chemin personnel, XML ES-DE réel, StateRoot, capture sensible ou identifiant matériel.

Développeurs : [guide de démarrage](docs/developer/README.md), [architecture actuelle](docs/architecture/moteur-actuel.md), [qualité/CI](docs/developer/ci-adaptative.md), [décisions adoptées](docs/decisions/README.md). Les choix de technologies V2 et leurs contrats candidats ne sont pas réputés adoptés par leur mention.

## Sécurité et garanties

Le logiciel cible le respect de la bibliothèque d'origine : absence de suppression implicite, distinction entre inventaire historique et présence physique courante, contrôle explicite des opérations. **Une CI verte sur données fictives ne prouve pas la sûreté sur vos disques.** Aucune opération réelle ne doit être déclenchée à des fins de contribution ou d'évaluation.

**Statut du dépôt : PRIVATE pendant la préparation** ; aucune publication publique du code, des assets ou d'une Release n'est autorisée à ce stade. Une licence/attribution sera ajoutée seulement après décision et vérification des droits.
