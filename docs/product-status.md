# État des fonctionnalités

> **Référence historique DOC-GOV-01 — 23 octobre 2026 :** dépôt Produit **PUBLIC**, `main=408d6263be6ae1fbc77c31f6eb9fb530cb4f76e6` (avant V2-01B et BRAND-01). Les [PR #42](https://github.com/TobbyCy/RomothequeManager/pull/42) (POC Windows V2-01A) et [#45](https://github.com/TobbyCy/RomothequeManager/pull/45) (CI-DOTNET-01) sont **fusionnées**. La [CI `push main` #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607) est **COMPLETED / SUCCESS** sur ce SHA, y compris build/tests .NET, smoke de fenêtre Avalonia et `ci-policy-gate`. Cette preuve est propre à ce commit ; elle n'atteste pas une Release ni un usage réel.

Le terme **fonctionnel** désigne ici un POC exécuté et testé **sur fixtures fictives**, pas un produit prêt pour des bibliothèques personnelles. Le code intégré, la validation CI et la disponibilité d'un binaire distribué sont trois états distincts.

## V2-01C — packaging Windows candidat (aucune distribution)

La première phase de l'[Issue #50](https://github.com/TobbyCy/RomothequeManager/issues/50) autorisait **uniquement la préparation technique** d'un ZIP autonome `win-x64` et de son fichier SHA-256. Le workflow construit le paquet avec le SDK exact et les dépendances verrouillées, inscrit un manifeste `POC-MANIFEST.json` (`syntheticOnly=true`, quatre vues, six jeux et trois bibliothèques fictives), vérifie le ZIP puis le réextrait dans un autre dossier et lance l'exécutable depuis cette extraction. Le résultat reste dans `RUNNER_TEMP`, **sans upload, GitHub Pre-release ou Release**.

Cette CI n'atteste pas encore un démarrage sur un Windows x64 propre sans runtime/SDK, ni les parcours interactifs clavier, focus, défilement, ComboBox et redimensionnement de [UXZIP-01 à UXZIP-12](https://github.com/TobbyCy/RomothequeManager/issues/50). Les licences/notices tierces et la recette externe restent à valider. Le [guide packaging](developer/v2-01c-packaging.md) précise la vérification et le blocage de publication.

## DIST-ZIP-01 — préparation de distribution de test (pas encore livrée)

Après fusion de la [PR #53](https://github.com/TobbyCy/RomothequeManager/pull/53) sur `main=31579350033c22cd3c55e156d29a4da7f18ad822`, Tobby a donné **GO_PREPARE_TEST_DISTRIBUTION**, pas le GO final d'upload. Une voie manuelle `workflow_dispatch` est préparée : `validate` (ZIP et SHA-256 éphémères, contrôlés depuis extraction) puis `publish` (pré-release de test uniquement après hash/commit/tag et variables d'approbation exacts). Aucune exécution PR/push n'autorise un upload.

**Séparation des gates :** `validate` peut construire et contrôler le candidat complet avec les notices source présentes, puis journaliser manifeste exact et SHA-256 **sans approbation juridique préalable**. Si le marqueur DEV 2 manque, l'état reste explicitement `LEGAL_HOLD` et aucun upload n'est effectué. **`publish` échoue avant tout upload** sans marqueur juridique, quatre variables d'approbation propriétaire et SHA source/ZIP/tag concordants. Les notices source manquantes restent bloquantes, et la PR #56 doit être intégrée avant l'essai du paquet complet. Revue Agent, validation juridique, approbation finale et UXZIP-01–12 restent à faire ; **aucun ZIP n'est actuellement téléchargeable**. Voir [guide packaging et distribution](developer/v2-01c-packaging.md).

## V2-01B intégré — Bibliothèques et snapshots

La [PR #48](https://github.com/TobbyCy/RomothequeManager/pull/48), issue du mandat [#47](https://github.com/TobbyCy/RomothequeManager/issues/47), a été **fusionnée** sur `main=eda249cd95327cb4bbba27350898513070eca62b`. Elle ajoute la quatrième vue Avalonia « Bibliothèques & snapshots » : **trois bibliothèques liées aux six jeux fictifs existants**, liste/inspecteur, snapshots datés, états de disponibilité et de fraîcheur, et sélection A/B sur résultats préqualifiés. Les sept scénarios PowerShell historiques restent des vecteurs de test indépendants, non additionnés au Dashboard.

**Intégré mais non distribué :** ce POC reste limité aux fixtures synthétiques. Aucun scan, collection réelle, moteur de comparaison C#, PowerShell lancé par l'UI, stockage ou Release n'est autorisé. Les compteurs inconnus restent `null`, les absences observées ne prouvent aucune suppression et le dernier complet vérifié reste distinct des tentatives partielles.

### Traçabilité de la fixture Windows V2-01B (POC synthétique intégré)

**`UI-only / handcrafted deterministic demo / NOT_ENGINE_VERIFIED`.** Le JSON embarqué `src/RomothequeManager.V2/Fixtures/library-ui-v2-01b.synthetic.json` est une démonstration fictive **figée et révisable**, non une sortie vérifiée du producteur PowerShell. Les six `syn-001…syn-006` restent répartis en trois `libraryId` (`lib-a/b/c`) ; les sept vecteurs PS demeurent séparés et ne constituent pas sept bibliothèques à afficher. Aucun résultat de classification de cette fixture ne doit être attribué au moteur PS5.1.

Les **oracles indépendants** des champs `counts`, `entries` et `countDenominator` sont codés en dur dans `ThreeFrozenUiOraclesAreIndependentOfFixtureCounts` (MSTest), puis confrontés au JSON :

| Bibliothèque | A → B synthétiques | Couverture | A∪B | Résultats attendus |
| --- | --- | --- | ---: | --- |
| `lib-a` | `a-complete-20261019` → `fictif-2026-10-20` | INCOMPLETE | 2 | UNKNOWN 2 |
| `lib-b` | `b-complete-20261017` → `fictif-2026-10-18` | COMPLETE | 2 | UNCHANGED 1, NOT_COMPARABLE 1 (conflit local d'identité) |
| `lib-c` | `c-complete-20261021` → `fictif-2026-10-22` | COMPLETE | 4 | ADDED 1, CHANGED 1, UNCHANGED 1, NOT_OBSERVED_IN_B_COVERED_SCOPE 1 |

Ces dénominateurs sont des **unions d'unités logiques observées**, jamais des nombres de fichiers physiques supprimés. Les tests de qualification imposent la couverture exacte des partitions `console × sourceKey × rootKey × domain`, vérifient les références A/B et leurs scopes et rejettent les preuves absentes, étrangères ou dupliquées. Le rendu masque l'historique détaillé lorsque `indexAccessible=false` : seule une référence mémorisée explicitement non consultable peut être montrée.

**Limite UX volontaire du POC :** `INTERACTIVE_KEYBOARD_SCROLL_NOT_TESTED`. Le smoke CI Windows atteste l'ouverture des quatre vues et l'exercice de l'état A/B, **pas** un parcours physique clavier/ComboBox, le maintien du focus ou du scroll, ni la vérification humaine à largeur réduite. À suivre séparément après intégration si le Contrôleur le décide.

## Capacités réellement intégrées

| Domaine | État intégré et preuves | Limites actuelles |
| --- | --- | --- |
| Moteur Windows PowerShell 5.1 / inventaire E1 | Sources et contrôles synthétiques présents ; CI-0 à CI-4 et Pester applicables **SUCCESS** dans le [run #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607) | Aucun scan ni modification de collection réelle autorisé |
| Prototype web `prototype/ui11/` | Dashboard, Bibliothèques, comparaison A/B, Collection et fiches sur données fictives ; smoke Node et Edge headless `file://` **SUCCESS** dans ce run | Pas de persistance, ni de connexion à une collection réelle ; Edge headless n'est pas une recette graphique humaine |
| Bibliothèques / snapshots / catalogue PS → JSON → JS | Producteur PowerShell, projections, comparateur à six classes et consommateur JS intégrés ; canaris et fixtures synthétiques exécutés en CI | DTO 0.2 encore candidat ; l'état OFFLINE/STALE ou un index inaccessible ne prouve jamais une suppression |
| **POC Windows V2-01A/B** | `src/RomothequeManager.V2/` intégré via **PR #42 et #48** ; .NET 10, Avalonia 12.1.4, **quatre vues** Dashboard / Collection-recherche / Fiche / Bibliothèques-snapshots avec A/B, **six jeux fictifs et trois bibliothèques fictives** ; **31 MSTest** et smoke réel de fenêtre Windows à quatre vues dans la CI de #48 | Lecture seule sur fixtures UI, résultats A/B préqualifiés et non vérifiés par le moteur ; pas de scan, stockage, réseau métier, export, import de ROM ou essai utilisateur sur PC externe |
| SDK et restauration .NET | `global.json` impose **SDK 10.0.401 exact**, `rollForward=disable` ; locks NuGet committés, restore verrouillé ; [PR #45](https://github.com/TobbyCy/RomothequeManager/pull/45) ajoute `actions/setup-dotnet` épinglé avant les contrôles | Le run post-fusion prouve le succès du provisionnement sur son runner, **pas encore** le scénario reproductible d'une image initialement dépourvue de 10.0.401 (suivi [#43](https://github.com/TobbyCy/RomothequeManager/issues/43)) |
| ZIP Windows / Release | Candidat V2-01C `win-x64` **self-contained** et fichier SHA-256 construits, recontrôlés et extraits dans `RUNNER_TEMP` ; smoke Windows des quatre vues depuis les fichiers extraits ; aucun `upload-artifact` ; anciens artefacts [purgés](https://github.com/TobbyCy/RomothequeManager/issues/44#issuecomment-6359600451) | **Aucun ZIP téléchargeable, installateur, Pre-release ou Release** ; essai Windows externe et notices tierces encore à qualifier ; des copies téléchargées avant la purge ne peuvent être effacées par GitHub |
| Licence et visuels | Licence MIT pour le code original et la documentation autorisée ; `LEGAL.md` et `BRANDING.md` publics ; bandeau PNG officiel V2 ajouté dans BRAND-01 avec autorisation propriétaire | Le bandeau reste sous droits graphiques réservés, distincts de MIT ; autres SVG/ICO non distribués ; aucun droit sur ROM/BIOS/jeux tiers |

## Travail futur ou non livré

- **Raccordement des bibliothèques réelles à l'interface Windows :** non implémenté dans V2-01B ; la vue intégrée utilise exclusivement des fixtures fictives et aucun moteur de comparaison métier C#.
- **Bibliothèques personnelles, favoris, tags, persistance et synchronisation :** non implémentés dans le POC Windows.
- **Export E2 à E6, conversion, copie et opérations physiques :** conception uniquement ; aucune action réelle autorisée.
- **Distribution Windows, USB, essais externes, compatibilité SteamOS/Linux/ARM :** non validés. L'obtention d'une CI verte ne lève pas l'interdiction de Release.

La [roadmap](roadmap.md) décrit les intentions. Le [CHANGELOG](../CHANGELOG.md) distingue les intégrations des anciens états de sprint. Les sections suivantes conservent les notes historiques **à leur date de rédaction** ; elles ne remplacent pas le tableau courant.

## Historique de construction du prototype

Les paragraphes ci-dessous conservent la trace des anciens sprints au moment de leur conception ; leurs libellés de validation n'attestent pas les tests du `main` publié.

**Sprint #13 (historique de la PR #28, désormais fusionnée)** : ajout d'un adaptateur de DTO `0.1.0-synthetic` pour le registre et les comparaisons précalculées de #27. Deux modes explicitement séparés, sans moteur métier JS en mode projection. Aucune preuve de parité runtime PS5.1 ni navigateur réel.


**Sprint #15 (historique de la PR #28, désormais fusionnée)** : Collection projetée et fiche de jeu synthétique candidate, filtre plateforme, provenance, variantes et observations ROM séparées, navigation vers snapshots. Adaptateur aligné sur `countKnown=false/observedUnitCount=null` de #27. Contrôles V8 simulés exécutés ; Node/navigateur/PS5.1 non exécutés. Le catalogue a ensuite été intégré via la PR #29 ; le DTO 0.2 reste un contrat candidat, sans adoption finale.
