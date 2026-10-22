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
