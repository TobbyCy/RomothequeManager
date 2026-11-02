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
    /// Prepares a read-only refresh for an accepted session without changing its Current
    /// or media-opening authority. The caller must verify cancellation and ownership
    /// before committing the opaque result on its UI context.
    /// </summary>
    /// <param name="session">The existing selected session whose LibraryId is preserved.</param>
    /// <param name="cancellationToken">Rejects the proposal before or after the synchronous parser.</param>
    /// <returns>An uncommitted proposal bound to this exact session and revision.</returns>
    Task<EsDeImportSession.PreparedEsDeRefresh> RefreshAsync(
        EsDeImportSession session, CancellationToken cancellationToken);
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

    /// <summary>Parse with the existing importer, never mutating the accepted session.</summary>
    /// <param name="session">The explicitly selected, already accepted ES-DE session.</param>
    /// <param name="cancellationToken">Allows a late parsed result to be discarded without commit.</param>
    /// <returns>A session-bound proposal; no media permission or Current change occurs here.</returns>
    public Task<EsDeImportSession.PreparedEsDeRefresh> RefreshAsync(
        EsDeImportSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = session.PrepareRefresh();
            cancellationToken.ThrowIfCancellationRequested();
            return candidate;
        }, cancellationToken);
    }
}
