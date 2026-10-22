**STAB-03A / #27 (Draft, non intégré)** : corrections synthétiques des helpers/fixtures et des gardes CI signalées par AGENT-05. Les tests sur le nouveau HEAD, Windows PowerShell 5.1, Node et GitHub Actions sont **NOT_EXECUTED / READY_FOR_AGENT_RETEST** ; aucune fusion ni validation post-merge. Voir les commentaires de #27 et Pilotage #69.

# État des fonctionnalités

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
