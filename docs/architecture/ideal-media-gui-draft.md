# NEXT-05 — GUI MEDIAS/PREVIEW #163 (candidat fonctionnel, non fusionné)

## État de la livraison

- PR [#159](https://github.com/TobbyCy/RomothequeManager/pull/159) GUI IDEAL standard **fusionnée**, ROMS et SQLite read-only disponibles.
- PR [#161](https://github.com/TobbyCy/RomothequeManager/pull/161) backend INDEX/PREVIEW natif **fusionnée** après avis sécurité indépendant, avec grants distincts.
- PR [#163](https://github.com/TobbyCy/RomothequeManager/pull/163) **OPEN/DRAFT** sur la branche existante `dev3/next05-ideal-media-gui-draft`. Aucun merge ou Release autorisé ici.
- PR [#165](https://github.com/TobbyCy/RomothequeManager/pull/165) : variantes PS1/PS2/PS3, intégration à surveiller au nouveau main ; aucun fichier PS3 interne ne doit être promu en jeu.

Ce document décrit le **code candidat**, pas une fonctionnalité déjà publiée. Les tests et la preuve GUI Windows au **HEAD final exact** doivent être `SUCCESS`, puis DEV 2 doit rendre sa revue ciblée et le propriétaire donner un GO de fusion distinct.

## Parcours utilisateur candidat de l'EXE standard

1. La collection ROMS s'affiche sans import XML ES-DE. L'utilisateur choisit sa racine et autorise explicitement ROMS avant l'inventaire. SQLite peut restaurer des faits **OFFLINE/STALE** sans restaurer de permission.
2. Après inventaire ROMS `Complete/Online/Fresh`, le bouton « Découvrir et afficher les jaquettes locales » ouvre une fenêtre MEDIA distincte. Il ne lance ni INDEX ni PREVIEW.
3. L'utilisateur choisit une racine finissant par **MEDIAS** et clique séparément **Autoriser INDEX**, puis **Indexer**. `IdealMediaApplicationService` valide les droits via handles natifs et une autorisation volatile one-shot ; les dossiers sont classés selon `IdealPlatformFolderConventions.PlatformAliases/FolderKinds`. Un nom de dossier n'est jamais un grant.
4. L'utilisateur **confirme explicitement** la correspondance des deux racines sélectionnées et lance la comparaison. `VerifiedScopeMapping.FromExplicitSelections`, `MediaPresentationService.Build` et `IdealMediaCoverProposals.Build` forment une projection **pure, sans I/O** et liée aux `ScanId/RunId/MediaRootRef`.
5. Les fichiers MEDIAS indexés sous `covers` proposent `CANDIDATE` si plateforme + nom de fichier ROM sans extension correspondent de manière unique, **même sans XML ou sans balise image**. `REFERENCED` exige une référence XML de chemin média exacte préalablement importée ET une attestation positive distincte que l'import appartient à la racine ROMS. `AMBIGUOUS` conserve toutes les alternatives et demande un choix d'observation explicite ; `NOT_COMPARABLE` signale une génération ou racine inexploitable ; absence locale uniquement dans un scope complet et comparable. Jamais d'association automatique par titre.
6. Choisir une observation candidate pour **aperçu** ne valide aucune association persistée. L'utilisateur clique **Autoriser PREVIEW** spécifiquement pour l'image choisie, puis **Afficher**. Le backend revalide `Current` et fournit un `MediaPreviewResult` borné. `IdealMediaBitmapAdapter` copie les octets `DecodedMediaPreview.Pixels` en mémoire dans un `WriteableBitmap` Avalonia `Rgba8888/Premul`, effectivement attaché à `Image.Source`. Ni la fenêtre ni le ViewModel n'ouvrent de fichier image par chemin.
7. Cancel, changement de ROMS `ScanId`, sélection MEDIAS A→B, OFFLINE/STALE, fermeture et Dispose détruisent les droits volatiles, l'état des propositions et les buffers d'aperçu. Aucune autorisation n'est persistée par SQLite.

## Sécurité et statut d'activation

La fenêtre MEDIA possède un verrou de **composition produit** `productMediaUiEnabled`, **désactivé par défaut** pour la construction isolée ; le parcours de l'EXE standard fournit `true` seulement depuis `ObservedLibraryWindow` après ROMS `Complete/Online/Fresh`. Ce booléen ne remplace **aucun** grant natif. INDEX et PREVIEW doivent ensuite être autorisés séparément par le backend #161, avec refus hors scope et consommation one-shot.

Il n'y a pas de mode d'activation par CLI, XML, SQLite ou simple consentement ROMS. La composition des tests `NEXT05_CI` est exclue de l'EXE standard. La PR #163 reste Draft jusqu'à revue DEV 2, verdict CI et GO propriétaire ; aucune bibliothèque personnelle n'est scannée.

## Oracles Windows et code testé

- `MEDIA_GUI_06–10` : projection CANDIDATE sans XML, doublons AMBIGUOUS, plateformes différentes, identité par fichier et non titre, OFFLINE/STALE/Partial.
- `MEDIA_GUI_11` : vraie racine Windows temporaire avec `ROMS/gba/Fiction.gba`, `MEDIAS/gba/covers/Fiction.png`, **aucun XML** ; INDEX distinct, candidat, PREVIEW distinct, pixels décodés non vides, Cancel efface l'image.
- `MEDIA_GUI_12` : deux jaquettes homonymes sous `covers/` et `covers/alternate/` ; alternatives conservées, jamais associées automatiquement.
- `MEDIA_GUI_13` : constructeur défaut sans autorisation ou INDEX implicite.
- `MEDIA_GUI_14` : JPEG natif fictif, PREVIEW séparé, pixels effectivement décodés.
- Harnais **CI seulement** dans `Next05GuiHarness` : capture vraie fenêtre MEDIA après parcours natif et compare un pixel capturé à `CadetBlue` à **1120×720 et 760×480**. Workflow déjà suivi `.github/workflows/next05-media-ui-proof.yml`, branche unique #163, acteur `TobbyCy`, token en lecture seule, artefacts synthétiques trois jours, aucune Release. La règle `tests/ci_policy.py` est mise à jour **uniquement pour ce renommage de branche unique et le marqueur standard sans harness**, sans contourner ni supprimer les canaris.
- La preuve requiert les liens vers **les runs complets au HEAD final**, pas des captures précédentes. Aucun `WINDOWS_GUI_PROOF PASS` anticipé.

## Limites explicites

Le rendu d'une jaquette n'est **pas** l'identification commerciale ou l'unicité d'un jeu. Variantes, playlists BIN/CUE/CHD/M3U, PS3 et dossiers internes doivent rester des observations distinctes. Les ambiguïtés demandent une action utilisateur ; aucun `GameId` durable, `UserDecisionId` fictif ou association persistée n'est créé par cette PR.

`PR163_GUI_END_TO_END_CANDIDATE / REQUIRED_CI_AND_WINDOWS_GUI_PROOF_PENDING / DEV2_REVIEW_PENDING / NO_MERGE_NO_RELEASE`
