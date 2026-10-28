# État des fonctionnalités

### Preuve binaire du ZIP corrigé (source `e775cdd6`)

- [Workflow Windows #40689889760 — SUCCESS](https://github.com/TobbyCy/RomothequeManager/actions/runs/40689889760), artifact temporaire [#12816457518](https://github.com/TobbyCy/RomothequeManager/actions/runs/40689889760/artifacts/12816457518) (jusqu'au **31.10.2026 12:43 UTC**).
- Source exacte de compilation (figée, malgré les commits documentaires du workflow) : `e775cdd64522d45ee2fb848df232fa1f19023326`, vérifiée par `UX_RECIPE_FROZEN_SOURCE` et `UX-MANIFEST.json`. ZIP applicatif `RomothequeManager-UX-PR64-win-x64-e775cdd6.zip` : **75 362 552 octets**, **250 fichiers**, SHA-256 **`fe6a4d59adfae5ea171c229838d17fdeef4bd23a41e03924d2e5a93f6c407364`**. Le fichier annexe `.zip.sha256` concorde ; le hash a été recalculé indépendamment après téléchargement de l'artifact Actions (dont le digest externe est un autre fichier).
- 62/62 tests .NET PASS, licences/runtime inclus et contrôlés, ZIP extrait/recomparé et smoke de la fenêtre Avalonia PASS. `UX_RECIPE_WINEXE_GUI_PASS subsystem=2` vérifie le PE GUI de l'exécutable **extrait de ce ZIP** ; cela ne prouve pas un double-clic interactif dans l'Explorateur.
- La fixture fictive intégrée permet de contrôler manuellement le résumé de diagnostics en français, la ROM/le média absent et l'échec XML suivi de la conservation du dernier catalogue valide. **Recette Windows humaine toujours requise** avant GO de fusion.
- **CI adaptative** : le premier run sur la mise à jour du workflow a rencontré `CLASSIFICATION_SCOPE_UNPROVEN / MERGE_PARENTS_INVALID` en raison d'un décalage entre le SHA de base de la PR et le HEAD de `main` pendant une intégration concurrente. Il ne s'agit pas d'un PASS de la CI générale : relancer un contrôle complet sur une base cohérente, sans affaiblir le contrôle des parents.
- **Aucune fusion ni Release ;** cette preuve est un artifact de recette temporaire, jamais une nouvelle publication publique.

## NEXT-01-UX — recette du ZIP corrigé PR #64

- Source applicative de référence : `e775cdd64522d45ee2fb848df232fa1f19023326`, comprenant WinExe et diagnostics ES-DE francisés. La prochaine PR de packaging porte uniquement la modification du workflow, ses canaris et la documentation : le ZIP embarque exactement le source applicatif ci-dessus.
- L'ancien artifact figé `cc35abf7` **ne valide pas** les corrections. Le nouveau workflow conserve toutes les barrières de licence, source, SHA-256, interdiction de ROMs et timeout du smoke. Vérification ajoutée : PE subsystem Windows GUI = 2, sur le .exe du ZIP extrait.
- Recette Windows Tobby : double-clic EXE, absence de console, import de fixture fictive et messages FR ROM/média absents, XML volontairement malformé puis reimport/reprise non destructive. Résultat manuel à reporter à la PR.
- Publication ZIP temporaire sous GitHub Actions seulement : **aucune Release**, aucune bibliothèque réelle, aucun GO de merge présumé.


## NEXT-01-UX-FIX — correction suite à recette Windows (PR #64, 28 octobre 2026)

- L'exécutable Avalonia de la PR est configuré en `WinExe`, pour ouvrir la fenêtre sans console au double-clic sous Windows. Le mode `--smoke-window` reste le contrôle automatisé de lancement ; la validation interactive de l'affichage relève de Tobby.
- Le tableau de bord importé donne maintenant des diagnostics français explicites (catégorie à vérifier / information, plateforme, entrée et explication), triés par importance ; les refus d'import / réimport expliquent les problèmes courants (XML invalide, gamelist absente ou ambiguë, dossier inaccessible). Aucun scan supplémentaire, aucune modification de l'adaptateur de lecture seule ni conclusion de suppression.
- Les tests synthétiques supplémentaires vérifient le sens des diagnostics et la conservation du catalogue valide après échec. Validation CI à rattacher au **nouveau HEAD** avant toute décision de merge. L'artifact UX provisoire antérieur, construit sur le SHA `cc35abf7…`, **ne démontre pas ces corrections** et ne doit pas être confondu avec un binaire corrigé.


## UX-ARTIFACT-CI — distribution de recette temporaire, PR #64 uniquement (24 octobre 2026)

- Un workflow **distinct** `.github/workflows/ux-artifact.yml` est proposé **dans la PR #64 Draft uniquement**. Il exige une PR #64 du dépôt même, ouverte vers `main`, et le compte propriétaire `TobbyCy`; il ne s'exécute pas au démarrage de l'application et ne déclenche aucun scan réel.
- La compilation est **figée** au commit déjà validé `cc35abf70acc12120f7617f8e5ab0a8ea84b8801`, indépendamment du commit portant le workflow. SDK `.NET 10.0.401`, restauration NuGet verrouillée, tests, `win-x64 --self-contained`, licences/notices exactes du runtime 10.0.12, inventaire juridique et marqueur LIC-ZIP-01 requis avant toute création de téléchargement.
- Une bibliothèque ES-DE générée **exclusivement dans le runner** contient quatre jeux fictifs, des fichiers texte `.fixture` non exécutables et des mini-images PNG synthétiques, avec cas de jaquette absente/illisible et média manquant. **Aucune ROM/BIOS, aucun média tiers, aucun chemin ou contenu personnel.**
- L'archive est extraite et contrôlée fichier par fichier (SHA-256), ainsi qu'au moyen du smoke Avalonia avec runtime externe volontairement indisponible. La seule sortie distribuable autorisée est l'**artifact GitHub Actions** temporaire de 3 jours contenant ZIP + `.sha256`; **aucun workflow Release, aucun tag, aucune fusion automatique**. Les canaris et la liste blanche CI sont étendus à ce chemin exact, tandis que l'interdiction d'upload du workflow principal `ci.yml` est inchangée.
- Statut courant : **workflow proposé sur la branche, CI et artifact à vérifier avant tout lien de téléchargement**. Un ZIP ne doit pas être annoncé livré sans un run SUCCESS et son artifact disponible. La recette UX clavier/focus/défilement/redimensionnement reste `NOT_TESTED` sans essai humain sous Windows.


## NEXT-01-UX — candidate Avalonia sur bibliothèque ES-DE (PR #64 Draft, non fusionnée)

- La [PR #64](https://github.com/TobbyCy/RomothequeManager/pull/64), dépendante du contrat de lecture seule [PR #62](https://github.com/TobbyCy/RomothequeManager/pull/62) désormais fusionné, propose **sur une branche non distribuée** deux modes clairement distincts : POC fictif et bibliothèque ES-DE importée sur sélection explicite du dossier racine.
- Dashboard, Collection, fiche et Bibliothèques consomment le même catalogue importé ; recherche et pagination de 50 jeux, identités distinctes pour les titres homonymes, jaquette locale avec tentative de miniature de repli, diagnostics et refus de réimport non destructif. Les comparaisons A/B restent **fictives et indisponibles en mode importé**.
- Les tests du lot sont **exclusivement synthétiques** (y compris une fixture de 2 000 entrées), sans scan de collection privée, ROM réelle, scraper, export ou modification des sources. La CI .NET et les captures interactives Windows doivent être qualifiées sur le HEAD exact de la PR avant toute approbation ; **aucune fonctionnalité NEXT-01-UX n'est intégrée sur `main` ni distribuée par cette PR**.
- Cette note de branche sert à la traçabilité du changement de code ; l'actualisation transversale des autres états historiques et de la Pre-release relève séparément du chantier documentaire [PR #63](https://github.com/TobbyCy/RomothequeManager/pull/63).

> **Référence historique DOC-GOV-01 — 23 octobre 2026 :** dépôt Produit **PUBLIC**, `main=408d6263be6ae1fbc77c31f6eb9fb530cb4f76e6` (avant V2-01B et BRAND-01). Les [PR #42](https://github.com/TobbyCy/RomothequeManager/pull/42) (POC Windows V2-01A) et [#45](https://github.com/TobbyCy/RomothequeManager/pull/45) (CI-DOTNET-01) sont **fusionnées**. La [CI `push main` #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607) est **COMPLETED / SUCCESS** sur ce SHA, y compris build/tests .NET, smoke de fenêtre Avalonia et `ci-policy-gate`. Cette preuve est propre à ce commit ; elle n'atteste pas une Release ni un usage réel.

Le terme **fonctionnel** désigne ici un POC exécuté et testé **sur fixtures fictives**, pas un produit prêt pour des bibliothèques personnelles. Le code intégré, la validation CI et la disponibilité d'un binaire distribué sont trois états distincts.

## V2-01C — packaging Windows candidat (aucune distribution)

La première phase de l'[Issue #50](https://github.com/TobbyCy/RomothequeManager/issues/50) autorisait **uniquement la préparation technique** d'un ZIP autonome `win-x64` et de son fichier SHA-256. Le workflow construit le paquet avec le SDK exact et les dépendances verrouillées, inscrit un manifeste `POC-MANIFEST.json` (`syntheticOnly=true`, quatre vues, six jeux et trois bibliothèques fictives), vérifie le ZIP puis le réextrait dans un autre dossier et lance l'exécutable depuis cette extraction. Le résultat reste dans `RUNNER_TEMP`, **sans upload, GitHub Pre-release ou Release**.

**LIC-ZIP-01 — inventaire des notices tierces (traçabilité : PR [#56](https://github.com/TobbyCy/RomothequeManager/pull/56)) :** inventaire des 25 paquets NuGet verrouillés, textes éditeurs et index `THIRD-PARTY-NOTICES.txt` proposés. La politique CI est ajustée uniquement pour accepter les fichiers de notices `.txt` sous `packaging/`. Le **publish candidat de DEV 1** est maintenant inventorié (223 fichiers, [.NET CI #174](https://github.com/TobbyCy/RomothequeManager/actions/runs/39958695887), runtime observé 10.0.12), mais le **ZIP final**, les documents de licence du runtime pack Windows exact et les avis natifs transitifs restent à vérifier avec DEV 1 ; **`thirdPartyNotices=PENDING` et `RELEASE_HOLD` demeurent**.

Cette CI n'atteste pas encore un démarrage sur un Windows x64 propre sans runtime/SDK, ni les parcours interactifs clavier, focus, défilement, ComboBox et redimensionnement de [UXZIP-01 à UXZIP-12](https://github.com/TobbyCy/RomothequeManager/issues/50). Les licences/notices tierces et la recette externe restent à valider. Le [guide packaging](developer/v2-01c-packaging.md) précise la vérification et le blocage de publication.

## DIST-ZIP-01 — préparation de distribution de test (pas encore livrée)

Après fusion de la [PR #53](https://github.com/TobbyCy/RomothequeManager/pull/53) sur `main=31579350033c22cd3c55e156d29a4da7f18ad822`, Tobby a donné **GO_PREPARE_TEST_DISTRIBUTION**, pas le GO final d'upload. Une voie manuelle `workflow_dispatch` est préparée : `validate` (ZIP et SHA-256 éphémères, contrôlés depuis extraction) puis `publish` (pré-release de test uniquement après hash/commit/tag et variables d'approbation exacts). Aucune exécution PR/push n'autorise un upload.

**Correctif P1 proposé — [PR #57](https://github.com/TobbyCy/RomothequeManager/pull/57) (non fusionnée) :** le chemin `publish` épingle et relit explicitement le tag Git distant sur le SHA approuvé **avant toute création de Draft Release et tout upload** ; une Draft peut ne pas créer elle-même ce tag. Les réponses API autres que 201, conflits de création et écarts de SHA bloquent l'opération ; `gh release create --verify-tag` interdit la création implicite. Le contrôle du tag reste répété avant la publication finale. Un échec après création du ref peut laisser un tag orphelin, à traiter manuellement, sans nettoyage automatisé. **Aucune publication autorisée par cette correction seule.**

**Séparation des gates :** `validate` peut construire et contrôler le candidat complet avec les notices source présentes, puis journaliser manifeste exact et SHA-256 **sans approbation juridique préalable**. Si le marqueur DEV 2 manque, l'état reste explicitement `LEGAL_HOLD` et aucun upload n'est effectué. **`publish` échoue avant tout upload** sans marqueur juridique, quatre variables d'approbation propriétaire et SHA source/ZIP/tag concordants. Les notices source manquantes restent bloquantes. La PR #56 est maintenant intégrée, mais la conformité du ZIP final exact reste à vérifier. Revue Agent, validation juridique, approbation finale et UXZIP-01–12 restent à faire ; **aucun ZIP n'est actuellement téléchargeable**. Voir [guide packaging et distribution](developer/v2-01c-packaging.md).

**P1 fin de ligne du marqueur juridique — [PR #60](https://github.com/TobbyCy/RomothequeManager/pull/60) (Draft, non fusionnée) :** après intégration de l'approbation LIC-ZIP-01, le [validate #39996678479](https://github.com/TobbyCy/RomothequeManager/actions/runs/39996678479) a encore signalé `LEGAL_HOLD` car la détection de la ligne isolée ne tolère pas le CRLF du checkout Windows. Correctif ciblé : `\r?` avant la fin de ligne, sans modifier la sensibilité à la casse, les autres contrôles juridiques ou les gates propriétaire/SHA/tag de `publish`. Les dix canaris du contrôle statique Windows (CI-0) couvrent LF, CRLF, l'inventaire réel et les cas de refus, sans modifier la couverture historique Pester. **Nouvelle validation `phase=validate` sur le futur `main` requise après fusion par le Contrôleur ; aucun upload ni Release autorisé par cette PR.**

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

## NEXT-01-A — adaptateur ES-DE synthétique en préparation

La branche NEXT-01-A (PR Draft à suivre) propose un importateur .NET **en lecture seule** des `gamelist.xml` ES-DE depuis une racine explicitement choisie : conventions `ROMS/<plateforme>/gamelist.xml` ou `MEDIAS/<plateforme>/metadata/es-de-gamelist.xml`, identité stable relative, jeux homonymes distincts, unités `.m3u`, références de médias et diagnostics de disponibilité. Le chargement est entièrement préparé avant substitution en mémoire ; un second import échoué conserve le dernier instantané valide. Les chemins hors racines autorisées, liens symboliques, DTD et sources ambiguës sont refusés. Seules des fixtures synthétiques sont exécutées en CI ; **ni UI réelle, ni scanner complet, ni accès au SSD utilisateur, ni Release**. Le correctif P1 de revue ajoute une identité de bibliothèque **stable dans la session** malgré l'ajout/retrait de jeux, une relocalisation explicite vérifiée par empreinte du contenu et `TryOpenMedia` qui réapplique confinement et contrôle des reparse points lors de l'ouverture. L'identité de bibliothèque n'est **pas persistée entre redémarrages** sans mécanisme de réassociation futur. DEV 3 raccordera ultérieurement cette projection aux vues Avalonia après revue du contrat.
