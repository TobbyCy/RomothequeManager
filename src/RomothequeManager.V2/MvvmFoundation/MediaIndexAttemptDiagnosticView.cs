using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Pure R4 presentation of a native attempt using only known global codes;
/// diagnostic reasons, scope references and unknown code text are never exposed.</summary>
internal sealed record MediaIndexAttemptDiagnosticView(
    InventoryScanStatus AttemptStatus, InventoryAvailability Availability,
    InventoryFreshness Freshness, IReadOnlyList<string> Codes,
    string Explanation, bool HasPreviousComplete)
{
    private static readonly IReadOnlyDictionary<string, string> Reasons =
        new Dictionary<string, string>(StringComparer.Ordinal) {
            ["MEDIA_INDEX_EXPLICIT_GRANT_REQUIRED"] = "Autorisation INDEX absente ou état incompatible ; origine exacte non déterminée. Sélectionner MEDIAS si nécessaire, puis autoriser INDEX et relancer par clic.",
            ["MEDIA_INDEX_STALE"] = "Sélection périmée ou révoquée. Choisir à nouveau MEDIAS et autoriser INDEX.",
            ["MEDIA_INDEX_CANCELLED"] = "Analyse annulée. Autoriser une nouvelle tentative pour relancer volontairement.",
            ["MEDIA_INDEX_TIMEOUT"] = "Limite de durée atteinte. Vérifier le périmètre choisi avant une nouvelle tentative autorisée.",
            ["MEDIA_FILE_BUDGET"] = "Limite de fichiers parcourus atteinte. Vérifier le périmètre choisi avant une nouvelle tentative autorisée.",
            ["MEDIA_DIRECTORY_BUDGET"] = "Limite de dossiers parcourus atteinte. Vérifier le périmètre choisi avant une nouvelle tentative autorisée.",
            ["MEDIA_DEPTH_BUDGET"] = "Limite de profondeur atteinte. Vérifier le périmètre choisi avant une nouvelle tentative autorisée.",
            ["MEDIA_TOTAL_HEADER_BUDGET"] = "Limite de lecture cumulée des en-têtes atteinte. Vérifier le périmètre choisi avant une nouvelle tentative autorisée.",
            ["MEDIA_INDEX_NATIVE_DENIED_OR_OFFLINE"] = "Source inaccessible ou refusée ; origine exacte non déterminée. Vérifier le support et les droits hors du produit, puis autoriser une nouvelle tentative.",
            ["MEDIA_INDEX_NATIVE_FAILED"] = "Cause technique non déterminée. Contacter le support avec le code uniquement."
        };

    /// <summary>Project the returned attempt independently of retained complete evidence,
    /// preserving ambiguous or missing causes without inferring a budget or access failure.</summary>
    internal static MediaIndexAttemptDiagnosticView From(MediaIndexSnapshot attempt,
        MediaIndexSnapshot? current)
    {
        var codes = attempt.Diagnostics.Select(d => d.Code).Where(Reasons.ContainsKey)
            .Distinct(StringComparer.Ordinal).Take(Reasons.Count).ToArray();
        var complete = attempt.ScanStatus == InventoryScanStatus.Complete &&
            attempt.Availability == InventoryAvailability.Online;
        var explanation = complete ? "Analyse complète ; les images rejetées ne rendent pas l’index incomplet." :
            codes.Length == 0 ? "Analyse incomplète ; cause non renseignée. Vérifier la source et autoriser une nouvelle tentative si souhaité." :
            string.Join("\n", codes.Select(code => Reasons[code]));
        if (!complete) explanation += " Les médias manquants ne sont pas confirmés.";
        return new(attempt.ScanStatus, attempt.Availability, attempt.Freshness,
            Array.AsReadOnly(codes.Length == 0 && !complete
                ? new[] { "MEDIA_INDEX_UNKNOWN_REASON" } : codes), explanation,
            current is not null);
    }

    /// <summary>Static explanation and next action alongside status and whitelisted codes.</summary>
    internal string Text => "Dernière tentative INDEX MEDIAS : " + AttemptStatus +
        " / " + Availability + " / " + Freshness + "\n" +
        string.Join(" · ", Codes) + "\n" + Explanation;
}
