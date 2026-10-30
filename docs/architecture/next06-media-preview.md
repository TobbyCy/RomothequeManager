# NEXT-06 — backend média → présentation/aperçu (Produit #95)

**Base :** `main=62741d052ece87ceddcc52142e9599190954a3ae` (NEXT-05/PR #106 et #107 intégrées). **Périmètre :** contrats et canaris backend, fixtures uniquement. Revue DEV 2 et GO Contrôleur nécessaires avant fusion.

## Architecture

- `MediaPresentationService.Build` transforme sans I/O les résultats de `MediaLinkProjection.Reconcile` en états `FOUND_UNLINKED` (galerie, hors lien ROM), `CANDIDATE`, `REFERENCED` (claim sourcé, non approuvé), `AMBIGUOUS`, `ABSENT_IN_COMPLETE_SCOPE` et `NOT_COMPARABLE` ; candidates, preuves, conflits et versions sont read-only. Pas de GameId ni de décision utilisateur inférée.
- `MediaPreviewService.LoadAsync` accepte seulement le triplet `MediaRunId/MediaRootRef/MediaObservationId` et la taille cible. Le constructeur public refuse **sans même tester le filesystem** (`REAL_MEDIA_PREVIEW_HOLD`). Seul l'assembly MSTest via constructeur `internal` peut ouvrir un root `rm-next06-fixture-*/MEDIAS` sous le répertoire temporaire exact.
- L'accès fichier, la vérification de la racine, la validation signature/format, le décodeur SkiaSharp et la gestion du tampon sont dans le service. View et ViewModel inchangés, aucun `Bitmap(path)`. `DecodedMediaPreview` fournit un tampon RGBA8888 dont le destinataire est propriétaire et doit appeler `Dispose()` ; aucun cache ni bitmap Skia transmis à Avalonia.
- Les observations et liens restent éphémères, distincts de `StorageRootId/MediaAssetId/GameId/UserDecisionId` (SQLite #51) non implémentés. XML ES-DE non obligatoire.

## Budget décodage et limites de preuve

| Aspect | Plafond backend test-only |
| --- | --- |
| Entrée compressée lue | **8 Mio**, volontairement plus strict que le plafond MEDIA 64 Mio |
| Source avant décodeur | **2048 px/axe**, **3,5 Mpx** (inférieur à 4 Mpx) |
| RGBA source + aperçu | **16 Mio cumulés** (calculs `checked`) |
| Cible d'aperçu | **512 px/axe** maximum, conserve l'aspect |
| Copies explicites (entrée+SKData+source+aperçu) | **≤32 Mio**, maximum calculé avant allocation |
| Décodage simultané | **1** via `SemaphoreSlim` ; cache **0** |
| Temps | timeout **10 s**, annulation des lectures asynchrones ; résultat natif ignoré après annulation |

Le projet fournit déjà **SkiaSharp 3.119.4 transitivement avec Avalonia 12.1.4** : aucun package ajouté. `SKCodec.GetPixels(SKImageInfo, byte[])` écrit dans un tampon **déjà alloué** et plafonné ; le service rejette les tailles natives annoncées incohérentes. Le repli vers un bitmap source plus grand est interdit. L'aperçu peut être refusé si les contraintes ne sont pas prouvées. La mémoire **native interne** de Skia n'est **pas** bornée par cette API : les tests rapportent une **mesure effective de variation PrivateMemorySize64** mais ne peuvent garantir un plafond absolu pour une image hostile. Pas de permission pour les médias personnels avant expertise native/TOCTOU distincte et GO explicite. Même prudence pour un codec natif non annulable en cours de `GetPixels` : le résultat est écarté après retour si annulation/obsolescence.

## Canaris backend synthétiques

- `MU-01` : PNG et JPEG **réellement encodés puis décodés** sous MEDIAS fictif, aucun XML.
- `MU-02/03` : galerie non liée, candidat, référence sourcée, ambiguïtés sans choix automatique.
- `MU-04` : absence locale uniquement sur sources complètes, sinon non comparable.
- `MU-05` : PNG incomplet, dimensions abusives, WebP non supporté et cible >512.
- `MU-06` : annulation durant I/O, changement de génération pendant décodage et destruction du tampon.
- `MU-07` : constructeur public fail-closed, paths forgés, run/observation erronés, symlink si autorisé par Windows, sources inchangées.
- `MU-08` : 3 aperçus simultanément demandés, décodés en série, buffers calculés, mesure temps/mémoire, source intacte.

**HOLD explicites :** tests Windows Avalonia réels et captures (DEV 3 après GO), ZIP post-UI sans harnais, mémoire native codec face à entrées adversariales extrêmes, SSD/TOCTOU physique, ROMothèque personnelle, SQLite, DocFX, scraping, fusion et Release. Les tests ci-dessus sont des **cas à exécuter**, pas des PASS revendiqués avant CI HEAD exact.
