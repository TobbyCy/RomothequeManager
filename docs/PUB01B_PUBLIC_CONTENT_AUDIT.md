# PUB01B_PUBLIC_CONTENT_AUDIT — préparation, pas autorisation de publication

**État : HOLD_PUBLIC / OWNER_ACTION_REQUIRED.** Audit candidat DEV 2 du 22 octobre 2026. Ce rapport est destiné à rester publiable : il ne reproduit aucun lien, détail ni contenu du dépôt opérationnel privé. Il ne constitue ni un scan exhaustif de l'historique Git ni une attestation juridique.

## Périmètre et preuves

- Arbre Produit sur PR Draft #30 (SHA observé `b62a2c48f8e1e18f5cdab42fb6a15584c8d884a1`) : 110 blobs parcourus pour références explicites au dépôt privé, noms/données personnels évidents, courriels et chemins locaux typiques ; échantillons des guides et de la configuration GitHub lus.
- `main` Produit : 85 blobs au moment du relevé, distinct de la PR #30. Les scripts et modules restent non destinés aux données réelles.
- Métadonnées GitHub consultées : 21 Issues, 9 PR (dont 3 fermées) ; 94 commentaires de discussion GitHub au moment de la recherche, aucun commentaire inline retourné par l'endpoint consulté.
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

**B0 — Historique GitHub hors arbre.** La description de la PR Produit #23 fermée et des commentaires d'anciennes PR contiennent des références au dépôt opérationnel privé ; au moins deux commentaires pointent explicitement vers celui-ci. Le propriétaire doit examiner, corriger/expurger là où les droits GitHub le permettent, et décider d'un traitement du reliquat historique. Une PR fermée, son fil, ses SHAs et ses métadonnées ne deviennent **pas invisibles** après ouverture du dépôt. Pour les IDs précis de commentaires et leur qualification, utiliser la remise privée au propriétaire, sans recopier ici les URL concernées.

**B1 — Historique complet, commentaires et métadonnées.** Valider l'ensemble des branches et références Git, l'auteur des commits, anciennes PR/Issues fermées, discussions/reviews, pièces jointes, Actions, journaux et artefacts. Ne pas promettre qu'une suppression de branche, un squash ou une modification de README efface les références historiques. Aucun nettoyage irréversible automatique.

**B2 — Licence.** Aucun `LICENSE` adopté. Soumettre au propriétaire le périmètre code/docs/assets et un arbitrage de licence (par exemple MIT, Apache-2.0, GPL-3.0, ou report de décision), avec compatibilité éventuelle des composants tiers, sans sélection par DEV 2.

**B3 — Sécurité des contributions.** Confirmer un canal de vulnérabilités privé réellement disponible, permissions/forks, secrets CI, règles de revue, protections réalisables et état des issues templates avant d'accepter une contribution externe.

**B4 — Graphismes et contenus externes.** Ne pas publier les cinq ressources graphiques historiques sans provenance, autorisation et droits vérifiés ; ne pas importer de jaquettes ou marques de tiers par défaut. Favoriser illustrations originales sous autorisation explicite.

**B5 — Véracité produit.** Aucun installateur/ZIP/Release Windows fourni, ni inventaire réel validé, ni export/copie/convertisseur en service. Les PR #28/#30 ne livrent qu'une démonstration hors ligne. Le contrat 0.2 est candidat ; pas de parité complète établie entre exporteur PS5.1, JSON et Edge.

## Décision à solliciter

Le propriétaire valide après remise privée de l'audit des objets GitHub : (1) traitement des historiques exposables, (2) licence/droits des visuels et des contributions, (3) canal sécurité et règles GitHub, (4) tests indépendants, puis (5) une autorisation **explicite et distincte** de rendre PUBLIC le seul dépôt Produit. Aucun de ces points n'est accordé implicitement par ce document.
