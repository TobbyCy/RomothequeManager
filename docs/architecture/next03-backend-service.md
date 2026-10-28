# NEXT-03 — contrat backend DEV 1 / raccordement ViewModel

**Statut : PR Draft / service applicatif implémenté dans l'assembly existante** `RomothequeManager.V2` (.NET 10). Référence fonctionnelle [#82](https://github.com/TobbyCy/RomothequeManager/issues/82) et technique [#83](https://github.com/TobbyCy/RomothequeManager/issues/83).

**Décision propriétaire : pas d'extraction Core, pas de second scanner.** `src/RomothequeManager.V2/InventoryApplicationService.cs` référence directement les types du **seul** moteur `InventoryReader.cs` / `InventorySession` déjà compilé par `RomothequeManager.V2.csproj`. La **couche service elle-même** ne référence aucun namespace Avalonia, MVVM, ViewModel, importeur ES-DE ou framework de présentation. L'assembly qui la contient demeure l'application Avalonia : **indépendance du code et des interfaces, pas isolation au niveau assembly**. La branche expérimentale `dev1/next03-backend-preparation` (extraction Core) n'est pas fusionnée ni réutilisée dans ce diff.

## API publique exacte pour DEV 3

Namespace : `RomothequeManager.V2.Inventory.Application`.

```csharp
public enum InventoryRunPhase {
    NoSelection, Ready, Running, CancellationRequested, Completed, Rejected, Faulted
}
public sealed record InventorySelectionResult(
    bool Accepted, string? SafeDiagnosticCode, long SelectionVersion);
public sealed record InventoryRunRequest(
    long SelectionVersion, bool ExplicitUserConsent);
public sealed record InventoryApplicationState(
    long SelectionVersion,
    InventoryRunPhase Phase,
    string? SelectedLocalRoot, // UI locale seulement, jamais publier dans CI
    InventorySnapshot? Current,
    InventorySnapshot? LastAttempt,
    string? SafeDiagnosticCode,
    bool ProgressIsIndeterminate)
{
    public InventoryScanStatus? LastScanStatus { get; }
    public InventoryAvailability LastAvailability { get; }
    public InventoryFreshness LastFreshness { get; }
    public IReadOnlyList<FileObservation> Observations { get; }
    public IReadOnlyList<InventoryDiagnostic> Diagnostics { get; }
    public IReadOnlyList<InventoryCoverage> CoverageProof { get; }
    public int? TotalFileCount { get; }
    public int? ConfirmedGameCount { get; }
}
public interface IInventoryApplicationService : IAsyncDisposable {
    InventoryApplicationState State { get; }
    event EventHandler<InventoryApplicationState>? StateChanged;
    InventorySelectionResult PrepareSelection(InventoryScope selectedScope);
    Task<InventoryApplicationState> StartConfirmedScanAsync(
        InventoryRunRequest request, CancellationToken cancellationToken = default);
    bool RequestCancellation();
    bool ClearSelection();
}
```

Les getters dérivés de `InventoryApplicationState` ne changent **jamais** les valeurs du snapshot moteur : `LastScanStatus` est `null` sans tentative, `LastAvailability/LastFreshness` sont `Unknown` sans preuve ; `TotalFileCount` et `ConfirmedGameCount` restent **nullable**. Les collections `Observations`, `Diagnostics`, `CoverageProof` correspondent à `LastAttempt` et sont vides **sans snapshot**, sans affirmer un scan à zéro.

**Cycle d'utilisation du ViewModel :**

1. `IStorageFolder` choisi volontairement via `OpenFolderPickerAsync`. Après confirmation du choix et **avant** inventaire, `PrepareSelection(new InventoryScope(folder.Path.LocalPath, aliases...))` effectue un préflight **borné** (syntaxe de chemin, métadonnées racine/`ROMS/`, refus des liens/jonctions et paramètres de budget), **sans énumérer les ROMs ni importer de XML**. Si le picker est annulé, ne pas appeler le service.
2. Afficher la portée **`ROMS/` uniquement**, les budgets, la mention lecture seule, le chemin au **seul utilisateur local** ; demander une confirmation distincte **pour chaque tentative**.
3. `await StartConfirmedScanAsync(new InventoryRunRequest(selection.SelectionVersion, true), cancellationToken)`. L'orchestrateur refuse le second départ et appelle une fois `InventorySession.Scan(token)` via `Task.Run`, sans blocking du thread UI. `StateChanged` peut venir du worker : **poster sur `Dispatcher.UIThread`** dans le ViewModel (pas dans le service). Ne pas appeler `PrepareSelection` ou `ClearSelection` en phase `Running/CancellationRequested`.
4. `RequestCancellation()` publie `CancellationRequested`; attendre le snapshot réel avant d'afficher `Partial/CANCELLED`. Un `Partial/Failed` laisse `Current` complet intact, `LastAttempt` reflète uniquement la tentative ; ne pas promettre de persistance inter-session.
5. `ClearSelection()` après fin du scan invalide la version et réinitialise les observations/filtres/fiches de l'ancien contexte. `DisposeAsync()` attend la fin effective du scanner, puis retire les abonnements côté UI.

**HOLD d'exécution :** le constructeur **public** `new InventoryApplicationService()` **refuse les scans**, même avec consentement, avec code `REAL_LIBRARY_SCAN_HOLD`. Seul un constructeur `internal`, accessible à l'assembly de tests autorisée par `InventoryReader.cs`, permet d'injecter **une permission exacte sur les dossiers synthétiques fraîchement matérialisés dans un espace temporaire**. Aucun mode normal/flag d'environnement/fichier manifest ne débloque l'analyse d'un SSD réel. Le raccordement UI et son éventuel chemin d'exécution restent soumis à la décision propriétaire ; le service ne prétend pas être déjà fonctionnel sur une collection personnelle.

**Refus d'exécution observables :** hors scan actif, le service conserve désormais le refus dans `State` (`Phase=Rejected`, `SafeDiagnosticCode` neutre) et le transmet par `StateChanged`, sans effacer `Current` / `LastAttempt` ni inventer de snapshot. Une tentative concurrente reçoit un résultat `Rejected` sans interrompre ni remplacer l'état `Running` / `CancellationRequested`. Les notifications sont sérialisées même si un observateur demande l'annulation depuis un callback `Running` ; les états déjà supplantés ne sont pas émis. Le ViewModel doit néanmoins conserver sa garde de génération et son dispatch sur le thread UI. **DEV 3 : afficher le `SafeDiagnosticCode` du service** pour rendre visible notamment `REAL_LIBRARY_SCAN_HOLD`, sans exposer de chemin ou exception.

**Aucune progression chiffrée** : pas d'API `IProgress` sur NEXT-02 ; indicateur indéterminé seulement. Pas de doublon confirmé, de suppression, de jeu identifié inventé, de jointure automatique entre `FileObservation` et `EsDeGame`, ni de lecture de `MEDIAS/` par l'inventaire. Afficher les détails **ES-DE** uniquement par l'import volontaire existant. `Stale` est rapporté s'il existe **dans le snapshot source** ; le service ne fabrique ni historique persistant ni calcul de fraîcheur temporelle.

## Recette backend synthétique et matrice des responsabilités

`tests/dotnet/RomothequeManager.V2.Tests/InventoryApplicationServiceTests.cs` utilise le générateur canonique DEMO-01 **déjà sur main**, sans dépendance au script DEMO-02 de la PR #75. Il matérialise IDEAL/CHAOS/MYSTERY sous un nouveau répertoire temporaire, compare les SHA-256 **des seules fixtures fictives** avant/après et nettoie uniquement sa sortie GUID.

- **IDEAL** : 32 fichiers observés, 13 plateformes **si alias explicités**, 1 playlist et 2 membres, `ConfirmedGameCount=null` ; 30 références seulement si import ES-DE séparé.
- **CHAOS** : 33 fichiers, 3 `PotentialDuplicateGroup` (candidats), sans inventer suppression, ni jeu logique.
- **MYSTERY** : 32 fichiers malgré zéro XML, pas d'identité de jeu ni de jaquette inventée.
- **Concurrence** : seconde demande pendant `.m3u` bloquée, changement de sélection refusé jusqu'à fin, version de sélection propagée.
- **Annulation** : `Partial/CANCELLED` pendant une vraie lecture `.m3u`, dernier `Current` conservé et total `null`.
- **Échec et Offline** : racine synthétique temporairement indisponible après `Complete` → `LastAttempt.Failed/Offline`, `Current` antérieur intact. Pour `Stale`, la propagation du champ est testée avec un **snapshot synthétiquement tagué** `Stale`, **pas** prétendue issue d'un scanner qui détecterait l'âge d'un support.
- **Sécurité** : constructeur public refuse le scan, préflight avant énumération, consentement distinct, aucun scan réel ou effet sur fichiers sources.

Lancer depuis un clone Windows avec .NET 10 et Node.js 20+ :

```powershell
dotnet test .\tests\dotnet\RomothequeManager.V2.Tests\RomothequeManager.V2.Tests.csproj -c Release --property:RestoreLockedMode=true --filter 'FullyQualifiedName~InventoryApplicationServiceTests'
```

La **CI Adaptive** du dépôt reste le contrôle exhaustif (PowerShell 5.1, Pester, .NET, politiques et statique), à vérifier sur le HEAD exact de la PR. **La réussite du backend ne valide pas le futur parcours Avalonia**. Les tests GUI NX03-01..11 du contrat #82 sont distincts et non exécutés avant raccordement DEV 3 ; NX03-12..15 P2, NX03-16 scan réel **HOLD**.

## Intégration future et risques

Branche créée indépendamment de #75/#76/#77 ; ne modifier aucune de leurs branches. Le service, un seul fichier .cs, est auto-compilé par le projet existant : **pas de modification .csproj, .sln, verrou NuGet ni dépendance supplémentaire**. Après intégration distincte de #76 → #75 → #77 et stabilisation de `main`, DEV 3 peut rattacher son ViewModel au contrat ci-dessus sur une branche à part, après revue des permissions de test/demo et nouvelle CI au HEAD intégré.

**Risques résiduels :** `InventorySession` non thread-safe → single-flight ; annulation effective pendant lecture ; fermeture et événements worker ; racine/ancêtres reparse + substitution concurrente de chemins (TOCTOU non prouvée sûre sur vrais disques). Ne jamais qualifier le système de prêt au scan réel. Pas de remplacement moteur, persistance, import ES-DE implicite, scrapers, modification ROMs, fusion ni Release autorisés par cette PR.
