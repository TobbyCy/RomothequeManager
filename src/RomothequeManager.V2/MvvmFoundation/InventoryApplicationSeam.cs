using RomothequeManager.V2.Inventory;

namespace RomothequeManager.V2.Inventory.Application;

// NEXT-03 FOUNDATION ONLY. This mirrors the PROPOSED DEV 1 contract in #83.
// DEV 1 owns its definitive assembly and implementation. Move/replace this
// declaration at integration, never maintain two contracts or two scanners.
public enum InventoryRunPhase
{
    NoSelection, Ready, Running, CancellationRequested, Completed, Rejected, Faulted
}

public sealed record InventoryApplicationState(
    long SelectionVersion,
    InventoryRunPhase Phase,
    string? SelectedLocalRoot,
    InventorySnapshot? Current,
    InventorySnapshot? LastAttempt,
    string? SafeDiagnosticCode,
    bool ProgressIsIndeterminate);

public sealed record InventorySelectionResult(
    bool Accepted, string? SafeDiagnosticCode, long SelectionVersion);

public sealed record InventoryRunRequest(
    long SelectionVersion, bool ExplicitUserConsent);

public interface IInventoryApplicationService : IAsyncDisposable
{
    InventoryApplicationState State { get; }
    event EventHandler<InventoryApplicationState>? StateChanged;

    InventorySelectionResult PrepareSelection(string selectedLocalRoot, InventoryScope settings);
    Task<InventoryApplicationState> StartConfirmedScanAsync(
        InventoryRunRequest request, CancellationToken cancellationToken = default);
    bool RequestCancellation();
    bool ClearSelection();
}
