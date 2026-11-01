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

Le programme `tools/doc-coverage/RealProjectInputs.cs` exige des manifestes valides, refuse les références/fichiers absents, reconstruit sémantiquement les deux compilations Roslyn et refuse les diagnostics d'erreur. Il rapproche les symboles éligibles des véritables métadonnées ECMA-335 de l'assembly, **y compris les membres `internal`** ; un écart bloque la mesure. Les symboles du projet qui n'appartiennent pas à la surface documentaire sont conservés comme **exclusions individualisées par `SymbolId` avec motif**, sans filtrer arbitrairement des dossiers entiers. Les nouvelles API internes métier critiques sont découvertes par leurs types/rôles sémantiques, indépendamment d'une liste fixe de noms de fichiers.

Un symbole n'est documenté que si son XML est valide, comporte un `summary` suffisamment informatif, sans `TODO` ni résumé contradictoire ou dupliqué. Un `inheritdoc` est accepté **uniquement si sa cible est résolue** et documentée (surcharge, interface ou `cref`), sinon il est refusé. Les canaris exécutent le vrai prédicat du gate avec des mutations RED/GREEN : nouveaux publics et internes métier, retrait de documentation/API, résumés multiples, héritage résolu/non résolu, `partial`, exclusion privée et symboles conditionnés par `DefineConstants`. Rapports JSON/Markdown par fichier et symbole dans l'artefact CI à rétention courte, sans publication DocFX.

**Dernière mesure de référence au HEAD source `727b2175980c80ab348533706c160d1c44425195` : `523/767 = 68,19 %`**, 52 sources réelles dont trois générées, 197 références, croisement assembly 767/767 PASS, 12 canaris PASS ; 244 membres éligibles encore manquants, tous dans les fichiers UI/VM réservés à DEV 3. Pour atteindre 90 % à **dénominateur constant**, il faut au moins `691/767`, soit **168 membres supplémentaires** ; toute fusion #147 impose de recalculer intégralement N/D, les nouveaux IDs et les non-régressions. Ce résultat n'est ni APPROVABLE, ni une autorisation de Release.

Le gate impose **90 % minimum**, **100 % des nouveaux IDs éligibles documentés**, aucun recul du ratio, aucune disparition silencieuse d'API éligible et aucun commentaire auparavant valable supprimé. Un résultat en dessous du seuil échoue : il n'est pas corrigé en baissant le seuil ni en masquant CS1591.

L'exécution historique [pilote DocFX #41212901369](https://github.com/TobbyCy/RomothequeManager/actions/runs/41212901369) prouve uniquement la faisabilité native sur un ancien SHA. Une CI au SHA actuel doit réellement construire XML/YAML/HTML et contrôler liens/UID, sans scanner de données personnelles.

Le passage en état **genuinely required** du check `ci-policy-gate` relève de la protection de branche GitHub contrôlée par le propriétaire. Un workflow présent ne suffit pas : si la protection est invérifiable, déclarer `BLOCKED_BRANCH_RULE`.
