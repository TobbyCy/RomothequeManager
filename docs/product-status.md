**PUB-01A / Préparation CI publique (branche Draft non intégrée)** : proposition CODEOWNERS propriétaire, politique de sécurité des PR/forks (permissions read-only, checkout SHA, événements et dispatch main), tests de refus et découverte fail-closed de la future suite Node du prototype. Documentation : `docs/developer/pub01a-public-ci-readiness.md`. **PAS de publication, pas de protection `main` activée ni de run distant vérifié** ; `CHANGES_REQUIRED_FOR_PUBLICATION` tant que paramètres GitHub, gates et contenu public n'ont pas été réceptionnés.

**STAB-03A / #29 (Draft, non intégré)** : corrections synthétiques des helpers/fixtures et des gardes CI signalées par AGENT-05. Les tests sur le nouveau HEAD, Windows PowerShell 5.1, Node et GitHub Actions sont **NOT_EXECUTED / READY_FOR_AGENT_RETEST** ; aucune fusion ni validation post-merge. Voir les commentaires Produit et la traçabilité de gouvernance non publique.

# État des fonctionnalités

**Lot T4-A (PR #29 Draft, non intégré) :** producteur et validateur 0.2 candidats durcis (preuves par plateforme, snapshot le plus récent, unités et compteurs, sérialisation ordinale), tests négatifs PS5.1/Node branchés sur CI-0 mais **NOT_EXECUTED**, code JS de lecture défensive sur #29. Correctif préparatoire de deux helpers #27 porté uniquement par #29, à réconcilier avec la branche #27. Contrat T2 #30 en revue, adoption normative interdite sans décision. Voir `docs/architecture/catalogue-t4-parity-and-integration.md`.

**Branche T3 expérimentale PR #29 (non intégrée) :** `Modules/CatalogueProjection.psm1` et `Modules/CatalogueDtoContract.psm1` fournissent projection synthétique, recherche, DTO candidat `0.2.0-ui-catalogue-candidate`, validateur indépendant et harness négatifs. PR #30 (T2) ne consomme pas encore ce schéma ; PS5.1/CI/Node **NOT_EXECUTED**. Voir `docs/architecture/catalogue-t3-canonical-candidate.md`. Aucune fonctionnalité nouvelle annoncée comme disponible sur main.

Ce tableau distingue les capacités en cours de revue, les maquettes et les versions **effectivement disponibles**.

| Domaine | État vérifiable | Limite |
| --- | --- | --- |
| Moteur PowerShell 5.1 / inventaire E1 | Sources historiques présentes sur le main Produit (intégration privée #24) ; pas de livraison applicative publiée | Exécution et tests métier non revérifiés sur le main intégré ; aucun scan réel autorisé |
| Pester 4.10.1 et gardes CI | Suites à revalider au HEAD effectivement intégré dans ce dépôt | Les succès d'un autre commit ne sont pas transférables |
| Dashboard, Collection, fiches | Conception documentée, **non implémentée** | Aucune interface graphique |
| Bibliothèques, Offline/Stale, snapshots — Sprints #10/#14/#16 | Registre et comparaisons read-only, identités ordinales et partitions sans collision ; exporteur JSON/JS synthétique et vecteurs communs pour UI #28 dans PR #27 | Cas de refus, couverture inconnue, compteurs null si index inaccessible ; **PS51_NOT_EXECUTED**, JSON non généré à l'exécution, Dev 2 doit accepter countKnown=false ; PR empilée non fusionnée |
| Favoris et tags | Besoins étudiés, non implémentés | Aucun stockage personnel disponible |
| Comparaison de snapshots — Issue #9 | Module PowerShell 5.1 autonome en PR Draft ; six états `ADDED`, `CHANGED`, `UNCHANGED`, `NOT_OBSERVED_IN_B_COVERED_SCOPE`, `UNKNOWN`, `NOT_COMPARABLE` ; **couverture complète A ET B** obligatoire pour conclure | Fixtures C1/C2 ajoutées, **NON EXÉCUTÉES / CI_SERVICE_BLOCKED** ; `observationDigest` prouve une observation, jamais un changement des octets ROM ; aucune suppression physique inférée |
| Simulation/export et conversion | Études fonctionnelles, **opérations réelles non autorisées** | Aucun effet physique implicite, aucun E6 |
| ZIP Windows / Release | **NON PUBLIÉ / NON TESTÉ** | Pas de programme à télécharger |
| Licence et médias | Diffusion non décidée, cinq visuels soumis à vérification de droits | Aucune redistribution autorisée par défaut |

Seules des preuves de tests exécutés sur un SHA exact permettent de déclarer un contrôle réussi. Les [changements](../CHANGELOG.md) documentent les versions intégrées ; la [roadmap](roadmap.md) décrit les objectifs futurs.
