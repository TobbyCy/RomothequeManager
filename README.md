# ROMothèque Manager

<p align="center">
  <img src="assets/branding/RM_V2_Horizontal_Fond_Sombre.png" alt="ROMothèque Manager — cartouche rétro stylisée avec étagère et flèche rouge" width="640">
</p>

**Inventorier, comprendre et organiser ses bibliothèques de jeux rétro — sans perdre de vue la provenance des fichiers ni l'état réel des supports.**

ROMothèque Manager est un projet logiciel **indépendant et open source pour son code original**, consacré à la gestion de collections **locales** de jeux vidéo rétro. Il distingue les jeux, leurs variantes, les unités de fichiers, les **métadonnées issues de sources importées** (dont **EmulationStation Desktop Edition — ES-DE**, source historique initiale) et les observations enregistrées au fil des inventaires. **À terme**, l'objectif est de conserver un catalogue interne indépendant des XML importés ; cette persistance autonome n'est pas encore livrée.

> **État du Produit au 28 octobre 2026 :** code source public, démonstrations web locales et [**première Pre-release Windows x64 V2-01C publiée**](https://github.com/TobbyCy/RomothequeManager/releases/tag/poc-v2-01c-20261023-4cf475fbd1ec), autonome (.NET inclus). Il s'agit **uniquement d'un POC synthétique** : quatre vues Avalonia, six jeux fictifs et trois bibliothèques fictives. **Ce ZIP historique ne contient pas NEXT-01-A/NEXT-02 et ne permet aucun scan réel.** Le `main` contient désormais le lecteur ES-DE NEXT-01-A et le moteur d'inventaire NEXT-02, testés sur fixtures fictives, mais **l'inventaire n'est pas encore accessible dans l'interface Windows**.
>
> **Aucune ROM, aucun BIOS ni jeu commercial n'est fourni ou proposé au téléchargement.**
>
> **Bandeau officiel V2 :** identité créée pour le projet, reproduite ici avec son autorisation. Les [droits graphiques](BRANDING.md) sont distincts de la licence MIT du code.

## Pourquoi ROMothèque Manager ?

Une bibliothèque rétro n'est pas simplement une liste de fichiers. Un même jeu peut posséder plusieurs versions, disques ou formats ; des métadonnées importées, notamment depuis ES-DE, peuvent regrouper ou décrire ces éléments de manière différente ; et les supports ou leurs index ne sont pas toujours accessibles.

Le projet cherche à répondre à quatre questions sans les confondre :

- **Qu'est-ce que je possède dans mon catalogue ?** Un jeu, ses variantes et les unités de fichiers qui lui sont associées ne désignent pas la même chose.
- **Qu'a-t-on réellement observé ?** Un inventaire ou un snapshot décrit un état daté, avec sa source, son niveau de couverture et ses éventuelles limites.
- **Qu'est-ce qui reste consultable ?** Une bibliothèque `OFFLINE`, `STALE`, `UNKNOWN` ou dont l'index est inaccessible ne prouve pas que ses jeux ont disparu.
- **Que pourra-t-on exporter en sécurité ?** Les ambiguïtés, dépendances et conflits doivent être visibles et résolus avant toute opération physique.

**Principe central : absence d'observation ≠ suppression du fichier.** ROMothèque Manager privilégie la lisibilité, les preuves et la préservation des sources plutôt qu'une action automatique difficile à annuler.

## En un coup d'œil

| Domaine | Ce qui existe aujourd'hui | Ce qui reste à faire |
| --- | --- | --- |
| **Moteur et inventaire E1** | Sources Windows PowerShell 5.1 intégrées ; tests et préflights sur données synthétiques | Qualification avant tout usage sur une collection réelle |
| **Tableau de bord et bibliothèques** | Prototype HTML/CSS/JavaScript consultable localement ; états de disponibilité et de fraîcheur fictifs | Persistance et raccordement à de véritables bibliothèques autorisées |
| **Snapshots et comparaison A/B** | Historique fictif, choix de deux snapshots, six classifications et couverture explicite | Recette UX détaillée du POC publié et raccordement futur à des bibliothèques réelles |
| **Collection et fiches de jeux** | Recherche, filtres, variantes, provenance et observations dans les démonstrations synthétiques | Enrichissement et gestion durable des données utilisateur |
| **Catalogue partagé PS → JSON → JS** | Producteur et consommateur intégrés ; **DTO 0.2 encore candidat**, tests synthétiques | Adoption du contrat et recette navigateur complète |
| **Export E2 à E6, conversion, copie** | Concepts, contrats et garde-fous documentés | Fonctionnalités non livrées ; **aucune opération réelle autorisée** |
| **POC Windows V2-01A/B (.NET 10 / Avalonia 12.1.4)** | Code et tests intégrés via [PR #42](https://github.com/TobbyCy/RomothequeManager/pull/42) et [#48](https://github.com/TobbyCy/RomothequeManager/pull/48) : Dashboard, Collection, Fiche et Bibliothèques/snapshots A/B ; six jeux et trois bibliothèques fictifs | Présent dans la **Pre-release de test V2-01C**, en lecture seule sur fixtures ; sans scan, import ES-DE dans l'UI, persistance ou export |
| **Moteur d'inventaire NEXT-02 (code de `main`)** | [Contrat P1 #66](https://github.com/TobbyCy/RomothequeManager/pull/66) et [moteur #68](https://github.com/TobbyCy/RomothequeManager/pull/68) intégrés ; inventaire local volontaire en lecture seule, indépendant des XML ES-DE ; [CI `push main` #40691495453](https://github.com/TobbyCy/RomothequeManager/actions/runs/40691495453) verte sur l'intégration | **Non présent dans le ZIP V2-01C historique, non relié à l'UI Avalonia**, pas de scan de collection réelle autorisé |
| **Pre-release Windows portable V2-01C** | [ZIP Windows x64 autonome](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip) et [SHA-256](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip.sha256) **publiés le 23.10.2026**, avec notices tierces ; [publication contrôlée et traçabilité #50](https://github.com/TobbyCy/RomothequeManager/issues/50) | **Pre-release de test non signée**, pas de stable/installateur, de compatibilité SteamOS validée ni de gestion réelle de ROM ; recette UX détaillée encore incomplète |

**Lire l'[état détaillé des fonctionnalités](docs/product-status.md)** pour distinguer code intégré, prototypes, tests réalisés et capacités futures. La [roadmap](docs/roadmap.md) expose les orientations sans annoncer de date de livraison.

## Télécharger et lancer le POC Windows (Pre-release de test)

1. Sur un PC **Windows x64**, télécharger le [ZIP V2-01C](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip) et son [fichier `.sha256`](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip.sha256) depuis la [Pre-release du 23.10.2026](https://github.com/TobbyCy/RomothequeManager/releases/tag/poc-v2-01c-20261023-4cf475fbd1ec). Le paquet contient le runtime .NET : **aucun SDK ou runtime .NET à installer**.
2. Dans PowerShell, vérifier l'empreinte du ZIP avec `Get-FileHash -Algorithm SHA256 .\RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip` et la comparer au fichier `.sha256` ainsi qu'à la valeur attendue ci-dessous.
3. **Extraire entièrement** le ZIP dans un dossier utilisateur de son choix (ne pas lancer l'exécutable depuis l'archive).
4. Dans le dossier extrait, lancer `RomothequeManager.V2.exe` sans droits administrateur. Le binaire n'est pas signé : Windows SmartScreen peut afficher un avertissement ; ne pas désactiver les protections globalement.

**SHA-256 du ZIP publié :** `22328d682b7c93edcdeeb735c9913880e68dec64c54b159bd6e6de77bcc7eaaf` — source `4cf475fbd1ece16b138403d3bd967a56d295ee54`.

Les vues **Dashboard, Collection, Fiche jeu et Bibliothèques** présentent **six jeux et trois bibliothèques fictifs**. Tobby a [confirmé un premier lancement et une utilisation générale réussis](https://github.com/TobbyCy/RomothequeManager/issues/50#issuecomment-6381903496) ; les scénarios UX détaillés clavier/focus/scroll/redimensionnement ne sont **pas** attestés individuellement. Le raccordement ES-DE, la persistance, les jaquettes réelles, la gestion de collections personnelles et les opérations physiques restent à développer.

## Découvrir le prototype interactif

Le prototype se consulte **sans installer Node.js ni lancer PowerShell**. Ses bibliothèques, jeux, fichiers et observations sont fictifs ; il n'effectue aucun scan, transfert, export, effacement ou connexion à une collection réelle.

**Pour l'ouvrir :**

1. Récupérer les **sources du dépôt** avec Git ou via **Code → Download ZIP** sur GitHub (il s'agit du code source, **pas** d'un ZIP applicatif Windows).
2. Conserver ensemble les fichiers du dossier `prototype/ui11/`.
3. Ouvrir `prototype/ui11/index.html` par double-clic dans un navigateur moderne, en `file://`. C'est la **démonstration historique** fondée sur des fixtures et un comparateur JavaScript synthétique.
4. Pour le parcours plus complet **Dashboard → Bibliothèques → Comparaison → Collection → Fiche**, ouvrir plutôt `prototype/ui11/demo-integree.html`. Cette démonstration distincte utilise des projections et un catalogue **construits manuellement sur des données fictives**, et non un export PowerShell réel.

Les deux démonstrations sont locales, sans serveur, CDN ni compte à créer. Le détail des modes et de leurs limites figure dans le [guide du prototype](prototype/ui11/README.md).

### Ce que montrent les écrans

| Vue | À observer dans la démonstration |
| --- | --- |
| **Dashboard** | Vue d'ensemble des bibliothèques, plateformes et états connus ou inconnus |
| **Bibliothèques** | Disponibilité, fraîcheur, index accessible ou non, dernier snapshot et dernier inventaire complet vérifié |
| **Comparaison** | Sélection de générations A/B, filtres, six classifications et limites de couverture ; « non retrouvé » ne signifie jamais « supprimé » |
| **Collection et fiches** | Titres fictifs, filtres, variantes, provenance datée et observations d'unités, sans modification possible |

Les **captures d'écran officielles** seront insérées après recette du prototype dans un navigateur réel et contrôle des données représentées. Aucune image de bibliothèque privée, jaquette tierce ni maquette conceptuelle présentée comme un écran fonctionnel n'est publiée dans cette version.

## Préserver avant d'exporter : E1 → E6

Le projet sépare volontairement l'observation d'une collection de toute action sur ses fichiers.

| Étape | Intention | Disponibilité |
| --- | --- | --- |
| **E1 — Inventorier** | Lire et décrire une source, ses identités et ses métadonnées, sans la modifier | Code technique et tests synthétiques présents ; **pas d'utilisation réelle autorisée** |
| **E2 — Sélectionner** | Choisir de manière explicite jeu, variante et unité | Conception en cours, parcours utilisateur non livré |
| **E3 — Rassembler les dépendances** | Identifier disques, fichiers associés et médias | Prévu |
| **E4 — Vérifier** | Signaler collisions, ambiguïtés, chemins dangereux et incohérences | Prévu |
| **E5 — Simuler** | Préparer un plan contrôlable avant une copie | Prévu |
| **E6 — Copier** | Exécuter un export physique après validation explicite | **Non implémenté pour un usage public et non autorisé** |

L'intégrité de la collection d'origine et la capacité d'expliquer les décisions restent prioritaires. Une fonction décrite, un test synthétique réussi ou une PR fusionnée **ne constituent pas une autorisation d'agir sur un disque personnel**.

## Code, qualité et limites de validation

Le dépôt contient un moteur historique **Windows PowerShell 5.1**, des modules de projection de données, un prototype web **HTML/CSS/JavaScript natifs** et les sources du **POC Windows .NET 10 / Avalonia 12.1.4**. Le SDK développeur est épinglé exactement à **10.0.401** par `global.json` (`rollForward=disable`) et provisionné explicitement dans la CI .NET. Les tests automatisés utilisent des fixtures fictives. Ils vérifient notamment le rejet de données invalides, les contrôles de chemins et des parcours d'interface en environnement simulé.

La [CI GitHub Actions](https://github.com/TobbyCy/RomothequeManager/actions) exécute les contrôles applicables selon la nature des changements, avec un **gate final obligatoire**. Chaque run est lié à son **SHA source exact** : une CI verte, prise isolément, ne prouve pas une recette sur des bibliothèques réelles ou un poste externe. La **disponibilité actuelle du ZIP** est établie distinctement par la [Pre-release publiée](https://github.com/TobbyCy/RomothequeManager/releases/tag/poc-v2-01c-20261023-4cf475fbd1ec).

**Repère historique (23.10.2026, avant publication) :** le [run `push main` #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607) était **SUCCESS** sur `408d6263…` après la [PR #45](https://github.com/TobbyCy/RomothequeManager/pull/45) ; il ne démontrait pas à lui seul un essai sur PC externe. Les quatre anciens artefacts ZIP Actions avaient été [retirés et contre-vérifiés](https://github.com/TobbyCy/RomothequeManager/issues/44#issuecomment-6359600451) et les nouveaux ZIP de cette **phase préliminaire** restaient confinés au runner. **Depuis, le ZIP V2-01C a été publié dans une Pre-release contrôlée**. La configuration métier définitive, la persistance, SteamOS et l'exploitation de collections réelles demeurent non validées.

## Repères dans le dépôt

| Chemin | Contenu |
| --- | --- |
| `Modules/`, `Export/`, `Preflight/` | Noyau PowerShell, inventaire E1 et contrôles préalables |
| `prototype/ui11/` | Interface web et démonstrations sur fixtures fictives |
| `src/RomothequeManager.V2/`, `tests/dotnet/` | Sources et tests du POC Avalonia V2-01C ; exécutable synthétique proposé dans la [Pre-release Windows](https://github.com/TobbyCy/RomothequeManager/releases/tag/poc-v2-01c-20261023-4cf475fbd1ec) |
| `tests/` | Tests, canaris de refus et outils de validation synthétique |
| [`docs/product-status.md`](docs/product-status.md) | État réel du produit et limites des preuves |
| [`docs/roadmap.md`](docs/roadmap.md) | Fonctionnalités futures et critères de sortie |
| [`docs/ux/README.md`](docs/ux/README.md) | Parcours et intentions d'interface |
| [`docs/user/lexique.md`](docs/user/lexique.md) | Vocabulaire des collections et des états |
| [`docs/user/securite-et-limites.md`](docs/user/securite-et-limites.md) | Règles de prudence et cas non autorisés |
| [`docs/developer/README.md`](docs/developer/README.md) | Démarrage développeur et références techniques |
| [`CHANGELOG.md`](CHANGELOG.md) | Historique des changements de code, distinct des versions distribuées |

## Contribuer et signaler un problème

Les contributions au **code, aux tests et à la documentation** sont bienvenues : consulter [CONTRIBUTING.md](CONTRIBUTING.md), ouvrir une [Issue](https://github.com/TobbyCy/RomothequeManager/issues) et préciser comportement attendu, résultat observé et contrôles réellement exécutés. Les reproductions et captures doivent utiliser **exclusivement des données inventées**.

Pour une question générale, lire [SUPPORT.md](SUPPORT.md). Pour une vulnérabilité, consulter [SECURITY.md](SECURITY.md) et n'utiliser le formulaire privé de GitHub **que s'il est effectivement disponible**. Ne jamais publier d'exploit, de secret, de chemin personnel ni de collection réelle dans une Issue ou une PR.

## Licence, marque et contenus de jeux

Le **code original** et la **documentation originale dont la diffusion est autorisée** sont proposés sous **licence MIT** : [LICENSE](LICENSE) — **Copyright (c) 2026 Cyril Tobler**. Le projet est maintenu par [TobbyCy](https://github.com/TobbyCy).

L'identité ROMothèque Manager (nom, cartouche rétro stylisée, étagère et flèche rouge) est présentée dans [BRANDING.md](BRANDING.md). **Les logos et icônes officiels ont des droits distincts de la licence MIT du code.** Le bandeau graphique V2 n'est pas inclus ici tant que ses droits de redistribution publique et les contrôles de sécurité des fichiers ne sont pas validés.

ROMothèque Manager **ne fournit, n'héberge ni ne distribue de ROM, BIOS, firmware propriétaire ou jeu commercial**, ni de liens de téléchargement de jeux protégés. L'utilisateur doit disposer des autorisations applicables à ses contenus ; détenir un jeu original ne donne pas automatiquement le droit de télécharger une ROM depuis une source tierce. Le projet est indépendant des fabricants et éditeurs de jeux. Pour les nuances juridiques, voir [LEGAL.md](LEGAL.md).

---

**ROMothèque Manager distribue une Pre-release Windows x64 historique strictement synthétique et propose des démonstrations web locales. Le code `main` comprend maintenant NEXT-01-A (import ES-DE) et NEXT-02 (inventaire), uniquement validés sur fixtures synthétiques : aucune fonction de scan de ROM personnelle, d'import ES-DE ou d'opération physique n'est accessible dans ce ZIP ni autorisée en conditions réelles.**
