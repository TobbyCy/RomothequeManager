using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

[TestClass]
public sealed class MediaIndexDiagnosticTests
{
    private static MediaIndexSnapshot Attempt(InventoryScanStatus status, params string[] codes) =>
        new("private-run", "private-root", DateTimeOffset.UtcNow, status,
            InventoryAvailability.Online, InventoryFreshness.Fresh,
            Array.Empty<InventoryCoverage>(), Array.Empty<MediaObservation>(),
            codes.Select(code => new InventoryDiagnostic(code, InventorySeverity.Warning,
                "PRIVATE_PATH", "PRIVATE_REASON", "IDEAL_NATIVE_MEDIA", DateTimeOffset.UtcNow)).ToArray(),
            0, null);

    [TestMethod]
    public void R4_KnownGlobalReasonsAreVisibleWithoutPrivateFields()
    {
        var codes = new[] { "MEDIA_INDEX_EXPLICIT_GRANT_REQUIRED", "MEDIA_INDEX_STALE",
            "MEDIA_INDEX_CANCELLED", "MEDIA_INDEX_TIMEOUT", "MEDIA_FILE_BUDGET",
            "MEDIA_DIRECTORY_BUDGET", "MEDIA_DEPTH_BUDGET", "MEDIA_TOTAL_HEADER_BUDGET",
            "MEDIA_INDEX_NATIVE_DENIED_OR_OFFLINE", "MEDIA_INDEX_NATIVE_FAILED" };
        var current = Attempt(InventoryScanStatus.Complete);
        foreach (var code in codes)
        {
            var view = MediaIndexAttemptDiagnosticView.From(Attempt(InventoryScanStatus.Partial, code), current);
            CollectionAssert.Contains(view.Codes.ToArray(), code);
            Assert.IsTrue(view.HasPreviousComplete);
            Assert.IsFalse(view.Text.Contains("PRIVATE", StringComparison.Ordinal));
            Assert.IsFalse(view.Text.Contains("private", StringComparison.Ordinal));
            StringAssert.Contains(view.Text, "Partial");
        }
        var combined = MediaIndexAttemptDiagnosticView.From(Attempt(InventoryScanStatus.Partial, codes), current);
        Assert.AreEqual(codes.Length, combined.Codes.Count);
    }

    [TestMethod]
    public void R4_UnknownAndObservationCodesDoNotInventGlobalCauses()
    {
        foreach (var codes in new[] { Array.Empty<string>(), new[] { "PRIVATE_PATH" },
            new[] { "MEDIA_INVALID_SIGNATURE", "MEDIA_FILE_LIMIT" } })
        {
            var view = MediaIndexAttemptDiagnosticView.From(Attempt(InventoryScanStatus.Partial, codes), null);
            CollectionAssert.AreEqual(new[] { "MEDIA_INDEX_UNKNOWN_REASON" }, view.Codes.ToArray());
            StringAssert.Contains(view.Text, "cause non renseignée");
            Assert.IsFalse(view.Text.Contains("PRIVATE", StringComparison.Ordinal));
            Assert.IsFalse(view.HasPreviousComplete);
        }
        var complete = MediaIndexAttemptDiagnosticView.From(
            Attempt(InventoryScanStatus.Complete, "MEDIA_INVALID_SIGNATURE"), null);
        Assert.AreEqual(InventoryScanStatus.Complete, complete.AttemptStatus);
        StringAssert.Contains(complete.Text, "Analyse complète");
    }
}
