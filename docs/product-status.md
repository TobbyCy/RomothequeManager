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
| Licence et médias | Diffusion non décidée, cinq visuels soumis à vérification de droits | Aucune redistribution autorisée par défaut |

Seules des preuves de tests exécutés sur un SHA exact permettent de déclarer un contrôle réussi. Les [changements](../CHANGELOG.md) documentent les versions intégrées ; la [roadmap](roadmap.md) décrit les objectifs futurs.
