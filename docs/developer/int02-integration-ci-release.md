# INT-02 — Préparation de fusion, industrialisation CI et contrôle Release

**Statut : CHANGES_REQUIRED avant fusion / PUBLIC.** Cette note est une procédure d'intégration **non exécutée**, issue de la branche candidate PUB-01A (#31). Elle ne remplace pas les HEAD qui devront être relus et testés au moment de la réception.

## 1. Ancrages Git réels (consultation du 22 octobre 2026)

| PR | HEAD relevé | Base historique relevée | Problème |
| --- | --- | --- | --- |
| #26 comparaison | `2ea6579ca645de9ee2af050800bb105b15ccc02a` | `main:f2c5111b8baf0727568c01dc8f7861c15a65b347` | CI Actions existante en **failure**, aucune réception PASS sur HEAD |
| #27 bibliothèque | `d4f12393c198a87487b167b4ff489c6ac5586c79` | `#26:a7be7c2e8d6a7af337f25613b3735cf980d7320b` | 7 commits post-base #26, conflits possibles dans scripts CI et docs |
| #29 catalogue | `c7576a6f2d6e27546b321163dae98288b2c73be0` | `#27:355ea45bc72190a61e43b2570982dcfaa716d982` | 12 commits post-base #27, mêmes fichiers CI/docus à rapprocher |
| #28 UI | `7195137db186ac4e815a75ceb0bcf3e96009618a` | `main:f2c5111b8baf0727568c01dc8f7861c15a65b347` | branche UI indépendante, aucune intégration au Produit actuel |
| #30 UI T2 | `a0a1f9c8c13bae54209f0fce9a26ae24d08656ca` | `#28:e3fc250deac26c10cbd4810980fb8f9fbbbea817` | 8 suites Node, base historique #28, raccordement à effectuer après noyau |
| #31 CI publique | HEAD évolutif (relire dans PR #31) | `#29:c8e47b2452b07b127a50de763db5928caa7edcd7` | branche candidate **ne contient pas** les derniers correctifs métier #29 |

Aucune PR ci-dessus n'a été fusionnée dans `main`. Les branches #27, #29, #30, #31 ont des **parents historiques**, même si leur base PR désigne une branche contemporaine.

### Contrôle d'identité des composants communs

À la consultation : `Modules/LibrarySnapshotProjection.psm1` et `tests/Invoke-CiLibraryProjection.ps1` sont identiques **sur #27 et #29** (blob Git `f1d2afd8793c873c2cb4ab5d57fc6777f2637a96` et `c152d73deeb542472023ec3c4889dc3f7d36bf64`), et **plus anciens sur #31**. Le lecteur `adapters/shared/catalogue-t3-consumer.js` est identique #29/#30 (blob `c43e60943014dc6e0f49cfed914d35697ea20c62`) ; fixture JSON `CatalogueT3CanonicalSample.json` identique #29/#30 (blob `1ffc52863ce86ab9a8b79d6b3b31ef5c7c9f8e37`). **Égalité de blobs ne prouve pas une exécution ni un merge.**

## 2. Ordre de travail du Contrôleur (fusion seulement après réception)

1. **#26 / main** : conserver SHA final, vérifier les canaris Python, la CI réelle et tous les résultats requis (aucun skipped requis). La PR #26 a un run GitHub `39743198888` en `failure` : policy failure, gate failure, jobs métier skipped ; les logs n'ont pas pu être récupérés par le connecteur. **BLOCKED** : ne pas fusionner tant que la cause ne peut être expliquée et qu'un nouveau run sur le HEAD exact ne donne pas un gate green vérifié.
2. **#27 après réception #26** : conserver les corrections de #27 Set/Map, dernière date UTC chronologique et couverture par console, ainsi que leurs harness. Concilier la règle de modules #26 (19 approuvés) vers #27 (20) **sans écraser les SHA du socle historique**. Concilier `ci_policy.py`, `test_ci_policy.py`, `Invoke-CiStatic.ps1`, statut Produit. Seules des branches candidates neuves sont autorisées pour simulation ; pas de force-push de #27 ni de fusion main automatique.
3. **#29 après réception #27** : conserver `CatalogueProjection`, `CatalogueDtoContract`, UTF-8 BOM, export JSON, huit familles de falsification, Node, et les corrections de #27 **au même blob**. Étendre 20→22 modules approuvés nommément ; ne jamais revenir aux vieilles règles 19/20 ni à l'ancienne regex corrompue.
4. **#28 puis #30** : réconcilier leur ascendance séparée. Vérifier la cohérence du consommateur canonique et de la fixture commune par SHA. À l'intégration, les suites Node doivent être déclenchées et toutes répondre aux critères ; UI Edge reste une recette distincte. Aucun comparateur reprogrammé dans le navigateur.
5. **#31 sécurité et CI publique après noyau/UI** : réappliquer les correctifs de sécurité sur l'arbre intégré réellement validé. **NE PAS remplacer le module #27/#29 ou le harness récent par ceux de la branche #31 plus ancienne.** Recalculer les règles d'allowlist et la matrice des jobs pour l'arbre produit.

**Préparation locale non destructive** (inspection uniquement, pas de `merge`, `rebase` ni `reset --hard`) :

```powershell
git fetch origin main feat/inventory-snapshot-compare-9 feat/library-snapshot-projection-10 feat/t1-catalogue-synthetic-integration feat/ui11-interactive-prototype feat/t2-catalogue-workspace audit/pub01a-public-ci-hardening
git rev-parse origin/main
git rev-parse origin/feat/inventory-snapshot-compare-9
git rev-parse origin/feat/library-snapshot-projection-10
git rev-parse origin/feat/t1-catalogue-synthetic-integration
git merge-base origin/feat/inventory-snapshot-compare-9 origin/feat/library-snapshot-projection-10
git merge-base origin/feat/library-snapshot-projection-10 origin/feat/t1-catalogue-synthetic-integration
git log --oneline a7be7c2e8d6a7af337f25613b3735cf980d7320b..origin/feat/inventory-snapshot-compare-9
git log --oneline 355ea45bc72190a61e43b2570982dcfaa716d982..origin/feat/library-snapshot-projection-10
git diff --check
git diff --name-status origin/feat/inventory-snapshot-compare-9 origin/feat/library-snapshot-projection-10
git diff --name-status origin/feat/library-snapshot-projection-10 origin/feat/t1-catalogue-synthetic-integration
```

**Fichiers de collision métier/CI à inspecter manuellement :** `tests/ci_policy.py`, `tests/Invoke-CiStatic.ps1`, `tests/test_ci_policy.py`, `.github/workflows/ci.yml`, `docs/product-status.md`, `CHANGELOG.md`. Retenir une seule version intégrée de chacun, prouvée par canaris positifs ET négatifs, plutôt que sélectionner globalement « ours/theirs ».

## 3. CI adaptative : événements, tests et gate

- `pull_request` → **main** : tester le ref de merge fourni par GitHub ; `push` uniquement main après fusion contrôlée ; `workflow_dispatch` limité à main dans la politique publique candidate.
- **Toujours exécutés** : `policy` (Python, classification Git, canaris et diff) puis `ci-docs` (contrôle portable chemins/docs/sécurité), puis `ci-policy-gate` final avec `if: always()`. Un `GIT_READ_FAILED`, un fichier non autorisé ou une sortie de classificateur falsifiée fait échouer le gate.
- **Quand PowerShell applicable** : `ci0-static` (PS5.1, source integrity 22 modules, canaris négatifs, snapshots #26, bibliothèques #27, catalogue #29, export JSON canonique et Node, Unicode), `ci1-runtime` (bootstrap), `ci2-pester` (Pester 4.10.1 et suites réellement découvertes), `ci3-e1-integration` (E1 safety), `ci4-preflight-synthetic` (synthetic local only).
- **Quand .NET applicable** : `ci-dotnet`, build/tests verrouillés et compteur de tests strictement positif. Aucun projet `.csproj` ou `packages.lock.json` observé dans les arbres #29/#30/#31 au moment de la note : le job .NET peut être **NOT_APPLICABLE**, **pas PASS**.
- **Prototype intégré** : fermer l'inventaire des **8 scripts** `smoke.cjs`, `projection-smoke.cjs`, `integrated-smoke.cjs`, `t2-catalogue.test.cjs`, `t4-catalogue.test.cjs`, `synthetic-import.test.cjs`, `ui-journey.test.cjs`, `unicode-producer.test.cjs`. Les 7 autonomes sont lancés sans argument ; le huitième reçoit le **même fichier JSON synthétique PowerShell 5.1** qui passe par le validateur Node. `Invoke-CiUiAdapterContract.ps1` garde le contrat JS **textuel** et ne remplace pas une recette Edge.
- **Gate de fin** : seul `ci-policy-gate` est candidat « required check » universel, car les autres jobs sont conditionnels. Une dépendance requise `failure`, `skipped`, `cancelled`, manquante, ou un résultat de `toJSON(needs)` incompatible doit produire **FAIL**. Ne jamais remplacer le résultat par `success()` ou marquer un job ignoré comme passé.

**Contrôle statique disponible :** l'union des **142 fichiers suivis** #29/#30/#31 a été confrontée à **34 expressions nominatives de CI-0 compilables** sur #31, **0 chemin non reconnu** après prise en compte des tests et docs Dev 2. Cela **ne vaut ni PASS PowerShell, ni PASS Python, ni PASS Node**.

## 4. Protection de main et contributions publiques

**À configurer seulement avec autorisation du propriétaire, après confirmation de disponibilité GitHub** :

- Règle `main` active : PR obligatoire, approbation indépendante (1 minimum, idéalement 2 si un second mainteneur est disponible), avis CODEOWNERS, résolutions des discussions, ancien avis invalidé après nouveau commit, suppression et force-push interdits, aucun bypass admin/app non prévu.
- Statut GitHub `ci-policy-gate` obligatoire depuis le workflow `Adaptive CI`, appliqué à un vrai SHA/merge SHA de PR. Vérifier qu'un check ancien ou hors branche ne peut pas satisfaire la règle.
- Actions : `contents: read` par défaut, `actions/checkout` SHA épinglé, `persist-credentials: false`, **pas** de `pull_request_target`, `workflow_run` privilégié, secret ou écriture sur une PR externe. Approbation des workflows de contributeurs externes, pas de runner privilégié ni stockage de token dans les PR de forks.
- Tags `v*` protégés via ruleset, pas de suppression/déplacement/force-push et création réservée au rôle Release autorisé après validation. Revenir au SHA exact d'un tag sans supposer que sa simple présence prouve la CI.
- Anciennes métadonnées des PR/issues et références vers espace interne privé : audit PUB-01B **indispensable** avant rendre le Produit visible.

## 5. Première Release : recette reproductible, pas de publication depuis cette branche

**Préconditions fermées :** arbre intégré relu, décision d'adoption du contrat (0.2 seulement si le Contrôleur l'a autorisée), build applicatif réellement présent, versions SDK/dépendances verrouillées, tests et CI-0 à CI-4 applicables + Node/Edge reçus, gate `ci-policy-gate` succès sur SHA de `main`, tag `vX.Y.Z` protégé pointant vers le **même SHA**, approbation humaine Release.

**Build futur** (uniquement lorsqu'un projet .NET existe) : `dotnet restore --locked-mode` → `dotnet test` avec tests réellement exécutés → `dotnet publish --configuration Release --runtime win-x64 --self-contained true` dans un répertoire neuf hors source. Ne jamais utiliser un ZIP d'un ancien POC comme preuve. Contenu sans bibliothèques ROM, chemins personnels, token, projet Pilotage, logs internes, fichiers temporaires ni StateRoot.

**Contrôle binaire** : produire un ZIP à ordre de chemins déterministe, dates et permissions normalisées, sans symlink/traversal, avec même compression/outil/version épinglés ; produire **deux builds indépendants** en environnements propres et comparer **SHA-256** et liste des entrées. Établir le manifeste `{gitCommit,gitTag,workflowRunId,requiredGate,toolchain,windowsRid,selfContained,files,zipSha256}`, vérifier `Get-FileHash -Algorithm SHA256` sur le ZIP puis sur le ZIP téléchargé. Toute différence, absence de build/test ou exception = **HOLD_RELEASE**.
  
La fonction existante `tests/ci_policy.py: release_boundary` vérifie des invariants d'identité, de profil et SHA-256 mais retourne expressément `NOT_AUTHORIZED_BY_CI` ; ce n'est **pas** un publieur. Aucun workflow à token `contents: write` ni Release API automatique à autoriser pour l'instant.

## 6. État de validation et écueils

- Run **réel** GitHub #26 `39743198888` : `CI policy / classify and canaries` **FAILURE**, `ci-policy-gate` **FAILURE**, autres jobs conditionnels **SKIPPED**. Le service de logs ne fournit pas le détail ; **cause non démontrée**. #28 possède un run en état analogue. #27/#29/#30/#31 sans PASS pertinent au nouveau HEAD.
- Tests PowerShell 5.1, Python, Pester, JSON↔Node, UI et comparaison du ZIP **non exécutés** par le DEV 1 dans ce lot ; canaris écrits ≠ preuve d'exécution. Les tests de Dev 2 simulés sous V8 ne valent pas recette Edge ou PS5.1.
- GitHub API protections main renvoie `403`, aucune règle de protection vérifiée. Le dépôt reste PRIVATE, et PUB-01B doit autoriser le nettoyage de contenu avant tout changement.

**Verdict : `INT02_TECHNICAL_MERGE_READY = FALSE` tant que #26 est rouge ou inexpliqué, que les bases Git ne sont pas réconciliées, et que l'arbre intégré n'a pas de runs vérifiés. `INT02_PREPARATION_COMMITTED / CHANGES_REQUIRED / HOLD_MERGE / HOLD_PUBLICATION / HOLD_RELEASE`.**
