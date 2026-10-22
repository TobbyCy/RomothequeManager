# État des fonctionnalités

Ce tableau distingue les capacités en cours de revue, les maquettes et les versions **effectivement disponibles**.

| Domaine | État vérifiable | Limite |
| --- | --- | --- |
| Moteur PowerShell 5.1 / inventaire E1 | Sources techniques en PR d'intégration ; pas encore un composant de version publiée | Aucun scan sur données réelles autorisé |
| Pester 4.10.1 et gardes CI | Suites à revalider au HEAD effectivement intégré dans ce dépôt | Les succès d'un autre commit ne sont pas transférables |
| Dashboard, Collection, fiches | Conception documentée, **non implémentée** | Aucune interface graphique |
| Bibliothèques, Offline/Stale, snapshots | Critères de conception, contrats/test à finaliser | Support Offline n'implique pas une collection supprimée |
| Favoris, tags, comparaison | Besoins étudiés | Aucun stockage personnel disponible |
| Simulation/export et conversion | Études fonctionnelles, **opérations réelles non autorisées** | Aucun effet physique implicite, aucun E6 |
| ZIP Windows / Release | **NON PUBLIÉ / NON TESTÉ** | Pas de programme à télécharger |
| Licence et médias | **Licence MIT adoptée pour le code et la documentation originaux autorisés** ; cinq visuels V2 soumis à vérification de droits | Publication du dépôt encore privée ; les visuels, marques et œuvres tierces ne sont pas automatiquement couverts par MIT |

Seules des preuves de tests exécutés sur un SHA exact permettent de déclarer un contrôle réussi. Les [changements](../CHANGELOG.md) documentent les versions intégrées ; la [roadmap](roadmap.md) décrit les objectifs futurs.

**Cadre légal :** voir [la licence MIT](../LICENSE), [les limites d'utilisation des ROM et BIOS](../LEGAL.md) et [l'identité graphique](../BRANDING.md). La publication du dépôt et une éventuelle Release Windows restent des décisions distinctes ; aucune application autonome n'est disponible aujourd'hui.
