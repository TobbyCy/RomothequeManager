using Microsoft.Win32.SafeHandles;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.RsA;

namespace RomothequeManager.V2.Inventory.Media;

/// <summary>Media access is distinct from ROMS inventory: even a valid ROMS grant
/// cannot index images or authorize previews.</summary>
internal enum IdealMediaOperation
{
    /// <summary>Metadata/header index of a separately selected MEDIAS root.</summary>
    Index,
    /// <summary>Read one approved observation for bounded image decoding.</summary>
    Preview
}

/// <summary>Volatile one-shot native media capability, never serialized into SQLite,
/// XML, settings, environment variables or a ViewModel.</summary>
internal sealed class IdealMediaReadGrant(
    IdealMediaOperation operation, string rootId, long selectionVersion,
    DateTimeOffset expiresUtc, RsAHandleIdentity nativeIdentity,
    string? runId = null, string? observationId = null)
{
    private int _claimed;
    private int _revoked;

    /// <summary>The only operation this capability can authorize.</summary>
    internal IdealMediaOperation Operation { get; } = operation;
    /// <summary>Logical role-scoped root identity, not a physical path grant.</summary>
    internal string RootId { get; } = rootId;
    /// <summary>Generation invalidated whenever the selected root changes.</summary>
    internal long SelectionVersion { get; } = selectionVersion;
    /// <summary>Short, bounded permission expiry.</summary>
    internal DateTimeOffset ExpiresUtc { get; } = expiresUtc;
    /// <summary>Identity of a natively opened MEDIAS root captured at consent.</summary>
    internal RsAHandleIdentity NativeIdentity { get; } = nativeIdentity;
    /// <summary>Exact media-index run required for a preview.</summary>
    internal string? RunId { get; } = runId;
    /// <summary>Exact indexed observation, not an arbitrary path.</summary>
    internal string? ObservationId { get; } = observationId;

    /// <summary>Revoke before Cancel, selection replacement or session Dispose.</summary>
    internal void Revoke() => Interlocked.Exchange(ref _revoked, 1);

    /// <summary>Check operation, root, generation and expiry without granting access.</summary>
    internal bool Valid(IdealMediaOperation requested, string rootId, long version) =>
        Volatile.Read(ref _revoked) == 0 && DateTimeOffset.UtcNow < ExpiresUtc &&
        Operation == requested && RootId == rootId && SelectionVersion == version;

    /// <summary>Atomically consume this one-shot media capability.</summary>
    internal bool TryClaim(IdealMediaOperation requested, string rootId, long version) =>
        Valid(requested, rootId, version) &&
        Interlocked.CompareExchange(ref _claimed, 1, 0) == 0 &&
        Valid(requested, rootId, version);
}

/// <summary>Prepared independent MEDIAS/PREVIEW consent boundary. This class does
/// not scan files or decode pixels: a later native reader must call ClaimRoot
/// before any enumeration, and release the resulting handle after its operation.
/// No public runtime API activates unfinished media access.</summary>
internal sealed class IdealMediaGrantSession : IDisposable
{
    private readonly object _sync = new();
    private string? _mediaRoot;
    private string? _rootId;
    private long _generation;
    private IdealMediaReadGrant? _grant;
    private MediaIndexSnapshot? _verified;
    private bool _disposed;

    /// <summary>Current selection generation, invalidated on every selection attempt.</summary>
    internal long Generation { get { lock (_sync) return _generation; } }

    /// <summary>Select a local MEDIAS root explicitly; selection alone cannot issue
    /// permission, scan files or open an image.</summary>
    internal void Select(string selectedMediaRoot, string roleRootId)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            RevokeLocked();
            _generation++;
            _mediaRoot = null;
            _rootId = null;
            _verified = null;
            if (!Guid.TryParse(roleRootId, out _))
                throw new ArgumentException("MEDIA_DURABLE_ROOT_ID_REQUIRED");
            _mediaRoot = RsAAnchoredReader.ValidateSelectedLocalMediaPath(selectedMediaRoot);
            _rootId = roleRootId;
        }
    }

    /// <summary>Issue a one-shot MEDIAS INDEX permission only on a separate,
    /// affirmative action, bound to the natively opened root identity.</summary>
    internal IdealMediaReadGrant AuthorizeIndex(bool explicitUserAction,
        DateTimeOffset expiry)
    {
        lock (_sync)
            return Issue(IdealMediaOperation.Index, explicitUserAction, expiry, null, null);
    }

    /// <summary>Issue an independent PREVIEW permission for precisely one
    /// observation from the complete, currently bound MEDIAS index.</summary>
    internal IdealMediaReadGrant AuthorizePreview(string runId,
        string observationId, bool explicitUserAction, DateTimeOffset expiry)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_verified is null || _verified.RunId != runId ||
                _verified.MediaRootRef != _rootId ||
                _verified.ScanStatus != InventoryScanStatus.Complete ||
                _verified.Availability != InventoryAvailability.Online ||
                _verified.Freshness != InventoryFreshness.Fresh ||
                _verified.Observations.Count(x =>
                    x.MediaObservationId == observationId &&
                    x.MediaRootRef == _rootId &&
                    x.RelativePath.StartsWith("MEDIAS/", StringComparison.Ordinal)) != 1)
                throw new UnauthorizedAccessException("IDEAL_MEDIA_PREVIEW_NOT_INDEXED");
            return Issue(IdealMediaOperation.Preview, explicitUserAction, expiry,
                runId, observationId);
        }
    }

    /// <summary>Bind a complete native index result only from the selected
    /// generation. SQLite restore, XML references and ROMS inventory never call this.</summary>
    internal bool AcceptNativeIndex(MediaIndexSnapshot indexed, long generation)
    {
        ArgumentNullException.ThrowIfNull(indexed);
        lock (_sync)
        {
            if (_disposed || _rootId is null || _generation != generation ||
                indexed.MediaRootRef != _rootId ||
                indexed.ScanStatus != InventoryScanStatus.Complete ||
                indexed.Availability != InventoryAvailability.Online ||
                indexed.Freshness != InventoryFreshness.Fresh ||
                indexed.Observations.Any(x =>
                    x.MediaRootRef != _rootId || !x.RelativePath.StartsWith("MEDIAS/",
                        StringComparison.Ordinal) ||
                    x.RelativePath.Split('/').Any(p => p is "." or ".." or "")))
                return false;
            _verified = indexed;
            return true;
        }
    }

    /// <summary>Consume the exact operation and reopen the selected root by
    /// natively anchored handles; refuse a changed root identity or stale grant.</summary>
    internal SafeFileHandle ClaimRoot(IdealMediaReadGrant grant, IdealMediaOperation operation,
        long generation, string? runId = null, string? observationId = null)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (grant is null || _mediaRoot is null || _rootId is null ||
                !ReferenceEquals(grant, _grant) ||
                !grant.Valid(operation, _rootId, generation) ||
                (operation == IdealMediaOperation.Preview &&
                 (grant.RunId != runId || grant.ObservationId != observationId)))
                throw new UnauthorizedAccessException("IDEAL_MEDIA_GRANT_INVALID");
            var root = RsAAnchoredReader.OpenSelectedLocalMediaRoot(_mediaRoot);
            if (!RsAAnchoredReader.Identity(root).Equals(grant.NativeIdentity) ||
                !grant.TryClaim(operation, _rootId, _generation))
            {
                root.Dispose();
                throw new UnauthorizedAccessException("IDEAL_MEDIA_ROOT_IDENTITY_CHANGED");
            }
            return root;
        }
    }

    /// <summary>Revoke all media authority before cancellation or changing the library.</summary>
    internal void Revoke()
    {
        lock (_sync)
        {
            if (_disposed) return;
            RevokeLocked();
            _verified = null;
        }
    }

    private IdealMediaReadGrant Issue(IdealMediaOperation operation,
        bool affirmative, DateTimeOffset expiry, string? runId, string? observationId)
    {
        ThrowIfDisposed();
        if (!affirmative || _mediaRoot is null || _rootId is null ||
            expiry <= DateTimeOffset.UtcNow ||
            expiry > DateTimeOffset.UtcNow.AddMinutes(2))
            throw new UnauthorizedAccessException("IDEAL_MEDIA_EXPLICIT_CONSENT_REQUIRED");
        using var opened = RsAAnchoredReader.OpenSelectedLocalMediaRoot(_mediaRoot);
        var identity = RsAAnchoredReader.Identity(opened);
        _grant?.Revoke();
        return _grant = new IdealMediaReadGrant(operation, _rootId, _generation,
            expiry, identity, runId, observationId);
    }

    private void RevokeLocked() { _grant?.Revoke(); _grant = null; }
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(IdealMediaGrantSession));
    }

    /// <summary>Dispose revokes grants; no native handle remains owned by this session.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            RevokeLocked();
            _verified = null;
            _rootId = null;
            _mediaRoot = null;
            _disposed = true;
        }
    }
}
