# PUB01B_PUBLIC_CONTENT_AUDIT — préparation, pas autorisation de publication

**État : HOLD_PUBLIC / OWNER_ACTION_REQUIRED.** Audit candidat DEV 2 du 22 octobre 2026. Ce rapport est destiné à rester publiable : il ne reproduit aucun lien, détail ni contenu du dépôt opérationnel privé. Il ne constitue ni un scan exhaustif de l'historique Git ni une attestation juridique.

## Périmètre et preuves

- Arbre Produit sur PR Draft #30 (SHA observé `b62a2c48f8e1e18f5cdab42fb6a15584c8d884a1`) : 110 blobs parcourus pour références explicites au dépôt privé, noms/données personnels évidents, courriels et chemins locaux typiques ; échantillons des guides et de la configuration GitHub lus.
- `main` Produit : 85 blobs au moment du relevé, distinct de la PR #30. Les scripts et modules restent non destinés aux données réelles.
- Métadonnées GitHub consultées : 21 Issues ouvertes et 9 PR (dont 3 fermées : deux non fusionnées, une fusionnée) ; 99 commentaires GitHub sur la première page REST (99/100), sans commentaire de revue inline sur cette page, aucun commentaire inline retourné par l'endpoint consulté.
- Dans l'arbre scanné, aucune URL explicite vers le dépôt opérationnel privé n'a été détectée ; deux liens pointent seulement vers des PR du Produit. Ceci **n'efface pas les liens présents dans les textes historiques GitHub**.
- Aucune ressource image/jaquette/logo binaire intégrée dans l'arbre vérifié ; aucun droit de redistribution sur les créations graphiques historiques n'est établi. Les pictogrammes de l'interface sont des placeholders typographiques, non des jaquettes.
- Vérifications non effectuées : tous commits/branches/objets orphelins, pièces jointes historiques, caches, éventuels journaux Actions/artefacts, URL externes en HTTP effectif, contrôle GitHub en interface propriétaire et vérification légale des droits.

## Corrections documentaires préparées

- `README.md` : distingue prototype HTML/JS sur PR Draft et absence d'application distribuable ; référence le contrat 0.2 comme candidat seulement.
- `CONTRIBUTING.md` : contribution synthétique, preuve des tests, tiers et droits de redistribution sans licence choisie.
- `SECURITY.md` : signale explicitement qu'un canal privé de vulnérabilité doit être activé/vérifié avant publication.
- `CHANGELOG.md` et `docs/product-status.md` : séparent le travail non fusionné, tests V8, Node/Edge/PS5.1 encore non exécutés.
- Formulaires GitHub Bug / Feature / Docs / Question et `config.yml` existants : interdisent les données réelles, les liens privés et la création libre sans formulaire. Vérifier leur comportement réel dans l'interface GitHub.

## Blocages avant toute mise en public

**B0 — Historique GitHub hors arbre.** Contrôle complémentaire ciblé des métadonnées : **deux descriptions de PR** (dont une fermée non fusionnée), **quatre commentaires de PR**, soit **sept URL explicites** renvoyant vers le dépôt opérationnel privé. Les identifiants précis des six objets distincts sont consignés exclusivement dans le checkpoint privé du propriétaire, pas dans ce document destiné à la publication. Le propriétaire doit examiner, corriger/expurger là où les droits GitHub le permettent, et décider d'un traitement du reliquat historique. Une PR fermée, son fil, ses SHAs et ses métadonnées ne deviennent **pas invisibles** après ouverture du dépôt. Pour les IDs précis de commentaires et leur qualification, utiliser la remise privée au propriétaire, sans recopier ici les URL concernées.

**B1 — Historique complet, commentaires et métadonnées.** Valider l'ensemble des branches et références Git, l'auteur des commits, anciennes PR/Issues fermées, discussions/reviews, pièces jointes, Actions, journaux et artefacts. Ne pas promettre qu'une suppression de branche, un squash ou une modification de README efface les références historiques. Aucun nettoyage irréversible automatique.

**B2 — Licence.** Aucun `LICENSE` adopté. Soumettre au propriétaire le périmètre code/docs/assets et un arbitrage de licence (par exemple MIT, Apache-2.0, GPL-3.0, ou report de décision), avec compatibilité éventuelle des composants tiers, sans sélection par DEV 2.

**B3 — Sécurité des contributions.** Confirmer un canal de vulnérabilités privé réellement disponible, permissions/forks, secrets CI, règles de revue, protections réalisables et état des issues templates avant d'accepter une contribution externe.

**B4 — Graphismes et contenus externes.** Ne pas publier les cinq ressources graphiques historiques sans provenance, autorisation et droits vérifiés ; ne pas importer de jaquettes ou marques de tiers par défaut. Favoriser illustrations originales sous autorisation explicite.

**B5 — Véracité produit.** Aucun installateur/ZIP/Release Windows fourni, ni inventaire réel validé, ni export/copie/convertisseur en service. Les PR #28/#30 ne livrent qu'une démonstration hors ligne. Le contrat 0.2 est candidat ; pas de parité complète établie entre exporteur PS5.1, JSON et Edge.

## Éléments concrets soumis au propriétaire — licence et identité visuelle

Sans recommander de licence à adopter automatiquement :

| Option indicative | Arbitrage nécessaire |
| --- | --- |
| MIT | Redistribution permissive, obligations de conserver l'avis de licence ; protection explicite de brevet moins détaillée qu'Apache-2.0. |
| Apache-2.0 | Redistribution permissive avec dispositions expresses sur les brevets, avis et NOTICE ; obligations documentaires supplémentaires. |
| GPL-3.0 | Copyleft applicable aux redistributions dérivées ; obligations de mise à disposition du code source correspondant ; compatibilité des dépendances à vérifier. |
| Publication différée sans licence choisie | Consultation du code possible si le dépôt devient public, mais **pas d'autorisation implicite** de redistribution/réutilisation par des tiers ; situation délicate pour accepter des contributions. |

Questions ouvertes : la même licence couvrira-t-elle scripts PS, JS, tests et documentation ? Quels noms/logos seront des marques réservées ? Les images historiques sont-elles originales, sous licence tierce ou sans preuve de droits ? Quel traitement pour les futures contributions externes et leurs licences ? Aucune création graphique exclue n'est autorisée à entrer dans le dépôt du seul fait du choix d'une licence logicielle.

## Décision à solliciter

Le propriétaire valide après remise privée de l'audit des objets GitHub : (1) traitement des historiques exposables, (2) licence/droits des visuels et des contributions, (3) canal sécurité et règles GitHub, (4) tests indépendants, puis (5) une autorisation **explicite et distincte** de rendre PUBLIC le seul dépôt Produit. Aucun de ces points n'est accordé implicitement par ce document.

## PUB01B_CONTENT_STATUS — contrôle complémentaire de consolidation (22.10.2026)

**Statut : CONTENT_REVIEWED / HISTORICAL_METADATA_BLOCKED / HOLD_PUBLIC.** Le contrôle de contenu a été repris sur un arbre Produit #30 fixé au SHA `a18381e21b7e3d65dfd101cfe8ac16e8b32fe48a` : **116 blobs de fichiers** inspectés par recherche de lien explicite vers le dépôt de pilotage privé, URL de repository, courriel, chemins Windows personnels et signatures courantes de jetons. **Zéro référence explicite au dépôt opérationnel privé détectée dans cet arbre**. Deux liens GitHub présents dans le guide du prototype pointent vers des PR du **Produit**, non vers le pilotage. Les courriels détectés dans les scripts sont des identités Git **synthétiques de canaris**, et les motifs de jetons détectés sont des **expressions régulières de détection**, pas des secrets publiés. Les motifs heuristiques ne sont pas une inspection exhaustive des données personnelles ni des antécédents Git.

- **README / CHANGELOG / CONTRIBUTING / SECURITY / docs** : candidats préparés dans la PR #30 ; présentent les fonctionnalités non fusionnées et l'absence de Release ou d'application Windows. Vérifier encore la cohérence après ordre d'intégration #28 puis #30 et les corrections de Dev 1. Aucun test navigateur/PS réel revendiqué.
- **Historique Issues/PR** : relevé des 21 Issues et neuf PR (trois fermées), des descriptions et de 99 commentaires de première page effectué ; sept liens directs vers le dépôt privé subsistent dans deux descriptions de PR et quatre commentaires. Leur inventaire exact a été transmis **dans l'espace confidentiel**, jamais reproduit ici. L'absence de courriel ou chemin local repéré dans cette recherche n'est pas une preuve exhaustive. Étendre le contrôle à chaque pièce jointe, commit, revue, branche, artefact et log.
- **Images / médias** : aucune image binaire (logo, jaquette, capture ; formats usuels) dans cet arbre #30, ni dans les listes de fichiers modifiés des PR consultées #22, #23, #24, #25, #28, #29, #30. Les **cinq visuels historiques** restent exclus tant que provenance et droits de redistribution n'ont pas été prouvés. Les caractères décoratifs du prototype ne constituent pas des licences graphiques.
- **PR Draft #25 — traduction anglaise** : `README.en.md` est un candidat distinct non intégré et dit encore « not implemented » pour des vues en prototype sur #28/#30. Le workflow candidat `readme-translation.yml` appelle une **action externe de traduction**, prévoit des permissions `contents:write` / `pull-requests:write` dans un job, et ne s'exécute que si le dépôt est public et sur `main`. **Faire une revue sécurité et confidentialité préalable** : garanties de non-transmission d'autres contenus, confiance dans la dépendance et nécessité du workflow, cohérence FR/EN et revue humaine ; aucun lancement autonome.
- **Licences / contribution / sécurité** : toujours sans licence autorisée ; décisions propriétaire requises pour code, documentation, noms et médias. Le signalement privé des vulnérabilités et les protections PR/forks/CI doivent être confirmés en conditions réelles, pas uniquement documentés.

**Réception attendue** : traitement de l'exposition historique par le propriétaire (une PR fermée reste visible), contrôle du graphe Git et des assets, licence choisie, canal sécurité et gouvernance Actions/Forks vérifiés, validation technique indépendante, puis **décision explicite de publication** du seul dépôt Produit. Ce complément ne vaut aucune autorisation PUBLIC.

## INT-02 / PUB01B_CONTENT_STATUS — classement de déclassement (22.10.2026)

**Statut : `HOLD_PUBLIC` — le Produit existant reste la seule cible envisagée.** Ce relevé actualise les inventaires ci-dessus, qui correspondent à des photographies antérieures et ne doivent pas être additionnées aux nouveaux chiffres.

### Métadonnées GitHub et antécédents exposables

Contrôle REST actualisé : **21 Issues hors PR, 10 PR** (y compris la nouvelle PR Draft #31 PUB-01A, ainsi que #22/#23 fermées non fusionnées et #24 fusionnée), **106 commentaires** de discussion (deux pages) et **0 commentaire inline** retourné par le point d'accès interrogé. Scan des **312 commits distincts** retrouvés dans l'historique atteignable des **neuf branches actuelles du Produit** : aucun lien textuel explicite vers le dépôt Pilotage dans les messages, aucune empreinte de jeton réelle trouvée par les motifs sélectionnés et aucune adresse d'auteur/committer extérieure aux adresses de non-réponse et de test reconnues. **Ce résultat ne prouve pas** l'absence de renseignements internes dans les diffs anciens, objets Git abandonnés, commentaires masqués, liens résolus par redirection, images, pièces jointes ou artefacts.

**Évolution depuis le précédent audit** : la description de la PR Produit #29 a été nettoyée de ses deux liens directs vers Pilotage ; son statut passe à `ALREADY_CORRECTED` pour **le corps actuel**, pas une garantie d'effacement des caches/historiques. **Cinq URL directes subsistent dans cinq objets Produit** : la description de la PR **#23 fermée**, un commentaire de la PR **#24 fusionnée**, et des commentaires des PR **#26**, **#27**, **#28** ouvertes. Inventaire des IDs de commentaires remis exclusivement dans l'Issue privée du Pilotage ; ce document public ne reproduit aucun lien interne.

| Classe | Élément | Action et autorité |
| --- | --- | --- |
| **BLOQUANT AVANT PUBLIC** | Les cinq références directes à Pilotage dans la description et les discussions GitHub historiques, notamment la PR #23 fermée et #24 fusionnée | Corriger ou expurger les surfaces modifiables via l'interface autorisée ; documenter l'impossibilité technique pour les commentaires d'autres auteurs. Le propriétaire décide comment traiter le reliquat ; une clôture de PR ne l'efface pas |
| **BLOQUANT AVANT PUBLIC** | Audit exhaustif des commits/diffs anciens, anciennes branches, caches, pièces jointes, artefacts GitHub Actions, données utilisateur éventuelles | Approfondir avec droits d'administration ou outils d'audit appropriés, sans force-push/suppression irréversible improvisée |
| **BLOQUANT AVANT PUBLIC** | Canal de signalement privé, permissions Actions et forks, tests réellement bloquants, vérification des règles de protection disponibles | Vérification propriétaire sur le dépôt final et revue Dev 1 de la PR #31 PUB-01A, sans désactiver CI-0 |
| **DÉCISION PROPRIÉTAIRE** | Licence(s) code/docs, noms, images, éventuelles marques, contributions extérieures | Aucun `LICENSE` sans choix explicite et revue des composants tiers ; éviter les cinq visuels historiques non autorisés |
| **SOUHAITABLE NON BLOQUANT** | README anglais et traduction automatisée proposés en PR Draft #25 | Avant réception : aligner l'anglais sur l'état réel des prototypes et revoir la dépendance de traduction externe ; ne pas activer silencieusement cette automation |
| **DÉJÀ CORRIGÉ** | Liens directs de la description actuelle de la PR #29 ; contenus destinés à publication sur la branche #30 | Vérification de la description et scan de l'arbre actuel, sans préjuger du graphe Git historique |
| **DÉJÀ CORRIGÉ SUR BRANCHE, NON INTÉGRÉ** | README FR, CONTRIBUTING, SECURITY, CHANGELOG, statut produit et disclaimer fonctions non disponibles sur #30 | Revalider après integration ordonnée, contrôler les liens effectifs sur `main` |
| **DÉCISION PROPRIÉTAIRE** | Date/GO de visibilité PUBLIC et communication | Autorisation de publication séparée, après les gates indépendants et le tri historique |

### Première préversion expérimentale — proposition, pas engagement de Release

**Nom de travail proposé : `0.1.0-alpha.1`**, sans tag ni adoption. Deux niveaux doivent rester **distincts** :

- **Prévisualisation de source UI (si le Contrôleur souhaite un jalon interne préalable)** : fichiers statiques HTML/CSS/JS, fixtures totalement fictives et README de recette ; requiert un navigateur récent et ne constitue **ni une application Windows portable autonome, ni un moteur d'export opérationnel**. Ne pas appeler son ZIP « installateur » ni « application ».
- **Future préversion Windows portable** : proposer, seulement **après décision de stack et intégration reçue**, un ZIP autonome versionné contenant l'exécutable/lanceur, les dépendances redistribuables permises, documentation, politique sécurité, licences/avis tiers, fixtures de démonstration, `SHA256SUMS.txt`, notes de version, numéro et commit source précis. Aucun prérequis .NET séparé à promettre sans preuve de packaging ; préciser OS/architecture réellement validés, mémoire/disque nécessaires et restrictions de permissions. Définir un comportement fail-closed, sans import réel implicite, conversion/écriture/copie automatique ni accès à des chemins privés.

**Fonctions recevables pour une alpha de consultation une fois intégrées et testées** : Dashboard, bibliothèques, snapshots historiques, comparaison A/B précalculée, Collection/Fiche, filtres, recherche, provenance, variantes et multidisque, OFFLINE/STALE/UNKNOWN, import manuel de **JSON synthétique validé**. **Exclure** : ROM/BIOS, conversion, export E6 réel, enrichissement en ligne, jaquettes tierces, catalogue cloud, favoris persistants, sauvegarde réelle ou support matériel annoncé sans tests.

**Gates avant tag/ZIP/publication** : arbre Git fusionnable contrôlé et main protégé selon droits, CI/Node/contrôles négatifs au SHA intégré, recette navigateur automatisée et Edge Windows indépendante, parité PowerShell 5.1 sur JSON synthétique réellement exporté (Unicode, identités, provenance et couverture), absence de secrets et contenu sensible dans les sources **et l'historique exposable**, licence/assets approuvés, reproductibilité ZIP/empreinte SHA-256 vérifiée depuis un autre environnement, notes détaillant les limitations, puis décision explicite de l'auteur. Le passage du dépôt en PUBLIC n'est pas équivalent à une Release.

## INT-03 — statut de déclassement, delta depuis INT-02

**Photographie des métadonnées : 22.10.2026, avant décision PUBLIC.** Aucun scan exhaustif des objets Git hors des branches actives n'est revendiqué. Le contrôle incrémental des nouveaux commentaires Produit depuis le checkpoint INT-02 a repéré cinq nouveaux commentaires, **sans nouveau lien direct** vers le dépôt privé. Le nombre de **cinq liens directs historiques** identifié dans la description d'une PR fermée et quatre commentaires demeure un blocage ; ne reproduire aucun de ces liens ici.

| Classe demandée | Surface | Critère pour lever l'état |
| --- | --- | --- |
| `PUBLIC_BLOCKER` | Description de la PR Produit **#23 fermée** et quatre commentaires historiques des PR #24, #26, #27, #28 ; cinq liens directs privés | Le propriétaire doit corriger les objets qu'il est autorisé à modifier, consigner ceux non modifiables, puis vérifier leur exposition réelle avant changement de visibilité |
| `PUBLIC_BLOCKER` | Historique des patches, anciennes références Git, artefacts Actions, pièces jointes et journaux ; sécurité de la CI pour les forks | Audit avec permissions adéquates et preuve d'absence d'élément non publiable ; branch protection/gate réellement opérationnels |
| `OWNER_DECISION_REQUIRED` | Licence code/documentation et droits de redistribution des cinq assets privés ; canal de sécurité non public actuellement non vérifié ; GO PUBLIC | Choix et vérifications du propriétaire, aucune adoption ou import automatique |
| `CORRECTION_NON_BLOQUANTE` | README anglais candidat #25 et action de traduction automatique externe | Revoir traduction, dépendance et autorisations avant intégration ; ne bloque pas une publication FR si cette branche reste exclue |
| `CLEARED` (sur périmètre borné) | Description actuelle PR #29 nettoyée, sources README/CONTRIBUTING/SECURITY et HTML fictif sur #30 ; aucun visuel binaire ajouté au Produit | Maintenir cette propriété au HEAD intégré, sans confondre avec un contrôle des historiques ou une recette navigateur réelle |

Les autres éléments sont décrits dans l'audit initial ci-dessus. **PUBLIC reste `HOLD_PUBLIC`** : une PR fermée, un commentaire ou une ancienne pièce jointe ne devient pas privé du seul fait de la fermeture. La cible PUBLIC est exclusivement le dépôt Produit existant.
