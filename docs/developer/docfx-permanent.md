# DocFX permanent — NEXT-05 / #88 et #138

## Checkpoint réel — 02.11.2026 (CURRENT)

Les garanties ci-dessous sont des **mesures Windows au SHA exact**, non une projection des anciens essais POC. Les « signatures » contrôlées ici sont celles des **API C#** (types, paramètres et surcharges), **pas une signature Authenticode** : l'application reste non signée.

| Contrôle | `main=7aa62c8f` intégré | PR #154 HEAD `f27bbe71` DRAFT/non fusionnée |
| --- | --- | --- |
| CI source | [push/main #41465566095](https://github.com/TobbyCy/RomothequeManager/actions/runs/41465566095) **10/10 SUCCESS**, gate vert | [Windows #41482716933](https://github.com/TobbyCy/RomothequeManager/actions/runs/41482716933) **8/10 SUCCESS**, `.NET` et `ci-policy-gate` **FAILURE** |
| .NET | **335/335 PASS** | Tests métier/packaging techniques séparés ; GUI non-admin extrait échoue à la recette CI |
| Roslyn (D/N) | **857/935 = 91,66 %**, nouveaux **0/0** | **860/938 = 91,68 %**, nouveaux **3/3** documentés |
| Éligibles/exclus | `997=935+62` | `1000=938+62` ; **62** générés Avalonia exclusivement sur provenance attestée |
| Contrôle sémantique | Signatures exactes, membres internes, assemblage et surcharges **PASS** | Idem **PASS** |
| Site DocFX | **120 YAML, 122 HTML, 2 013 liens locaux, 0 lien cassé et 0 UID absent** | Même validation des UID/liens **PASS**, sans rendre le required gate vert |

La compilation XML → métadonnées YAML → HTML et les canaris RED/GREEN du cleanup Windows sont **effectivement validés dans le job `ci-docfx`** de ces deux runs. Les vérifications de site **ne valident pas les liens et ancres ajoutés sur la branche documentaire #146**, qui exigent leur contrôle indépendant. Les résultats des mesures historiques ci-dessous gardent leur valeur **pour leurs seuls HEAD**, pas comme taux CURRENT.

**Différence essentielle de périmètre :** `docs/api-reference/filterConfig.yml` filtre certains harnais CI dans le **site rendu**. Ce fichier **ne retire pas** ces symboles du **dénominateur Roslyn** ; seules les 62 déclarations entièrement générées par Avalonia et attestées par XAML/analyzer peuvent être exclues. Les API métier `internal` demeurent éligibles. Aucun seuil abaissé, `NoWarn`, exclusion de fichier métier ou suppression de check requis.

Le site DocFX n'est **pas publié**. Le succès du job documentaire de #154 ne compense pas l'échec du vrai EXE extrait sous utilisateur standard. Voir [STATUS](../../STATUS.md) et [checklist de recette #95](https://github.com/TobbyCy/RomothequeManager/issues/95#issuecomment-6585108522).

## Cleanup Windows de la baseline (#149, lot C)

Le cleanup conserve huit tentatives, les diagnostics Git/IO/processus et un échec fatal
`DOC_BASELINE_WORKTREE_CLEANUP_FAILED` si le dossier existe encore **ou** si Git
enregistre toujours le worktree. Les pauses bornées de 2, 4, 6, 8, 10, 12 et 14 secondes
(56 secondes cumulées, hors commandes) remplacent les sept pauses fixes de 2 secondes.
Une lecture CIM indisponible est signalée ; elle ne masque pas le verdict final.
Aucun processus détenteur d'un handle n'est tué par le cleanup.

`tools/doc-coverage/Test-BaselineCleanup.ps1` exécute le bloc réel de `ci.yml` sous
Windows, dans un dépôt Git synthétique neuf : verrou sans partage de suppression
maintenu jusqu'à l'échec fatal RED, puis libéré pour vérifier GREEN avec les deux
invariants. Un verrou transitoire de 22 secondes vérifie la récupération au-delà de
l'ancienne fenêtre de 14 secondes. Le PID créé par le canari est connu et tracé ;
il ne prouve pas l'identité du processus des incidents CI historiques. Aucun fichier
personnel, build produit, serveur externe ou permission réelle n'est utilisé.

Le canari est obligatoire dans `ci-docfx`. Cette correction ne change ni les règles
Roslyn/DocFX, ni les exclusions, ni les autres jobs requis. Un handle persistant
au-delà des huit tentatives reste un échec, pas un PASS ni une relance infinie.

## Génération et gates

La source de vérité des signatures est le code C# et sa documentation XML. Ce site n'est ni publié via Pages, ni une garantie de permissions d'accès réelles.

Sur Windows avec **SDK 10.0.401** (global.json), restaurer l'application en mode verrouillé, puis compiler avec génération XML. Utiliser **DocFX 2.81.0** épinglé localement :

```powershell
dotnet restore src/RomothequeManager.V2/RomothequeManager.V2.csproj --locked-mode
dotnet build src/RomothequeManager.V2/RomothequeManager.V2.csproj -c Release --no-restore -p:GenerateDocumentationFile=true
dotnet tool install docfx --tool-path "$env:RUNNER_TEMP/docfx" --version 2.81.0
& "$env:RUNNER_TEMP/docfx/docfx.exe" metadata docs/api-reference/docfx.json
& "$env:RUNNER_TEMP/docfx/docfx.exe" build docs/api-reference/docfx.json --warningsAsErrors
```

L'analyse de couverture est distincte de la couverture de tests Pester et du simple nombre de warnings CS1591. Depuis les corrections DOC-P1-01/02/03, la CI compile **deux fois le vrai projet** `RomothequeManager.V2.csproj` (HEAD et base `main` dans une worktree Git isolée). Le fichier `tools/doc-coverage/DocCoverage.targets`, importé uniquement sur le runner via `CustomAfterMicrosoftCommonTargets`, capture `@(Compile)`, `@(ReferencePathWithRefAssemblies)`, `DefineConstants`, LangVersion, Nullable et le chemin de l'assembly réellement émis. `EmitCompilerGeneratedFiles` capture également les sources Avalonia générées. Aucune configuration du projet produit n'est modifiée pour calculer la couverture.

Le programme `tools/doc-coverage/RealProjectInputs.cs` exige des manifestes valides, refuse les références/fichiers absents, reconstruit sémantiquement les deux compilations Roslyn et refuse les diagnostics d'erreur. Il rapproche les symboles éligibles des métadonnées ECMA-335 de l'assembly compilée, **y compris les membres `internal`**, puis contrôle aussi leurs identifiants de déclaration XML exacts (surcharges différenciées par les types des paramètres, import Roslyn non-public explicite). Un écart refuse la mesure ; ce second contrôle reste soumis à la preuve native Windows au HEAD exact. Par défaut, **tous les symboles explicitement déclarés et non privés** de l'assembly applicative sont éligibles : aucun nom de classe, méthode ou fichier ne peut soustraire une nouvelle API interne métier. Les exclusions privées sont tracées individuellement par `SymbolId` et motif, sans allowlist de fichiers ni règle de baisse du dénominateur.

Un symbole n'est documenté que si son XML est valide, comporte un `summary` suffisamment informatif, sans `TODO` ni résumé contradictoire ou dupliqué. Un `inheritdoc` est accepté **uniquement si sa cible est résolue** et documentée (surcharge, interface ou `cref`), sinon il est refusé. Les canaris exécutent le vrai prédicat du gate avec des mutations RED/GREEN : nouveaux publics et internes métier, `ReadAccessPolicy.AllowsRead` aux noms atypiques, **ajouts non documentés alors que le taux global reste supérieur à 90 %**, retrait de documentation/API, résumés multiples, héritage résolu/non résolu, `partial`, exclusion privée et symboles conditionnés par `DefineConstants`. Rapports JSON/Markdown par fichier et symbole dans l'artefact CI à rétention courte, sans publication DocFX.

**Point historique, périmètre précédent : au HEAD `f51d4f8f`, `523/767 = 68,19 %`. Ce taux N/D n'est PAS reconductible au nouveau calcul qui inclut désormais par défaut les contrats `internal` déclarés dans le projet de production.** Une nouvelle mesure native au HEAD outillage courant et une nouvelle mesure après toute fusion #147 sont obligatoires ; la CI conserve le ratio exact `10×D≥9×N`, les 100 % des nouveaux symboles et la non-régression. Les fichiers UI/VM demeurent sous responsabilité DEV 3, sans modification concurrente DEV 1. Aucun taux avant le rapport Windows réel ne doit être présenté comme validé.

Le gate impose **90 % minimum**, **100 % des nouveaux IDs éligibles documentés**, aucun recul du ratio, aucune disparition silencieuse d'API éligible et aucun commentaire auparavant valable supprimé. Un résultat en dessous du seuil échoue : il n'est pas corrigé en baissant le seuil ni en masquant CS1591.

L'exécution historique [pilote DocFX #41212901369](https://github.com/TobbyCy/RomothequeManager/actions/runs/41212901369) prouve uniquement la faisabilité native sur un ancien SHA. Une CI au SHA actuel doit réellement construire XML/YAML/HTML et contrôler liens/UID, sans scanner de données personnelles.

Le passage en état **genuinely required** du check `ci-policy-gate` relève de la protection de branche GitHub contrôlée par le propriétaire. Un workflow présent ne suffit pas : si la protection est invérifiable, déclarer `BLOCKED_BRANCH_RULE`.

## GENERATED_AVALONIA_ONLY — qualification contrôlée (#138)

L'exclusion n'est **jamais** décidée par `.g.cs`, `Generated`, un nom de classe ou la présence/absence de XML. Les deux compilations natives HEAD et base capturent les chemins des analyzers réellement passés au compilateur, leur identité/version et empreinte, les sources XAML et les sorties fraîches de `CompilerGeneratedFilesOutputPath`. La provenance d'une sortie Avalonia doit correspondre à l'assembly `Avalonia.Generators`, à son chemin contrôlé, au hash de la source effectivement compilée et à une entrée XAML non ambiguë. Une source non attribuable à un XAML précis ou à un analyzer vérifié **reste éligible** ; les fichiers générés restent des candidats tant que cette preuve n'est pas établie.

Pour chaque `SymbolId`, le classificateur utilise **toutes** les `DeclaringSyntaxReferences`. Seuls les IDs composés intégralement de déclarations générées et attestées peuvent être exclus : une déclaration `partial` manuelle interdit l'exclusion du type commun. Un membre manuel de ce type reste éligible, ainsi qu'un membre manuel dont le fichier ou le nom ressemble à du code généré. Les sorties manquantes, la dérive des versions/générateurs/empreintes entre base et HEAD et l'usurpation de provenance sont refusées ou conservées en éligibles, jamais masquées.

Les artefacts internes `generated-exclusions-head.json` et `generated-exclusions-base.json`, et le rapport `gate.json`, donnent pour chaque ID exclu : règle, générateur/version/empreinte, source/empreinte, SHA de compilation, nombre de déclarations et source XAML. Pour chaque compilation, réconcilier `E_all = E_manual + E_generated_excluded` sans doublons. Des canaris vérifient provenance authentique, fichier trompeur, `partial` mixte, hash falsifié, dérive de générateur et ajout d'API manuelle non documentée malgré une couverture globale ≥90 %.

L'ancienne mesure 531/896 au HEAD `4bfd86dd` est **historique**, non reconductible après #147. La qualification des 59 candidats n'est ni une exemption pré-approuvée, ni une preuve de couverture ≥90 %. La CI Windows et la revue indépendante DEV 2 devront valider le nouveau HEAD ; aucun site public, `NoWarn`, changement de visibilité ni Release.
