# État des fonctionnalités

Ce tableau distingue les capacités en cours de revue, les maquettes et les versions **effectivement disponibles**.

| Domaine | État vérifiable | Limite |
| --- | --- | --- |
| Moteur PowerShell 5.1 / inventaire E1 | Sources techniques en PR d'intégration ; pas encore un composant de version publiée | Aucun scan sur données réelles autorisé |
| Pester 4.10.1 et gardes CI | Suites à revalider au HEAD effectivement intégré dans ce dépôt | Les succès d'un autre commit ne sont pas transférables |
| Dashboard, Collection, fiches | **Prototype UI #11 sur PR Draft #28 et extension #30** : Dashboard, Collection avec fiche, inspecteur synthétique, adaptation des catalogues candidats T1 / T3 | Pas d'application Windows finale, pas de fiche enrichie, tests navigateur/CI non exécutés |
| Bibliothèques, Offline/Stale, snapshots | **Prototype UI #11 en PR Draft #28/#30** : bibliothèques fictives, historique, sélecteur A/B, six résultats, filtres et détail | Projection JS synthétique uniquement ; aucune lecture de données réelles, parité PowerShell non validée |
| Favoris et tags | Besoins étudiés, non implémentés | Aucun stockage personnel disponible |
| Simulation/export et conversion | Études fonctionnelles, **opérations réelles non autorisées** | Aucun effet physique implicite, aucun E6 |
| ZIP Windows / Release | **NON PUBLIÉ / NON TESTÉ** | Pas de programme à télécharger |
| Licence et médias | Diffusion non décidée, cinq visuels soumis à vérification de droits | Aucune redistribution autorisée par défaut |

Seules des preuves de tests exécutés sur un SHA exact permettent de déclarer un contrôle réussi. Les [changements](../CHANGELOG.md) documentent les versions intégrées ; la [roadmap](roadmap.md) décrit les objectifs futurs.


**Sprint #13 (PR Draft #28, non fusionnée)** : ajout d'un adaptateur de DTO `0.1.0-synthetic` pour le registre et les comparaisons précalculées de #27. Deux modes explicitement séparés, sans moteur métier JS en mode projection. Aucune preuve de parité runtime PS5.1 ni navigateur réel.


**Sprint #15 (PR #28 Draft, non fusionnée)** : Collection projetée et fiche de jeu synthétique candidate, filtre plateforme, provenance, variantes et observations ROM séparées, navigation vers snapshots. Adaptateur aligné sur `countKnown=false/observedUnitCount=null` de #27. Contrôles V8 simulés exécutés ; Node/navigateur/PS5.1 non exécutés. Le catalogue 0.2 est désormais un consommateur candidat synchronisé dans la PR #30 avec le module partagé de Dev 1. STAB-03B / INT-02 ont corrigé les régressions des anciens tests : **115 contrôles** répartis sur les cinq suites et **42 contrôles de parcours supplémentaire** sur les trois modes ont abouti sous V8 avec API Node et DOM simulées, sans exécution Node native ; **Node natif, Edge et parité avec un JSON réellement exporté par PS5.1 restent NON EXÉCUTÉS ici**. Le contrat n'est pas adopté.
