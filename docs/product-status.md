# État des fonctionnalités

Ce tableau distingue les **sources intégrées dans le dépôt public**, les prototypes consultables et les versions **effectivement distribuées**. Code présent ne signifie ni validation d'exécution réelle ni adoption des contrats candidats.

## État GitHub au 22 octobre 2026

Le dépôt Produit existant est **PUBLIC**. Après intégration de la PR #34, la base `main` contrôlée est `f3f13ac5596422b3550c2054318950f6f4fc398f` (22 octobre 2026).

Les PR **#26 à #34 sont fusionnées** ; la PR de traduction **#25 est fermée sans fusion**. La PR documentaire **#36 reste candidate à revue et non fusionnée**. Les sources, la licence [MIT](../LICENSE), [LEGAL](../LEGAL.md) et [BRANDING](../BRANDING.md) sont présentes dans `main` ; la publication du dépôt n'est ni une Release ni une preuve d'exécution sur collections réelles.

Le run historique [#39807003399](https://github.com/TobbyCy/RomothequeManager/actions/runs/39807003399) était **FAILURE** sur l'ancien `main` ; il ne décrit plus l'état actuel. Après correction des tests Python et des fixtures PS5.1 (Issues #2 et #37), le [run PR #34 #39828359155](https://github.com/TobbyCy/RomothequeManager/actions/runs/39828359155) est **SUCCESS** sur le HEAD `039ddd51495950ac7d1f515d94d4f56f3f09a68e`, avant sa fusion. Le [run `push main` #39829504202](https://github.com/TobbyCy/RomothequeManager/actions/runs/39829504202) était **encore en cours lors de cette actualisation** ; le run et les gates de la PR #36 doivent être vérifiés séparément sur leur propre SHA. Ces succès restent **synthétiques** : ils ne prouvent pas une utilisation sur collection réelle, un contrôle Edge graphique, une Release ou l'adoption du DTO 0.2 / de la stack Windows.

| Domaine | État vérifiable | Limite |
| --- | --- | --- |
| Moteur PowerShell 5.1 / inventaire E1 | Sources intégrées dans `main` ; validation runtime de bout en bout encore à établir pour l'arbre publié | Aucun scan de données réelles autorisé |
| CI publique — PR #31 et correctifs #34 | Workflow et gardes fail-closed intégrés ; [CI de #34 réussie](https://github.com/TobbyCy/RomothequeManager/actions/runs/39828359155) sur son HEAD source, 30 tests Python, refus de chemins et suites synthétiques applicables | Vérifier distinctement le `push main` et la CI de #36 après synchronisation ; ne pas transférer un PASS à un autre SHA |
| Pester 4.10.1 et gardes CI | Sur le dernier HEAD de PR #34 : **42/42 tests métier PASS**, canaris de découverte, couverture et refus PASS ; contrôle exact des 22 modules, mesure de couverture sur **18 historiques + Export E1** | Résultats de synthèse sur ce commit précis, pas une mesure de la couverture des quatre extensions ni une preuve d'usage sur ROM réelles |
| Dashboard, Collection, fiches | **Prototype HTML/JS intégré via #28 et #30**, consultable en local sur fixtures fictives : dashboard, catalogue, fiches et filtres ; contrôles Node/DOM simulé et import synthétique exécutés en CI de #34 | Aucun installateur ni accès ROM réel ; **navigateur Edge graphique non exécuté**, parité complète sur données réelles non certifiée |
| Bibliothèques, Offline/Stale, snapshots | **PR #28 fusionnée** : bibliothèques fictives, historique, sélecteur A/B ; correctifs de fixtures de #34, **46 assertions PS5.1 synthétiques PASS** sur le HEAD de #34 | Aucun inventaire réel ; séparation index inaccessible, doublons et fraîcheur maintenue, parité navigateur réel non certifiée |
| Catalogue synthétique partagé — PR #29 | **PR #29 fusionnée** : producteur/validateur PS et consommateur JS du **DTO 0.2 candidat** intégrés ; T1, producteur Unicode et contrôles Node synthétiques exécutés sur le HEAD de #34 | Contrat 0.2 **non adopté définitivement** ; pas de validation de toute la chaîne dans un navigateur réel |
| Projection de bibliothèques — Issue #10 | Module PowerShell, projection et export JSON synthétique intégrés ; contrôles PS5.1 et fixtures de projection exécutés sur le HEAD de #34 | Aucun support réel analysé, pas de déduction de suppression ni de disponibilité actuelle depuis un snapshot ancien |
| Comparaison de snapshots — Issue #9 | Composant PowerShell avec six classifications, identités ordinales sensibles à la casse ; canaris et assertions de comparaisons synthétiques exécutés sur le HEAD de #34 | Aucune suppression physique inférée, aucune opération sur ROM réelles |
| Favoris et tags | Besoins étudiés, non implémentés | Aucun stockage personnel disponible |
| Simulation/export et conversion | Études fonctionnelles, **opérations réelles non autorisées** | Aucun effet physique implicite, aucun E6 |
| ZIP Windows / Release | **NON PUBLIÉ** (aucune GitHub Release) | Pas d'installateur ni de ZIP autonome Windows à télécharger |
| Licence et médias | [MIT](../LICENSE) / `SPDX-License-Identifier: MIT` adoptée pour le code et la documentation originaux dont le titulaire détient les droits | ROM, BIOS, composants tiers et éléments graphiques de provenance distincte exclus ; cinq visuels en attente de validation |

Seules des preuves de tests exécutés sur un SHA exact permettent de déclarer un contrôle réussi. Les [changements](../CHANGELOG.md) documentent les versions intégrées ; la [roadmap](roadmap.md) décrit les objectifs futurs.


## Historique de construction du prototype

Les paragraphes ci-dessous conservent la trace des anciens sprints au moment de leur conception ; leurs libellés de validation n'attestent pas les tests du `main` publié.

**Sprint #13 (historique de la PR #28, désormais fusionnée)** : ajout d'un adaptateur de DTO `0.1.0-synthetic` pour le registre et les comparaisons précalculées de #27. Deux modes explicitement séparés, sans moteur métier JS en mode projection. Aucune preuve de parité runtime PS5.1 ni navigateur réel.


**Sprint #15 (historique de la PR #28, désormais fusionnée)** : Collection projetée et fiche de jeu synthétique candidate, filtre plateforme, provenance, variantes et observations ROM séparées, navigation vers snapshots. Adaptateur aligné sur `countKnown=false/observedUnitCount=null` de #27. Contrôles V8 simulés exécutés ; Node/navigateur/PS5.1 non exécutés. Le catalogue a ensuite été intégré via la PR #29 ; le DTO 0.2 reste un contrat candidat, sans adoption finale.
