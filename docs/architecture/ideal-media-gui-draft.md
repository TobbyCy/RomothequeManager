# NEXT-05 — GUI MEDIAS/PREVIEW candidate (DEV 3, Draft)

**Source:** #161 HEAD `9ee05924a655404645fd53d4963be37aa0e42256`.
**Isolation:** branche GUI dédiée `dev3/next05-ideal-media-gui-draft`, empilée sur #161.
**Ne dépend PAS de #159 et ne l'altère PAS.** Aucun merge, Release, données personnelles ou activation de l'EXE standard.

## Ce qui est codé (candidat non activé)

- `IdealMediaDraftViewModel` : contrat typé `IdealMediaApplicationService` pour une sélection MEDIAS indépendante, clic positif INDEX puis Start, clic positif PREVIEW par observation puis Load, refus, Cancel/Dispose, états et délai de résultat, destruction des pixels RGBA.
- `IdealMediaDraftWindow` : fenêtre Avalonia code-only **non reliée à Program/MainWindow** ; picker local dédié MEDIAS et libellés INDEX / PREVIEW distincts. Pas de décodage, scan, XML ou I/O source dans View ou ViewModel.
- **Gate compilé fail-closed** `NativeMediaSecurityValidated = false` : aucune des commandes de sélection/INDEX/PREVIEW ne peut être activée ; toute méthode interne appelée directement refuse également.
- `EsDeDeclaredMediaClaims.Build` : projection pure sans I/O, seulement depuis `EsDeCatalogueSnapshot.Games[].Media` déjà importés. Correspondance exacte **plateforme + chemin ROMS déclaré** avec une seule observation ; référence MEDIAS bornée et déjà déclarée en XML. Ne fait ni recherche sur titre, ni scan, ni appariement réseau ; source absente ou doublon → pas de lien automatique.
- `MediaPresentationService.Build` conserve `REFERENCED / CANDIDATE / AMBIGUOUS / ABSENT_IN_COMPLETE_SCOPE / NOT_COMPARABLE` comme indices, sans approuver une jaquette ni restaurer une autorisation. La GUI candidate ne sélectionne que `REFERENCED` pour un éventuel aperçu et ne retient aucun grant dans SQLite.
- Tests synthétiques `MEDIA_UI_01–04` : aucune commande active, aucun Current, chemins/traversal rejetés, pas de déduction par titre, doublons/plateformes divergentes refusés, annulation/dispose.

## Verrous pour un prochain lot autorisé

1. **DEV 2** doit conclure la revue de sécurité #161 au HEAD exact, incluant INDEX/PREVIEW séparés, handle native, TOCTOU, budgets et annulation.
2. **Propriétaire/Contrôleur** doit autoriser explicitement l'activation du parcours GUI. Lever le gate uniquement avec tests d'activation positifs et négatifs et nouvelle CI requise. Ne jamais déduire le droit PREVIEW d'INDEX, de ROMS, du picker, de l'XML ou de SQLite.
3. Après stabilisation de #159, intégrer seulement dans un lot GUI distinct la navigation et les snapshots ROMS observés, les preuves des trois racines choisies séparément et le mapping `VerifiedScopeMapping.FromExplicitSelections`. Un XML ES-DE historisé n'est pas une permission d'accès MEDIAS.
4. Faire le rendu RGBA via un adaptateur UI à partir du buffer déjà décodé **après** grant PREVIEW, sans ouvrir les chemins depuis la View. Couvrir l'annulation tardive, A→B, formats corrompus, absent, hors-scope, OFFLINE/STALE, la disparition de l'image et les tailles 1120×720 / 760×480.
5. Recette native de l'EXE standard à partir du HEAD assemblé et de fixtures fictives, puis nouvelle contre-revue DEV 2. Une CI verte de #161 ou de cette PR n'est pas une qualification de l'EXE #159.

## Oracles attendus

- Avant validation #161 : `CanChooseMedia = CanAuthorizeIndex = CanStartIndex = CanAuthorizePreview = CanLoadPreview = false`, aucun handle ouvert, aucun preview.
- Après GO explicite futur : picker MEDIAS ≠ grant ; INDEX ≠ PREVIEW ; un grant PREVIEW = une observation du Current complet ; reselect/Cancel/Dispose invalident les opérations ; résultat tardif rejeté ; image disposée.
- Jaquette ES-DE : `REFERENCED` est seulement une provenance de chemin ; lien ambigu, manquant ou incomparable conserve un placeholder. Aucun `APPROVED_COVER` automatique.

## Vérification indépendante et validation GUI Draft

DEV 2 [#161](https://github.com/TobbyCy/RomothequeManager/pull/161#issuecomment-6634854191) :
`APPROVABLE_FOR_GUI_INTEGRATION` au SHA `9ee05924a655404645fd53d4963be37aa0e42256`,
**limité au développement Draft sur fixtures**. Le verdict n'autorise aucune
fusion, activation des commandes, test sur données personnelles ni Release.
La fenêtre MEDIA reste non composée et son verrou compilé vaut `false`.

L'annulation doit laisser les contrôles désactivés tant que les opérations
asynchrones n'ont pas réellement terminé ; un `Cancel` efface toute preuve
native `Current` pour une nouvelle PREVIEW et les buffers RGBA sont disposés.
La liste des références est stable pendant les notifications afin d'éviter
un cycle de changement de sélection.

Tests `MEDIA_UI_01–05` : cas négatifs, provenance de chemin explicite,
absence de grants et refus des chemins absolus, traversants ou ambigus.
CI GUI au HEAD exact à contrôler ; les PASS du backend #161 ne se transfèrent
pas automatiquement à ce Draft.

**Statut :** `IDEAL_MEDIA_GUI_DRAFT / NOT_ENABLED / NO_MERGE_NO_RELEASE`.
