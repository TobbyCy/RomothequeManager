# NEXT-03 — branche backend de préparation (DEV 1)

**Statut : `NEXT03_BACKEND_BRANCH_PREPARATION` / raccordement Avalonia **HOLD**.**
Branche : `dev1/next03-backend-preparation`, créée depuis `main=0d42e61af9b1aa50659e6e503cb91cebc52083c3`.
Contrats : [#82](https://github.com/TobbyCy/RomothequeManager/issues/82) (fonctionnel, NX03-01..11 P1), [#83](https://github.com/TobbyCy/RomothequeManager/issues/83) (architecture DEV 1), [#80](https://github.com/TobbyCy/RomothequeManager/issues/80) (DEV 3).

## Livrables présents sur cette branche

- `src/RomothequeManager.Inventory.Core/RomothequeManager.Inventory.Core.csproj` : bibliothèque .NET 10 **sans références NuGet/Avalonia/MVVM**, autonome. Compile **exactement** le source moteur déjà présent `../RomothequeManager.V2/InventoryReader.cs` via `Compile Include` (aucune copie physique, modification ou scanner parallèle).
- `src/RomothequeManager.Inventory.Core/InventoryApplicationService.cs` : `IInventoryApplicationService` et `InventoryApplicationService` avec `PrepareSelection(InventoryScope)`, `StartConfirmedScanAsync(InventoryRunRequest, CancellationToken)`, `RequestCancellation()`, `ClearSelection()`, `State`, `StateChanged`, `DisposeAsync()`. Le service est **indépendant du framework UI**, le futur ViewModel devra dispatcher l'événement `StateChanged` vers Avalonia. Types publics : `InventoryRunPhase`, `InventoryApplicationState`, `InventorySelectionResult`, `InventoryRunRequest`.
- `tests/next03/` : banc smoke autonome .NET 10, génération Node des **trois dossiers physiques synthétiques**, hash **uniquement de fixtures** avant/après, vérification des oracles 32/33/32, 13 plateformes via alias explicites, 3 groupes de doublons CHAOS, MYSTERY sans XML, refus sans consentement, défaut **deny-by-default**, single-flight, annulation effective pendant une playlist, `Current` préservé et sélection non remplacée en cours de scan.
- `.github/workflows/next03-backend-preparation.yml` : preuve Windows synthétique **uniquement sur cette branche**, .NET SDK 10.0.401 et Node 20+, **aucun ZIP publié/uploadé**, aucun accès aux vraies collections, aucun merge ni Release.

**Différence essentielle entre source et programme livré :** l'application Avalonia actuelle **ne référence PAS cette bibliothèque de préparation**. Pour l'instant, le même source NEXT-02 est encore compilé dans le WinExe historique et dans le Core construit séparément par le banc de préparation. **Les deux assemblies ne sont pas chargés ensemble et aucun scanner concurrent n'est ajouté au produit.** Le raccordement final exigera **la suppression de la compilation du fichier moteur dans le WinExe** et **l'ajout d'un ProjectReference vers le Core**, de sorte qu'il n'existe qu'une autorité moteur par application. Ne pas annoncer ce raccordement comme implémenté actuellement.

## Sémantique applicative (contrat DEV 3)

1. **`PrepareSelection`** : appelé uniquement après clic du picker. Vérifie métadonnées de la racine locale et `ROMS/`, rejet des liens/URI/UNC, copie les alias ; **aucune énumération ROMS/ ni XML**. L'annulation du picker doit préserver la sélection initiale : ne pas appeler `PrepareSelection`.
2. **`StartConfirmedScanAsync(new InventoryRunRequest(version, true))`** : demande une autorisation à chaque scan ; vérifie le numéro de sélection, refuse la réentrée et délègue **une seule** exécution synchrone `InventorySession.Scan(token)` au thread de fond. `ProgressIsIndeterminate=true` pendant l'exécution, pas de `IProgress` ni pourcentage inventé.
3. **`RequestCancellation`** : phase `CancellationRequested`, CTS demandée ; la phase n'est pas `Completed` avant le retour réel. Le moteur définit `Current` (dernier `Complete`) et `LastAttempt` (dernière tentative `Complete/Partial/Failed`).
4. **`StateChanged`** : peut arriver depuis worker. Souscriptions côté ViewModel doivent mettre à jour l'écran via `Dispatcher.UIThread` et se détacher à la fermeture ; aucune dépendance Avalonia/MVVM côté backend. Les événements ne doivent pas publier des chemins privés en CI.
5. **`ClearSelection`** : refuse une sélection courante pendant une activité, invalide `SelectionVersion` après achèvement. Aucune réassociation USB, persistance ou jointure avec ES-DE.
6. **Sécurité :** le constructeur **public refuse TOUS les appels de scan** (`REAL_LIBRARY_SCAN_HOLD`) même après consentement. Seul le banc interne de tests dispose d'une autorisation **scopée aux chemins synthétiques temporaires** ; aucune variable d'environnement, fichier manifest ou case UI ne déverrouille des scans réels. **Le futur branchement produit ne doit pas contourner ce HOLD** sans décision séparée du Contrôleur et revue des risques TOCTOU.

### Distinction irréductible des provenances

- `InventorySnapshot` = observation `FILESYSTEM_OBSERVATION` sous `ROMS/`, `ConfirmedGameCount=null`, `CoverageProof` des seuls chemins couverts ; `ObservedFileCount` n'est pas un nombre de jeux.
- `EsDeCatalogueSnapshot` = références XML **seulement après import explicite**, jamais appelées par ce backend. Les 14 PNG absents du manifeste CHAOS sont un oracle global, pas 14 diagnostics NEXT-02.
- `PotentialDuplicateGroup` = **candidat** sans empreinte ROM binaire, jamais action de suppression/fusion.
- Statuts `ScanStatus`, `Availability`, `Freshness` indépendants. `TotalFileCount=null` si Partial/Failed, valeur zéro permise uniquement pour une vraie portée complète vide.

## Checkpoints pour l'Agent

**G0 — intégration préalable (bloquant) :** PR #76 → #75 → #77 sur `main` avec GO, contrôle diff et CI verte après chaque fusion. Risque connu : quatre conflits #75/#77 (`CHANGELOG.md`, `docs/product-status.md`, `tests/Invoke-CiStatic.ps1`, `tests/ci_policy.py`). Conserver l'union minimale des règles CI, les deux jeux de documentation et la recette Windows, sans toucher aux branches historiques.

**G1 — backend :** revue du contrat ci-dessus DEV 2 + DEV 3 ; vérifier la preuve Windows sur le HEAD exact de cette branche. Vérifier les tests `NX03-01..11` sur les fixtures et les limites documentées. Ne pas faire évoluer `InventoryReader.cs` sans défaut reproduit. Les cas `NX03-12..15` restent P2, `NX03-16` scan réel **HOLD**.

**G2 — raccordement (HOLD jusqu'au nouveau main) :** après intégration, envisager une nouvelle PR Draft de raccordement ciblée dans laquelle :
- modifier `src/RomothequeManager.V2/RomothequeManager.V2.csproj` : ajouter `ProjectReference` Core et `Compile Remove="InventoryReader.cs"` (ou déplacer la propriété de compilation après revue). **Jamais** deux assemblages NEXT-02 dans le même processus ;
- mettre à jour `src/RomothequeManager.V2/packages.lock.json`, `tests/dotnet/RomothequeManager.V2.Tests/packages.lock.json` et, si nécessaire, locks Core, sous restore verrouillé ;
- DEV 3 raccordera `MainWindow`, le picker et le futur `InventoryViewModel`, les vues Observation/Collection, en préservant `MainWindow.Imported.cs` et `MainWindow.LibraryUx.cs` ;
- adapter sans élargissement excessif les allowlists `tests/ci_policy.py`, `tests/Invoke-CiStatic.ps1` et les contrôles de source ; compléter `CHANGELOG.md`, `docs/product-status.md` ;
- relancer .NET tests existants, tests CORE, Pester et synthétiques, smoke Windows, publish/ZIP en quarantaine avec preuves SHA du HEAD **nouveau**, puis recette humaine (clavier, scroll, 760×480, hors-ligne).

## Commande locale / recette

Sous Windows PowerShell 5.1, Node.js 20+ et SDK .NET 10, à partir du dépôt :

```powershell
dotnet restore .\tests\next03\RomothequeManager.Inventory.Core.Smoke.csproj
dotnet run --project .\tests\next03\RomothequeManager.Inventory.Core.Smoke.csproj -c Release -- --repo .
```

Le runner génère et supprime **uniquement son propre répertoire temporaire GUID** ; il refuse une bibliothèque personnelle. Après fusion ultérieure #75, conserver la recette indépendante `Invoke-Demo02Recipe.ps1` pour préparer des fixtures de démonstration visibles à l'utilisateur ; le service public restera bloqué sur vraies ROMs tant que `NX03-16` n'a pas son GO.

**Résultat attendu : `NEXT03_BACKEND_BRANCH_READY / NO_AVALONIA / NO_MVVM / REAL_SCAN_HOLD / FINAL_WIRING_HOLD`.**
