using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

/// <summary>Only presentation labels change; backend reason strings remain exact.</summary>
[TestClass]
public sealed class LegacyMediaDiagnosticPresentationTests
{
    [DataTestMethod]
    [DataRow("MEDIA_NOT_AVAILABLE", "Média indisponible")]
    [DataRow("MEDIA_UNSUPPORTED_OR_UNVERIFIED", "Image non vérifiable")]
    [DataRow("MEDIA_TOO_LARGE_COMPRESSED", "Image trop volumineuse")]
    [DataRow("MEDIA_STALE_GENERATION", "Aperçu obsolète")]
    [DataRow("MEDIA_CANCELLED", "Aperçu annulé")]
    [DataRow("MEDIA_BUDGET_EXHAUSTED_OR_STALE", "Aperçu limité")]
    [DataRow("UNRECOGNIZED_DIAGNOSTIC", "Aperçu indisponible")]
    public void UI05_MediaDiagnosticUsesShortLabelAndDoesNotDisplayRawCode(
        string diagnostic, string expected)
    {
        Assert.AreEqual(expected, LegacyMediaDiagnosticPresentation.ShortLabel(diagnostic));
        Assert.IsFalse(expected.Contains("MEDIA_", StringComparison.Ordinal));
    }

    [TestMethod]
    public void UI05_NullDiagnosticHasNeutralText()
    {
        Assert.AreEqual("Aperçu indisponible",
            LegacyMediaDiagnosticPresentation.ShortLabel(null));
    }
}
