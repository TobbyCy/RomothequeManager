# État des fonctionnalités

Ce tableau distingue les capacités en cours de revue, les maquettes et les versions **effectivement disponibles**.

| Domaine | État vérifiable | Limite |
| --- | --- | --- |
| Moteur PowerShell 5.1 / inventaire E1 | Sources techniques en PR d'intégration ; pas encore un composant de version publiée | Aucun scan sur données réelles autorisé |
| Pester 4.10.1 et gardes CI | Suites à revalider au HEAD effectivement intégré dans ce dépôt | Les succès d'un autre commit ne sont pas transférables |
| Dashboard, Collection, fiches | **Prototype UI #11 codé sur PR Draft #28** : Dashboard, première Collection consultable, inspecteur synthétique | Pas d'application Windows finale, pas de fiche enrichie, tests navigateur/CI non exécutés |
| Bibliothèques, Offline/Stale, snapshots | **Prototype UI #11 codé sur PR Draft #28** : 4 bibliothèques fictives, historique, sélecteur A/B, six résultats, filtres et détail | Projection JS synthétique uniquement ; aucune lecture de données réelles, parité PowerShell non validée |
| Favoris et tags | Besoins étudiés, non implémentés | Aucun stockage personnel disponible |
| Simulation/export et conversion | Études fonctionnelles, **opérations réelles non autorisées** | Aucun effet physique implicite, aucun E6 |
| ZIP Windows / Release | **NON PUBLIÉ / NON TESTÉ** | Pas de programme à télécharger |
| Licence et médias | [MIT](../LICENSE) / `SPDX-License-Identifier: MIT` adoptée pour le code et la documentation originaux dont le titulaire détient les droits | ROM, BIOS, composants tiers et éléments graphiques de provenance distincte exclus ; cinq visuels en attente de validation |

Seules des preuves de tests exécutés sur un SHA exact permettent de déclarer un contrôle réussi. Les [changements](../CHANGELOG.md) documentent les versions intégrées ; la [roadmap](roadmap.md) décrit les objectifs futurs.


**Sprint #13 (PR Draft #28, non fusionnée)** : ajout d'un adaptateur de DTO `0.1.0-synthetic` pour le registre et les comparaisons précalculées de #27. Deux modes explicitement séparés, sans moteur métier JS en mode projection. Aucune preuve de parité runtime PS5.1 ni navigateur réel.


**Sprint #15 (PR #28 Draft, non fusionnée)** : Collection projetée et fiche de jeu synthétique candidate, filtre plateforme, provenance, variantes et observations ROM séparées, navigation vers snapshots. Adaptateur aligné sur `countKnown=false/observedUnitCount=null` de #27. Contrôles V8 simulés exécutés ; Node/navigateur/PS5.1 non exécutés. Catalogue non encore fourni par le noyau.
