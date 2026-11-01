namespace RomothequeManager.V2;

/// <summary>
/// Orchestrates explicitly requested, read-only ES-DE imports. It never selects a root,
/// grants filesystem access, scans ROM contents, or persists a library.
/// </summary>
/// <remarks>
/// The caller stages a result off the UI thread and adopts it only after its own
/// cancellation and lifetime checks. File validation and media-path policy remain
/// exclusively in <see cref="EsDeImportSession"/>.
/// </remarks>
public interface IEsDeImportApplicationService
{
    /// <summary>Prepare one explicitly selected catalogue without adopting it in the UI.</summary>
    Task<EsDeImportSession> PrepareAsync(string selectedRoot, CancellationToken cancellationToken);

    /// <summary>
    /// Refresh an existing session, preserving its identity. Cancellation cannot interrupt
    /// the synchronous ES-DE parser, so UI callers must reject a late result after await.
    /// </summary>
    Task<EsDeCatalogueSnapshot> RefreshAsync(EsDeImportSession session, CancellationToken cancellationToken);
}

/// <summary>Default import orchestrator using the unchanged ES-DE read-only backend.</summary>
public sealed class EsDeImportApplicationService : IEsDeImportApplicationService
{
    /// <inheritdoc/>
    public Task<EsDeImportSession> PrepareAsync(string selectedRoot, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prepared = new EsDeImportSession(selectedRoot);
            prepared.Import();
            cancellationToken.ThrowIfCancellationRequested();
            return prepared;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<EsDeCatalogueSnapshot> RefreshAsync(EsDeImportSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var updated = session.Import();
            cancellationToken.ThrowIfCancellationRequested();
            return updated;
        }, cancellationToken);
    }
}
