using System.Collections.ObjectModel;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Fixed, source-neutral folder conventions for IDEAL ROMS and MEDIAS
/// observations. A key only classifies a chosen folder; it neither authorizes
/// reading nor proves a game identity or a media association.</summary>
internal static class IdealPlatformFolderConventions
{
    // Explicitly supported ES-DE-style folder names. Unknown folders remain
    // UNKNOWN and cannot produce a convention-based media association.
    /// <summary>Explicit folder-to-platform classifications; no read permission or identity proof.</summary>
    internal static IReadOnlyDictionary<string, string> PlatformAliases { get; } =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["gb"] = "gb", ["gbc"] = "gbc", ["gba"] = "gba",
                ["nes"] = "nes", ["snes"] = "snes", ["n64"] = "n64",
                ["gc"] = "gc", ["gamecube"] = "gc", ["wii"] = "wii",
                ["wiiu"] = "wiiu", ["nds"] = "nds", ["3ds"] = "3ds",
                ["psx"] = "psx", ["ps1"] = "psx", ["ps2"] = "ps2",
                ["ps3"] = "ps3", ["psp"] = "psp", ["psvita"] = "psvita",
                ["xbox"] = "xbox", ["xbox360"] = "xbox360",
                ["megadrive"] = "megadrive", ["genesis"] = "megadrive",
                ["mastersystem"] = "mastersystem",
                ["saturn"] = "saturn", ["dreamcast"] = "dreamcast",
                ["arcade"] = "arcade", ["mame"] = "mame",
                ["pcengine"] = "pcengine", ["atari2600"] = "atari2600"
            });

    // No generic "media" or platform folder is automatically a cover.
    // Only explicitly recognized subfolders can be preview candidates.
    /// <summary>Explicit media subfolder kinds: only a cover folder produces cover candidates.</summary>
    internal static IReadOnlyDictionary<string, MediaKind> FolderKinds { get; } =
        new ReadOnlyDictionary<string, MediaKind>(
            new Dictionary<string, MediaKind>(StringComparer.OrdinalIgnoreCase)
            {
                ["covers"] = MediaKind.Cover,
                ["cover"] = MediaKind.Cover,
                ["thumbnails"] = MediaKind.Thumbnail,
                ["logos"] = MediaKind.Logo,
                ["backgrounds"] = MediaKind.Background,
                ["screenshots"] = MediaKind.Screenshot
            });
}
