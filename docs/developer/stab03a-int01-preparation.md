# STAB-03A → INT-01 : préparation de réconciliation (sans fusion)

**Périmètre : DEV 1 / code, tests, CI, branches Produit.** Préparation du 22 octobre 2026. Documentation technique candidate sur PR #29, **sans GO d'intégration, sans adoption normative du DTO 0.2**. Les HEAD exacts de cette note doivent être relus au déclenchement d'INT-01, car ils peuvent changer après son commit.

## 1. Références initiales vérifiées

| Flux | HEAD avant cette note | Base historique de la PR | Dette d'ascendance vs branche parente actuelle |
|---|---|---|---|
| #26 comparateur | `2ea6579ca645de9ee2af050800bb105b15ccc02a` | `main=f2c5111b8baf0727568c01dc8f7861c15a65b347` | 0 vis-à-vis de main à la comparaison de départ |
| #27 bibliothèques | `d4f12393c198a87487b167b4ff489c6ac5586c79` | ancien #26 `a7be7c2e8d6a7af337f25613b3735cf980d7320b` | **7 commits de #26** à réconcilier |
| #29 catalogue | `df5aba1ec799cf0fbf85724cedff180019208326` | ancien #27 `355ea45bc72190a61e43b2570982dcfaa716d982` | **12 commits de #27** à réconcilier |
| #31 préparation CI publique | `fca19035d47cc8ab1d091eec1cde8b499f4a4048` (avant canari UI additionnel) | ancien #29 `c8e47b2452b07b127a50de763db5928caa7edcd7` | **4 commits de #29** à réconcilier, plus les nouveaux commits éventuels |

**Ne pas assimiler « PR #27 base = branche #26 » à un alignement effectif des arbres.** Les branches sont volontairement empilées à des SHAs historiques. GitHub peut afficher `mergeable=false` sans démontrer que tous les tests sont invalides ou que les conflits ont été traités.

### 7 commits de #26 postérieurs à la base de #27

`af45a866`, `e7d278fa`, `6d79205a` (ensemble exact des 19 modules, source integrity) ; `d5e6dd3b` (commit synthétique Python vide), `c06188af` (allowlists), `5b02cdc0` (canaris positifs/négatifs), `2ea6579c` (statut Produit). Fichiers concernés : `tests/ci_policy.py`, `tests/test_ci_policy.py`, `tests/Invoke-CiStatic.ps1`, `docs/product-status.md`.

### 12 commits de #27 postérieurs à la base de #29

`39938540`, `00a224bd`, `65a04925`, `09184460`, `e6664a40`, `fa7dc598`, `8a756364`, `2b418c83`, puis `b8df58cc`, `ea42377d`, `698b9215`, `d4f12393`. Fichiers concernés : `Modules/LibrarySnapshotProjection.psm1`, `tests/Invoke-CiLibraryProjection.ps1`, `tests/Invoke-CiStatic.ps1`, `tests/test_ci_policy.py`, `docs/product-status.md`, `CHANGELOG.md`.

Les deux derniers correctifs visent (1) la **couverture par console** : preuve GBA ne peut pas conférer PARTIAL à PS1 non observée ; (2) **lastObservedAtUtc** : horodatage UTC fractionnaire choisi chronologiquement, indépendamment de l'ordre d'entrée. Les corrections et harness correspondants sont portés **à contenu identique sur #27 et #29**, sans fusion.

## 2. Collision / source de vérité à préserver pendant INT-01

| Surfaces communes | Règle d'intégration |
|---|---|
| `Modules/SnapshotComparison.psm1` (seulement #26) | Prendre le module exact #26, **ne jamais dupliquer** le comparateur dans #27/#29 |
| `Modules/LibrarySnapshotProjection.psm1` + `tests/Invoke-CiLibraryProjection.ps1` (#27 et #29) | Conserver le contenu **identique au nouveau #27/#29** : Set/Map scalaires vides, couverture par console, dates fractionnaires et canaris |
| `tests/ci_policy.py` (#26/#27/#29/#31) | **Construire un seul fichier intégré** avec la liste verrouillée 18 historiques + 4 extensions, classificateur et refus stricts, canaris du commit vide, sécurité PUBLIC de #31 seulement si autorisée ; ne jamais copier aveuglément la version 19 ou 20 modules par-dessus celle de 22 |
| `tests/Invoke-CiStatic.ps1` (#26/#27/#29/#31) | Garder les SHA historiques, la liste nominative des modules au nombre réel, les regex **fermées et compilables**, chemins des fixtures, tests, scripts Node, prototype applicable et `CODEOWNERS` si #31 acceptée ; conserver le refus de l'inconnu |
| `tests/test_ci_policy.py` (toutes) | Conserver **l'union des canaris réellement indépendants** (fork, statuts skipped/cancelled, commit Git sans changement, chemins refusés, BOM, politique PUBLIC) sans supprimer les cas négatifs pour obtenir du vert |
| `.github/workflows/ci.yml` (#27/#29/#31) | Maintenir permissions `contents: read`, actions SHA, `persist-credentials:false`, PR/push main, policy+docs+CI-0 à CI-4 + éventuel .NET, `ci-policy-gate` final `always()`. Ajouter les tests catalogue/DTO et le JSON **PS5.1 réellement exporté** vers Node. Après #28/#30, exécuter les smokes et le contrôle textuel UI uniquement quand ses sources existent ; refuser les sources/tests manquants |
| `docs/product-status.md`, `CHANGELOG.md` | Réconcilier une seule chronologie qui distingue `main`, PR Draft, tests `NOT_EXECUTED` et produit réellement utilisable ; retirer toute référence interne non nécessaire avant PUBLIC |

## 3. Vérifications avant toute demande de fusion

Inspecter les arbres de chaque HEAD dans des worktrees **synthétiques et non persistants**. Aucun rebase ni merge n'est autorisé par cette note.

```powershell
git fetch origin main feat/inventory-snapshot-compare-9 feat/library-snapshot-projection-10 feat/t1-catalogue-synthetic-integration audit/pub01a-public-ci-hardening
git merge-base origin/feat/inventory-snapshot-compare-9 origin/feat/library-snapshot-projection-10
git merge-base origin/feat/library-snapshot-projection-10 origin/feat/t1-catalogue-synthetic-integration
git log --oneline a7be7c2e8d6a7af337f25613b3735cf980d7320b..origin/feat/inventory-snapshot-compare-9
git log --oneline 355ea45bc72190a61e43b2570982dcfaa716d982..origin/feat/library-snapshot-projection-10
git diff --name-status origin/feat/inventory-snapshot-compare-9 origin/feat/library-snapshot-projection-10
git diff --name-status origin/feat/library-snapshot-projection-10 origin/feat/t1-catalogue-synthetic-integration
git diff --check
```

Exécuter les scripts suivants uniquement dans un **checkout isolé de chaque HEAD applicable**, sous Windows PowerShell **5.1 Desktop** ; capturer version, HEAD, code de sortie, exceptions et noms exacts des harness :

```powershell
$PSVersionTable.PSVersion
git rev-parse HEAD
powershell.exe -NoProfile -NonInteractive -File .\tests\Invoke-CiStatic.ps1
python -B -m unittest discover -s tests -p test_ci_policy.py -v
powershell.exe -NoProfile -NonInteractive -File .\tests\Invoke-CiSnapshotComparison.ps1
powershell.exe -NoProfile -NonInteractive -File .\tests\Invoke-CiLibraryProjection.ps1
powershell.exe -NoProfile -NonInteractive -File .\tests\Invoke-CiUiProjectionSerialization.ps1
powershell.exe -NoProfile -NonInteractive -File .\tests\Invoke-CiCatalogueProjection.ps1
powershell.exe -NoProfile -NonInteractive -File .\tests\Invoke-CiCanonicalCatalogue.ps1
node .\tests\catalogue_t3_consumer.test.cjs
```

**Applicabilité :** #26 ne possède que les premiers tests historiques/comparateur ; #27 ajoute bibliothèques+projection UI ; #29 ajoute catalogue+DTO+Node. Ne pas lancer une commande absente de l'arbre testé puis présenter cet échec comme un défaut du moteur. Après assemblage INT-01, **tous** les tests pertinents doivent être présents et réellement exécutés. Pour une parité forte, exécuter `Export-SyntheticCanonicalCatalogue.ps1 -OutputJson` dans un chemin temporaire, puis relire les **octets du fichier réellement créé** sous Node avec `RM_CANONICAL_EXPORT_JSON` ; la fixture JSON manuscrite n'est pas une preuve PS5.1.

## 4. Gates CI et sécurité à réceptionner

- `policy` et `ci-docs` toujours requis ; pour code PowerShell/mixte : `ci0-static`, `ci1-runtime`, `ci2-pester`, `ci3-e1-integration`, `ci4-preflight-synthetic`. `ci-dotnet` seulement si applicable, l'absence de projet n'est pas un faux PASS.
- `ci-policy-gate` **`always()`** doit refuser une dépendance obligatoire absente, skipped, cancelled ou failed, et figurer comme **required check dans la protection effective de main après publication**. Un YAML seul ne garantit pas la protection.
- Les contributions publiques doivent utiliser `pull_request` sans secrets/write token, `push/main` et éventuellement `workflow_dispatch` vérifié sur main ; jamais `pull_request_target` pour exécuter le fork. Checkout sans credentials persistants, actions épinglées.
- Le draft PUB-01A **#31** contient `CODEOWNERS`, canaris de sécurité du workflow, découverte Node et contrôle textuel UI en mode applicable ; son ancienne base #29 n'inclut pas les derniers correctifs P0. **Elle ne doit pas remplacer silencieusement les fichiers #29 plus récents.**
- La protection `main`, les reviews CODEOWNERS indépendantes, la facturation et les approbations fork nécessitent des réglages propriétaires. Avant activation réelle, bloquer fusion/publication.

## 5. Reliquats / verdict

**Inspection GitHub des SHAs et corrections de sources effectuée**, mais **PS5.1, Python, Node, navigateur et GitHub Actions sur les nouveaux HEAD = NOT_EXECUTED** tant qu'un compte rendu d'exécution indépendant n'est pas attaché à chacun. Aucune modification de ROM réelles, StateRoot, aucun merge/rebase/force-push/Release. Risques résiduels : intégration des fichiers communs, actions de l'Agent, exactitude des gates sur merge ref, collision de docs, revue du consommateur T2 et des métadonnées publiables. **Statut proposé : STAB03A_READY_FOR_INT01 (préparation de revue/intégration uniquement) / HOLD_MERGE / RUNTIME_NOT_EXECUTED.**
