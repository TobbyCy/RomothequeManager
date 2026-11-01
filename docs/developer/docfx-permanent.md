# DocFX permanent — NEXT-05 / #88 et #138

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
