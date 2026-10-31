using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RomothequeManager.V2.Tests")]

namespace RomothequeManager.V2;

// #141 RED STAGE: the old imported media paths still call Bitmap(Stream) directly.
// Test-first contract seam. The preflight is intentionally non-enforcing at this
// commit to establish the Windows failure of LM-01/LM-02 BEFORE the guard is written.
// This is NOT a completed safety control and must not be merged in this state.
internal static class LegacyImportedImageBudget
{
    internal const int MaxCompressedBytes = 4 * 1024 * 1024;
    internal const int MaxSidePixels = 2048;
    internal const long MaxSourcePixels = 2_097_152;
    internal const long MaxViewBytes = 12L * 1024 * 1024;
    internal static bool Preflight(byte[] bytes, out string reason)
    {
        reason = "LM_GUARD_NOT_IMPLEMENTED_RED_STAGE";
        return true;
    }
}
