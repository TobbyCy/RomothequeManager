using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Pure ES-DE provenance adapter. Never searches disk, opens XML, matches by
/// title, grants media access or approves a cover. All paths are source-relative.</summary>
internal static class EsDeDeclaredMediaClaims
{
    /// <summary>Build only claims literally declared in an already imported ES-DE
    /// snapshot and referring to one unambiguous captured ROM path and platform.</summary>
    internal static IReadOnlyList<ExplicitMediaReference> Build(
        IReadOnlyList<FileObservation> roms,
        EsDeCatalogueSnapshot? imported)
    {
        ArgumentNullException.ThrowIfNull(roms);
        if (imported is null) return Array.Empty<ExplicitMediaReference>();

        var result = new List<ExplicitMediaReference>();
        var uniqueRoms = roms
            .Where(r => r.Kind is InventoryKind.PotentialRom or InventoryKind.Playlist
                or InventoryKind.ArchiveCandidate)
            .Where(r => SafeRelative(r.RelativePath, "ROMS/"))
            .GroupBy(r => (r.PlatformKey, r.RelativePath))
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());

        // Duplicate source rows cannot assert a unique, attributable claim.
        var uniqueGames = imported.Games
            .Where(g => SafeRelative(g.RomRelativePath, "ROMS/"))
            .GroupBy(g => (g.Platform, g.RomRelativePath))
            .Where(group => group.Count() == 1)
            .Select(group => group.Single());

        foreach (var game in uniqueGames)
        {
            if (!uniqueRoms.TryGetValue((game.Platform, game.RomRelativePath), out var rom))
                continue;
            foreach (var declared in game.Media)
            {
                if (!string.Equals(declared.Kind, "image", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(declared.Kind, "thumbnail", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!SafeRelative(declared.RelativePath, "MEDIAS/")) continue;

                result.Add(new ExplicitMediaReference(rom.ObservationId,
                    declared.RelativePath, "ESDE_IMPORTED_XML",
                    "DECLARED:" + imported.ContentFingerprint));
            }
        }
        return Array.AsReadOnly(result
            .Distinct()
            .OrderBy(c => c.RomObservationId, StringComparer.Ordinal)
            .ThenBy(c => c.MediaRelativePath, StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary>Conservative lexical boundary only: it never proves on-disk scope
    /// or access. Native readers independently enforce handle-based confinement.</summary>
    private static bool SafeRelative(string? value, string requiredPrefix)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(requiredPrefix, StringComparison.Ordinal) ||
            value.Contains('\\') || value.Contains(':') || value.Contains('\0'))
            return false;
        var parts = value.Split('/');
        return parts.Length >= 3 && parts.All(p => p.Length > 0 &&
            p != "." && p != "..");
    }
}
