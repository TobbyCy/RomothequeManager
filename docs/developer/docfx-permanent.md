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

L'analyse de couverture est distincte de la couverture de tests Pester. Roslyn compte les déclarations source réellement éligibles (exportées et contrats internes explicitement répertoriés), sans compter deux fois les `partial`. Un commentaire ne compte que si le XML est valide et le `summary` non trivial. Les `inheritdoc` non résolus sont exclus du numérateur. Rapports par symbole et fichier dans l'artefact temporaire de CI.

Le gate impose **90 % minimum**, **100 % des nouveaux IDs éligibles documentés**, aucun recul du ratio, aucune disparition silencieuse d'API éligible et aucun commentaire auparavant valable supprimé. Un résultat en dessous du seuil échoue : il n'est pas corrigé en baissant le seuil ni en masquant CS1591.

L'exécution historique [pilote DocFX #41212901369](https://github.com/TobbyCy/RomothequeManager/actions/runs/41212901369) prouve uniquement la faisabilité native sur un ancien SHA. Une CI au SHA actuel doit réellement construire XML/YAML/HTML et contrôler liens/UID, sans scanner de données personnelles.

Le passage en état **genuinely required** du check `ci-policy-gate` relève de la protection de branche GitHub contrôlée par le propriétaire. Un workflow présent ne suffit pas : si la protection est invérifiable, déclarer `BLOCKED_BRANCH_RULE`.
