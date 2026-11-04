namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Short user-facing text for bounded ES-DE media load diagnostics.
/// Exact backend diagnostic codes are retained separately in the tooltip.</summary>
internal static class LegacyMediaDiagnosticPresentation
{
    /// <summary>Never interpret a refused or unverifiable image as a deleted ROM.</summary>
    internal static string ShortLabel(string? code) => code switch
    {
        "MEDIA_NOT_AVAILABLE" => "Média indisponible",
        "MEDIA_UNSUPPORTED_OR_UNVERIFIED" => "Image non vérifiable",
        "MEDIA_TOO_LARGE_COMPRESSED" => "Image trop volumineuse",
        "MEDIA_STALE_GENERATION" => "Aperçu obsolète",
        "MEDIA_CANCELLED" or "MEDIA_GENERATION_CANCELLED" => "Aperçu annulé",
        "MEDIA_BUDGET_EXHAUSTED_OR_STALE" => "Aperçu limité",
        "MEDIA_LOADING" => "Chargement en cours",
        _ => "Aperçu indisponible"
    };
}
