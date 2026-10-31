# NEXT-05 / MI-B — Backend de projection MEDIAS sur fixtures Windows

Contrat : Produit #135, MI_B_DTO_APPROVABLE DEV 2. Départ main 315ec3a9 (MI-A #127 fusionnée et CI verte). Implémentation interne non autorisée en production.

## Frontières

- MI-A reste le seul lecteur : MiBFixtureMediaApplicationService délègue exclusivement à MiAFixtureMediaSession. Racines synthétiques Windows sous TEMP/rm-mia-fixture-{GUID}/MEDIAS uniquement. Grants séparés RomsInventory / MediaIndex / MediaPreview.
- DTO internes immuables : MiBMediaRow, MiBRunView, MiBProjectionState. Aucun chemin brut, FileID, StorageRootId, handle, grant, exception native, XML, UserDecision, ni image décodée. Libellés neutres et bornés, codes diagnostics en allowlist, collections défensivement copiées.
- Select : validation de syntaxe seulement, sans I/O de fichier. Consent : action explicite distincte, grant MEDIAS temporaire en mémoire avec vérification handle racine MI-A. Start : une seule consommation du grant et appel du scanner MI-A hors UI. Cancel : token et révocation, sans promesse de préemption d'I/O noyau Windows.
- Isolation : version de sélection + nonce de scope privé. Un résultat A arrivé tardivement ne peut publier sous B. Select B annule l'ancien scope et vide les vues A. Dans la même sélection, un Current complet survit à un LastAttempt Partial, Failed ou Cancelled.
- Couverture : ObservedMediaCount = nombre d'observations image, non tous les fichiers. KnownObservedMediaTotal est null si non Complete, non Online, couverture incomplète ou TotalFileCount source absent. Progression indéterminée : aucun pourcentage/byte inventé. PNG/JPEG contrôlés par en-tête, WebP reconnu non décodé.
- Historique : MiBMediaProjection.HistoricalFixture ne lit que des snapshots mémoire complets explicitement fournis ; disponible Offline/Stale sans disque, droit, revalidation, nouveau stockage SQLite MEDIAS ou scan automatique.

## Tests d'acceptation

| Cas | Attendu |
| --- | --- |
| MI-B01 | Choix != consentement != Start, one-shot, version erronée, refus de l'API publique |
| MI-B02 | Cancel conserve CurrentComplete de la même racine, résultat tardif A rejeté après Select B |
| MI-B03 | PNG/JPEG/WebP en-têtes, budgets stricts, Partial/Unknown, diagnostics filtrés, collections immuables |
| MI-B04 | Archive fixture Offline/Stale consultable sans réactiver un grant ou exécuter un scan |

## Preuves et limitations

Windows natif réel MI-B01–04, Adaptive complète, politique/docs, ci-policy-gate, ZIP win-x64 extrait et SHA-256 vérifié en quarantaine ; provenance du SHA probatoire distincte du HEAD source si workflow temporaire. Aucun workflow permanent.

MI-A07 (ACL/non-admin/FS alternatifs) et MI-A09 (USB/SSD physique) restent NOT_EXECUTED. Aucun accès personnel, Skia, aperçu, permission publique, fusion autonome ni Release. HOLD actifs : REAL_LIBRARY_SCAN_HOLD, REAL_MEDIA_SCAN_HOLD, REAL_MEDIA_PREVIEW_HOLD, REAL_CATALOG_STORAGE_HOLD, NEXT05_RELEASE_HOLD.
