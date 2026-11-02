# Guide de démarrage pour développeurs

**État courant : NEXT-05 sur `main=7aa62c8f` (02.11.2026)**. Ce guide remplace en première lecture l'ancien instantané **POC V2-01A/B du 23.10 (31 MSTest)** : ces chiffres ne représentent plus la base actuelle. Le code NEXT-05 est validé **sur fixtures synthétiques**, distinct de la [Release Alpha v0.4.0](../../README.md#télécharger-et-essayer) et du [candidat bêta non publié](../../STATUS.md#blocage-du-candidat-bêta-154--ne-pas-confondre-les-preuves).

## Prérequis et restauration

| Domaine | Environnement et contrainte |
| --- | --- |
| Application Avalonia Windows | **SDK .NET `10.0.401` exact** imposé par `global.json` ; Avalonia **12.1.4** ; projet `src/RomothequeManager.V2/RomothequeManager.V2.csproj` |
| Tests .NET | `tests/dotnet/RomothequeManager.V2.Tests/` ; restauration NuGet via `packages.lock.json` et `--locked-mode` |
| Moteur historique E1 | **Windows PowerShell 5.1 Desktop**, Pester **4.10.1** pour ses contrats ; tests synthétiques uniquement |
| Scripts de contrôle | Git, Python 3, PowerShell sur Windows ; Node.js pour les tests/générateurs qui en ont besoin, **pas** pour utiliser l'Alpha publiée |
| DocFX | SDK .NET exact, **DocFX 2.81.0** épinglé, Roslyn/MSBuild et job Windows natif ; aucun site API publié |

**Runtime et dépendances vérifiés :** le SDK de construction est `10.0.401` ; le ZIP self-contained `win-x64` emploie le runtime embarqué **.NET 10.0.12** (ne pas le confondre avec le runtime **10.0.11** du laboratoire Avalonia minimal). Avalonia Desktop/Themes.Fluent **12.1.4**, SkiaSharp **3.119.4**, Microsoft.Data.Sqlite **10.0.14**, SQLitePCLRaw **2.1.13**. Le graphe SQLite est contrôlé par **six NUPKG, cinq DLL et quatre textes légaux** avec manifeste de provenance, mais `LEGAL_PUBLISH_HOLD` et l'approbation binaire distincte restent nécessaires : un inventaire juridique intégré ne publie aucun ZIP.

Dans un clone **sans ROMs, BIOS, médias ni chemins personnels**, exécuter depuis la racine :

```powershell
git status --short
dotnet --version                           # doit afficher exactement 10.0.401
dotnet restore tests/dotnet/RomothequeManager.V2.Tests/RomothequeManager.V2.Tests.csproj --locked-mode
dotnet test tests/dotnet/RomothequeManager.V2.Tests/RomothequeManager.V2.Tests.csproj -c Release --no-restore
python -B tests/ci_policy.py dotnet
```

Le contrôle `dotnet --version` doit être effectué **dans le dépôt** (résolution de `global.json`). Un test local réussi n'autorise ni une Release, ni une permission réelle. Les tests de référence sur `main=7aa62c8f` et les jobs policy/.NET/DocFX requis sont [SUCCESS](https://github.com/TobbyCy/RomothequeManager/actions/runs/41465566095) ; l'état de toute nouvelle branche exige **sa propre CI au SHA exact**.

## Architecture intégrée et frontières

- **ES-DE** : `IEsDeImportApplicationService` et `ImportedCatalogueViewModel` pilotent le parcours de consentement, import et refresh ; `EsDeImportSession` demeure l'autorité de lecture XML et de `TryOpenMedia`. L'import ne commence **jamais** au lancement. `PrepareRefresh` prépare un nouveau snapshot sans remplacer `Current` ; l'adoption est gardée contre Cancel, Dispose, changement de sélection et résultats tardifs.
- **Inventaire IDEAL** : `InventoryApplicationService` et VM séparent préflight/consentement, scan, état et présentation ; les observations ROMS restent bornées à leur racine explicitement autorisée **dans les fixtures**. Maintenir `CurrentComplete` lors d'un `LastAttempt` partiel, échoué ou Offline/Stale.
- **Médias** : MI-A et MI-B séparent les rôles `ROMS`, `MEDIAS` et `PREVIEW`. La voie historique ES-DE du lot B utilise `ILegacyImportedMediaLoader` avec `EsDeImportSession.TryOpenMedia`, décodeur PNG/JPEG borné et `LegacyImportedMediaViewModel` sans I/O ; la View possède les `Bitmap` transférés. Aucun scraper, aucune attribution automatique. La preuve [B-05 760×480](https://github.com/TobbyCy/RomothequeManager/pull/151) est sur fixtures, pas une recette générale d'accessibilité.
- **SQLite/PERSIST-01** : `ICatalogStore` conserve snapshots, métadonnées/provenances et décisions manuelles ; migrations et backup/restore contrôlés sur **stockage synthétique jetable uniquement**. Le constructeur public refuse le stockage personnel ; test de réouverture `Offline/Stale` n'implique pas un nouveau scan ou rattachement de support.
- **Code UI** : couche Avalonia pour sélecteurs natifs, dispatcher, focus, rendu et ownership ; VM pour commandes/identité/générations/annulation ; services injectables pour I/O autorisées dans leur périmètre. Aucun élargissement d'accès depuis une simple nouvelle vue ou un résultat de test.

Sources : [frontières MVVM ES-DE](../architecture/next05-mvvm145-esde-import.md), [contrat SQLite](../architecture/persist01-sqlite.md), [MI-B et permissions](../architecture/next05-mib-fixtures-backend.md), [parcours UX NEXT-05](../ux/next05-ideal-parcours.md) et [manuel utilisateur prudent](../user/parcours-ideal-next05.md). Les documents de conception NEXT-03 restent historiques.

## Gates CI Windows et DocFX

La **CI adaptative** classe les chemins puis impose les jobs applicables. Pour un changement de code/contrat, vérifier build .NET Release, restaurations verrouillées, tests réellement exécutés, policy/doc, packaging de test et le **required `ci-policy-gate`**. Pour les guides seuls, les contrôles documentaires appropriés restent obligatoires. Un `SKIPPED` acceptable pour un job non applicable ne remplace jamais un check requis.

Au `push/main=7aa62c8f` ([run #41465566095](https://github.com/TobbyCy/RomothequeManager/actions/runs/41465566095)), **335/335 MSTest PASS**, DocFX `D/N=857/935=91,66 %` et `0/0` nouvelle API ; sur #154 ([run #41482716933](https://github.com/TobbyCy/RomothequeManager/actions/runs/41482716933)), DocFX **860/938=91,68 %** et **3/3 nouvelles API** passent, sans valider la CI globale. Les signatures décrivent des **API typées**, non une signature du binaire Windows.

La chaîne **DocFX permanente**, fusionnée via #148 et fiabilisée dans #149, construit **XML C# → YAML → HTML**, vérifie les UID/liens et mesure la couverture Roslyn sémantique à partir des vrais manifestes MSBuild HEAD + base. Elle exige **`10 × D ≥ 9 × N`** (≥90 %), **100 % des nouveaux IDs éligibles** et aucune perte silencieuse de documentation ou d'API. Les membres `internal` non privés sont comptés ; seules les sources **Avalonia générées avec provenance attestée** peuvent être exclues. Les signatures, surcharges et métadonnées ECMA-335 sont croisées. Les rapports temporaires du job `ci-docfx` incluent `gate.json`, `report.md`, `head.json`, `base.json` et les exclusions. **Ne pas reprendre un ancien N/D pour un autre HEAD.**

Documentation de référence : [DocFX permanent](docfx-permanent.md), [CI adaptative](ci-adaptative.md), [préflight fictif](preflight-synthetique.md), [workflow Git](workflow.md) et [inventaire licences](lic-zip-01-inventory.md). Ne pas changer `ci.yml`, les exclusions, les checks ou les seuils pour transformer un échec en PASS.

## Distribution : Alpha publique, ZIP POC et candidat bêta

La **Release v0.4.0-alpha.1** est téléchargeable et testée sur le périmètre NEXT-04.1. L'ancien **ZIP V2-01C POC** constitue un jalon antérieur distinct. La PR [#154 candidate IDEAL](https://github.com/TobbyCy/RomothequeManager/pull/154) prépare un ZIP `win-x64` autonome en phase **`validate`**, avec deux builds contrôlés et des notices SQLite, **en quarantaine et non téléversé**. Elle n'est **pas** une nouvelle Release.

Au HEAD #154 `f27bbe71`, la [CI Windows #41482716933](https://github.com/TobbyCy/RomothequeManager/actions/runs/41482716933) reste **8/10 SUCCESS** : DocFX **860/938 = 91,68 % et nouvelles API 3/3**, probes de packaging et SQLite natif PASS, mais démarrage du vrai EXE extrait sous utilisateur standard **timeout après 90 s**, sans preuve `MainWindow.Opened`. Le job `.NET` et `ci-policy-gate` échouent. Les essais Avalonia minimalistes du laboratoire ne démontrent pas le fonctionnement de ce ZIP. **La cause reste NOT_VERIFIED** et la recette dédiée [#95](https://github.com/TobbyCy/RomothequeManager/issues/95) sur fixtures n'a pas eu lieu.

La revue finale indépendante DEV 2 de #154 est **`CHANGES_REQUIRED / OWNER_DECISION_REQUIRED`** ([revue sur le HEAD exact](https://github.com/TobbyCy/RomothequeManager/pull/154#pullrequestreview-5741436816)). Les preuves WTS/Win32 et SQLite natif non-admin sont positives, mais aucun marqueur GUI du vrai EXE extrait n'a été récupéré avant le timeout. Aucun défaut de GPU, runtime ou profil n'est identifié comme cause certaine.

Préparation seulement : [protocole opérateur #95](https://github.com/TobbyCy/RomothequeManager/issues/95#issuecomment-6585108522) et [guide candidat sur la branche #154](https://github.com/TobbyCy/RomothequeManager/blob/f27bbe716275a7ab55e9a189ed5a7fc0b9f41dae/docs/developer/beta-ideal-validate.md). Ce dernier n'est pas encore intégré à `main` : **ne pas le présenter comme une fonction distribuée**. La provenance SHA du ZIP candidat doit être établie sur ses **octets exacts**, pas déduite du seul SHA source.

## Contribuer et documenter

Une PR reçoit uniquement des fixtures synthétiques ; publier **SHA source exact + run Windows + nombre de tests + résultats négatifs + limites**. Distinguer `Complete` du résultat actuel, `CurrentComplete` conservé et disponibilité physique ; ne jamais transformer `OFFLINE/STALE` en bibliothèque vide. Préserver licences MIT du code et notices propriétaires des [assets graphiques](../../BRANDING.md) ; aucun import de médias tiers par défaut.

Avant toute modification de code ou de documentation, lire [CONTRIBUTING](../../CONTRIBUTING.md), l'[état produit](../../STATUS.md) et le [suivi #146](https://github.com/TobbyCy/RomothequeManager/issues/146). **Pas de fusion autonome, de changement de permissions ou de Release sans GO propriétaire.**
