# État des fonctionnalités

Ce tableau distingue les **sources intégrées dans le dépôt public**, les prototypes consultables et les versions **effectivement distribuées**. Code présent ne signifie ni validation d'exécution réelle ni adoption des contrats candidats.

## État GitHub au 22 octobre 2026

Le dépôt Produit existant est **PUBLIC**. La version de base vérifiée avant ce correctif documentaire est `main=fc53aa54cb916b89db84580c90707bee87b7842a`.

Les PR **#26, #27, #28, #29, #30, #31, #32 et #33 sont fusionnées** ; la PR de traduction **#25 est fermée sans fusion**. Les sources, la licence [MIT](../LICENSE), [LEGAL](../LEGAL.md) et [BRANDING](../BRANDING.md) sont présentes dans `main` ; la publication du dépôt n'est ni une Release ni une preuve d'exécution sur collections réelles.

La [CI publique du HEAD vérifié (run 39807003399)](https://github.com/TobbyCy/RomothequeManager/actions/runs/39807003399) est **FAILURE**, avec correction du test de politique prise en charge séparément par Dev 1. **Aucun PASS CI, PS5.1, navigateur réel ou parité end-to-end n'est revendiqué pour ce HEAD**. Les validations simulées d'anciens commits restent des preuves historiques, pas un transfert automatique au `main` publié. Le choix de stack applicative Windows et l'adoption normative du DTO 0.2 restent en attente de décision.

| Domaine | État vérifiable | Limite |
| --- | --- | --- |
| Moteur PowerShell 5.1 / inventaire E1 | Sources intégrées dans `main` ; validation runtime de bout en bout encore à établir pour l'arbre publié | Aucun scan de données réelles autorisé |
| CI GitHub publique — PR #31 | **PR #31 fusionnée**, workflow et contrôle de chemins présents dans `main` | Run public sur `main` du 22.10.2026 **en échec** ; corrections Dev 1 en cours, required checks effectifs à confirmer |
| Pester 4.10.1 et gardes CI | Suites à revalider au HEAD effectivement intégré dans ce dépôt | Les succès d'un autre commit ne sont pas transférables |
| Dashboard, Collection, fiches | **Prototype HTML/JS intégré via #28 et #30**, consultable en local sur fixtures fictives : dashboard, catalogue, fiches et filtres | Aucun installateur ni accès ROM réel ; pas de validation Node/Edge de bout en bout établie pour le HEAD public |
| Bibliothèques, Offline/Stale, snapshots | **PR #28 fusionnée** : bibliothèques fictives, historique, sélecteur A/B et comparaison affichée avec données synthétiques | Aucune lecture de données réelles ; parité PowerShell→JSON→UI non certifiée |
| Catalogue synthétique partagé — PR #29 | **PR #29 fusionnée** : producteur/validateur PS et consommateur JS du **DTO 0.2 candidat** intégrés | Contrat 0.2 **non adopté définitivement** ; parité PS5.1→JSON→JS non certifiée sur le HEAD public |
| Projection de bibliothèques — Issue #10 | Module PowerShell et export JSON synthétique intégrés ; comparaisons et historique lisible même hors ligne | PS5.1 et export end-to-end non vérifiés sur cet arbre ; aucune déduction de suppression |
| Comparaison de snapshots — Issue #9 | Composant PowerShell de comparaison synthétique intégré ; six classifications et identité ordinale sensible à la casse | Tests PS5.1 du nouvel arbre non certifiés ; aucune suppression physique inférée |
| Favoris et tags | Besoins étudiés, non implémentés | Aucun stockage personnel disponible |
| Simulation/export et conversion | Études fonctionnelles, **opérations réelles non autorisées** | Aucun effet physique implicite, aucun E6 |
| ZIP Windows / Release | **NON PUBLIÉ** (aucune GitHub Release) | Pas d'installateur ni de ZIP autonome Windows à télécharger |
| Licence et médias | [MIT](../LICENSE) / `SPDX-License-Identifier: MIT` adoptée pour le code et la documentation originaux dont le titulaire détient les droits | ROM, BIOS, composants tiers et éléments graphiques de provenance distincte exclus ; cinq visuels en attente de validation |

Seules des preuves de tests exécutés sur un SHA exact permettent de déclarer un contrôle réussi. Les [changements](../CHANGELOG.md) documentent les versions intégrées ; la [roadmap](roadmap.md) décrit les objectifs futurs.


## Historique de construction du prototype

Les paragraphes ci-dessous conservent la trace des anciens sprints au moment de leur conception ; leurs libellés de validation n'attestent pas les tests du `main` publié.

**Sprint #13 (historique de la PR #28, désormais fusionnée)** : ajout d'un adaptateur de DTO `0.1.0-synthetic` pour le registre et les comparaisons précalculées de #27. Deux modes explicitement séparés, sans moteur métier JS en mode projection. Aucune preuve de parité runtime PS5.1 ni navigateur réel.


**Sprint #15 (historique de la PR #28, désormais fusionnée)** : Collection projetée et fiche de jeu synthétique candidate, filtre plateforme, provenance, variantes et observations ROM séparées, navigation vers snapshots. Adaptateur aligné sur `countKnown=false/observedUnitCount=null` de #27. Contrôles V8 simulés exécutés ; Node/navigateur/PS5.1 non exécutés. Le catalogue a ensuite été intégré via la PR #29 ; le DTO 0.2 reste un contrat candidat, sans adoption finale.
