# Journal des changements

## NEXT-05 / DEV 1 — G1 PERM-F01–04 : adversarial tests sur fixtures (31.10.2026)

- PR #143 : quatre tests Windows sur les véritables scanners RS-A et MI-A, adaptateur MI-B, portes publiques PREVIEW/ROMS/MEDIAS, UI et SQLite ; aucun mock de permissions ni modification de code produit. Confinement strict dans des fixtures sous %TEMP%, avec témoins positifs d'en-têtes/fichiers générés et contrôles négatifs des mauvais grants, expirations, révocations, changement de sélection, restauration et CLI.
- **Preuves à confirmer en CI** : aucun PASS anticipé, aucune permission réelle ni SSD, aucun changement de politique. Les contrôles Windows non-admin/ACL, exFAT et retrait USB restent NOT_EXECUTED ; revue indépendante DEV 2 nécessaire au HEAD exact.


## NEXT-05 / DEV 3 — #139 : preuve clavier Tab / Shift+Tab native Windows (31.10.2026)

- **Dernier gate DEV 2** sur les deux correctifs P1 UI MI-B : `KEYBOARD_EVIDENCE_NOT_EXECUTED` au HEAD antérieur `5ccfcff8`, sans P0/P1 fonctionnel nouveau ; GO Contrôleur ciblé pour preuve *réellement exécutée* dans la **même PR #139**. Les deux P1 d'origine (six commandes invisibles et annulation prise pour OFFLINE/STALE) restent inchangés et conservés.
- **Méthode exacte (harnais `NEXT05_CI` uniquement)** : sur runner Windows Server 2022, fenêtre Avalonia native ouverte et activée ; `TryGetPlatformHandle`/HWND et `GetForegroundWindow` contrôlés, touches `VK_TAB` et `VK_SHIFT` injectées par **Win32 `SendInput` avec vrais événements key-down/key-up dans la file d'entrée Windows**. `InputElement.KeyDownEvent` de la fenêtre observé via handler tunnel/bubble, y compris marqueur `KeyModifiers.Shift`. Après chaque événement, contrôle effectif `IsFocused` sur la commande attendue, unicité du focus, `IsEffectivelyEnabled`, saut des contrôles désactivés et écran capturé avec bordure jaune contrôlée au pixel. La **mise au point initiale** sur le premier bouton d'un scénario utilise `Focus(NavigationMethod.Pointer)` comme *ancre non comptabilisée* ; **les 28 transitions comptées sont uniquement causées par `SendInput`**, jamais par un simple appel `Focus(Tab)`. Il s'agit d'une injection **logicielle** de touches OS, **pas** d'un clavier USB physique ni d'un test humain.
- **Transitions réelles dans les deux résolutions 1120×720 ET 760×480** :
  - `SelectedA` sans consentement : `Select ↔ Consent`, `Start.CanExecute=false`, aucune tentative de scan, pas de grant implicite.
  - `ConsentA` : `Select ↔ Start`, saut du consentement devenu disabled.
  - `CompleteA` : `Select → Consent → CurrentComplete → LastAttempt`, puis sens inverse ; saut de Start/Cancel désactivés.
  - `RunningCancelKeyboardFixture` : `Cancel → CurrentComplete → LastAttempt`, puis sens inverse ; état de navigation **DTO synthétique** pour stabiliser Cancel, **sans scan concurrent ni accès réel**.
- [**Preuve Windows native #41223433101**](https://github.com/TobbyCy/RomothequeManager/actions/runs/41223433101) **COMPLETED/SUCCESS** sur SHA probatoire **`306d315fbece43c746f8c672101a35b206be7216`** : **266/266 tests .NET PASS**, **60 PNG** natifs (les 32 états déjà couverts + 28 transitions clavier), 60 CSV de géométrie/pixels des six commandes, et `MIB-keyboard-trace.csv` **28 transitions contrôlées**. Logs `UI_MIB_NATIVE_KEYBOARD_STEP_PASS`, 8 `UI_MIB_NATIVE_KEYBOARD_PASS`, `UI_MIB_NATIVE_KEYBOARD_TRACE_PASS ... steps=28 ... forward_reverse=true disabled_skip=true focus=true`, `UI_MIB_P1_VISUAL_PIXEL_ORACLE_PASS ... captures=60 ... disabled=true focus_ring=true`. Captures archive OFFLINE/STALE toujours injection visuelle synthétique, non preuve SQLite MEDIAS.
- [**Artefact Windows #13051440282**](https://github.com/TobbyCy/RomothequeManager/actions/runs/41223433101/artifacts/13051440282), expiration **2026-11-03 17:16:21 UTC**. ZIP Windows normal sans seam `NEXT05_CI`, **22 fichiers**, extraction + hashes internes vérifiés ; SHA-256 **`f0b60e441a7ad71d3d97994494082bc665b866e5a6cb43e03cf5b9a3944ceb95`**, **candidat quarantainé, aucune Release**.
- Workflow de preuve CI de branche **supprimé** du diff fusionnable après succès ; code/source/harnais/tests à comparer blob-par-blob avec le HEAD définitif. **CI Adaptive du HEAD final et re-revue indépendante DEV 2 du delta clavier requises**, ne pas annoncer APPROVABLE avant vérification. Aucune modification du scanner MI-A, backend MI-B, permission, Skia réel, ROMothèque/SSD personnels, fusion autonome ni Release. HOLD `REAL_*_HOLD` et `NEXT05_RELEASE_HOLD` maintenus.


## NEXT-05 / DEV 3 — correctifs P1 UI MI-B post-fusion #137 (31.10.2026)

- **P1-UI-MIB-01** : six commandes Avalonia `Select / Consent / Start / Cancel / CurrentComplete / LastAttempt` corrigées par styles explicites isolés (Button / template ContentPresenter, normal, disabled, survol, pressé et focus) et panneaux visibles. Le harnais CI capture désormais leur géométrie **et les pixels réellement dessinés** avec métadonnées de contrôle pour chaque screenshot ; l'oracle Windows rejette PNG noirs, texte manquant, état désactivé sans preuve et absence d'anneau de focus. Focus programmatique `NavigationMethod.Tab` vérifié ; séquence physique de touches Tab non revendiquée.
- **P1-UI-MIB-02** : `CurrentIsHistorical` n'est plus assimilé automatiquement à `OFFLINE/STALE`. Après `MI_A_CANCELLED` probant sur la même racine, le bandeau principal affiche « Annulée · dernier complet conservé » ; `CurrentComplete` reste distinct de `LastAttempt`. La véritable archive `IsHistoricalFixture` + Offline/Stale reste en lecture seule. Tests UI-MI-B03 renforcés (annulation, `CurrentIsHistorical=true`, Partial distinct), GUI refuse tout bandeau erroné.
- **Preuve native Windows** [#41214695627](https://github.com/TobbyCy/RomothequeManager/actions/runs/41214695627), SHA probatoire `30dc66eee8b35c1d5fbe94d4acfaa626eaa9d76e` : **266/266 tests .NET PASS**, **32 captures Windows** 1120×720 et 760×480, `UI_MIB_P1_VISUAL_PIXEL_ORACLE_PASS` sur les six commandes/PNG (`text_pixels=true disabled=true focus_ring=true`) et `UI_MIB_WINDOWS_GUI_PASS`. Archive Offline/Stale = injection **visuelle synthétique**, non preuve SQLite MEDIAS. [Artefact éphémère #13047553896](https://github.com/TobbyCy/RomothequeManager/actions/runs/41214695627/artifacts/13047553896) disponible jusqu'au 03.11.2026 15:27:26 UTC. ZIP candidat Windows **22 fichiers**, extraction + hashes par fichier vérifiés ; SHA-256 `2f51f1b6d88aeb18814e68efb324fdc63e5b2f9804439084510a2b97edc99b80`, **quarantaine**, pas Release. Normal EXE sans seam `NEXT05_CI`.
- Workflow de preuve temporaire supprimé avant le HEAD PR final, sans retoucher le code/XAML/tests de la recette ; CI Adaptive HEAD final + revue **indépendante DEV 2** des sources et pixels encore requises avant tout verdict. Aucun changement scanner MI-A, backend MI-B, permissions, Skia, API de production, support personnel ou Release ; tous `REAL_*_HOLD` maintenus.


## NEXT-05 / UI MI-B #137 — Preuve native Windows (31.10.2026)

- ViewModel MEDIAS et fenêtre Avalonia internes reliés au service MI-B #136 ; sélection/consentement/Start/Cancel séparés, Current/LastAttempt, isolation A/B, Offline/Stale et diagnostics neutres ; « Annulée » seulement avec `MI_A_CANCELLED`, Partial/Failed sans faux succès. Aucun Skia, aucun scan personnel, aucun grant public.
- [Windows #41209890799](https://github.com/TobbyCy/RomothequeManager/actions/runs/41209890799) : 265 tests PASS, **32 captures PNG** 1120×720 / 760×480 (dont archive Offline/Stale uniquement **visuelle synthétique**), ZIP candidat de 22 fichiers extrait et SHA-256 `5c28704fd694567114e4b701967ac91c79089a0ad85947f0e8aa3f80b0453fc5`. Source de preuve `041110da`, workflow éphémère supprimé après SUCCESS. Revue humaine DEV 2 et CI finale sur HEAD sans workflow en attente ; aucune Release.


## NEXT-05 / UI MI-B — DEV 3 / PR #137 (WIP fixtures, 31.10.2026)

- Raccordement MVVM au service interne `IMiBFixtureMediaApplicationService` validé par DEV 2 dans #135 et fusionné via #136 sur `main=e4796b58`. Vue Avalonia MEDIAS séparée du scanner MI-A, de ROMS, de PREVIEW et du catalogue.
- Sélection MEDIAS explicite, consentement volatil distinct, Start one-shot, Cancel, progression indéterminée, CurrentComplete et LastAttempt distincts, filtrage des notifications tardives par version et génération, labels sûrs et Offline/Stale sans regrant. En-têtes uniquement, sans Skia.
- Tests UI-MI-B01..05 ajoutés ; **CI et captures natives Windows non encore prouvées**, fenêtre CI-only et 24 captures restent à intégrer et à vérifier avant APPROVABLE. PR Draft, aucune fusion autonome, permission publique, accès personnel ni Release.


## NEXT-05 / MI-B — DTO et adaptateur backend MEDIAS (PR DEV 1, fixtures uniquement)

- DTO **internes** `MiBMediaRow`, `MiBRunView`, `MiBProjectionState` : en-têtes PNG/JPEG/WebP non décodés, observations en lecture seule, comptes de médias observés distincts des totaux connus, champs inconnus/Partial explicites, codes diagnostics allowlist et labels sans chemin source.
- Service `IMiBFixtureMediaApplicationService` : sélection MEDIAS fictive sans I/O de fichier, consentement distinct / volatile et à usage unique via `MiAReadGrant(MediaIndex)`, Start hors thread UI, annulation/Dispose, notifications versionnées et prévention de publications tardives A/B ; réutilisation **exclusive** du scanner MI-A déjà fusionné #127.
- `CurrentComplete` distinct de `LastAttempt` sur une même racine ; changement de sélection purge A, Offline/Stale historiques en projection **mémoire fixture uniquement**, sans SQLite MEDIAS, aucun auto-scan/regrant.
- Tests MI-B01–04 ajoutés (fixtures Windows synthétiques, budgets, diagnostics, refus public, mutation de collections). **Preuves Windows, CI et ZIP du HEAD à constater avant revue DEV 2**, aucun Skia/permission réelle/fusion autonome/Release.


## NEXT-05 / MI-A #127 — correctif P1-MI-A-ROOT-01 après fusion #116

- Synchronisation non destructive depuis `main=ebc5ebe2` (UI-RSA #116 fusionnée) ; permissions RS-A et vue UI préservées.
- Isolation des observations et des références de portée entre sélections MEDIAS A/B ; `Current` et `LastAttempt` d'une ancienne sélection ne peuvent être réattribués à une nouvelle. Une tentative incomplète sur une **même** sélection conserve son dernier `Current` complet. Publications asynchrones de l'ancien scope rejetées.
- Canaris MI-A10 et MI-A11 ajoutés : A→B, annulation/refus, conservation sur B et changement de sélection pendant un scan. **Preuves du nouveau HEAD à valider**, aucun accès personnel, grant public ou Release.


## NEXT-05 / MI-A — Scanner MEDIAS Windows par handles (PR #127, DRAFT / FIXTURES ONLY)

- Nouvelle session de tests `MediaIndex` interne, grant MEDIAS indépendant de ROMS/PREVIEW, sélection explicite, identité native du handle, version/révocation et refus de tout grant en production.
- Ouverture des répertoires/fichiers par handles Windows relatifs et refus conservateur reparse/alias/ADS/hardlinks ; lecture d'en-têtes PNG/JPEG/WebP seulement, plafonds MI-A fixes et conservation du dernier Current complet sur tentative dégradée.
- Tests Windows MI-A01…06/08 ajoutés ; **CI, canaris natifs et ZIP en attente de vérification**. MI-A07 (ACL/non-admin/FS) et MI-A09 (USB physique) **NOT_EXECUTED**. Aucun code RS-A/UI-RSA #116 modifié, ni média personnel, Skia, fusion autonome ou Release.

## NEXT-05 / UI-RSA Avalonia — DEV 3 / PR #116 (31.10.2026, fixtures uniquement)

- Après fusion du scanner RS-A #114 dans `main=30958b11410a` et [CI push-main #41180639449](https://github.com/TobbyCy/RomothequeManager/actions/runs/41180639449) verte, ajout d'un panneau Avalonia dédié aux **seules fixtures ROMS** : sélection explicite, consentement temporaire non conservé, Start/Cancel, progression sans total inventé, refus, tentative Partial/Failed et état Offline/Stale. View, ViewModel et scanner interne restent séparés ; l'interface n'ouvre aucun fichier source elle-même.
- Le dernier `CurrentComplete` n'est pas remplacé par une tentative incomplète. Les générations RS-A, le catalogue SQLite historique et les associations manuelles de jaquettes demeurent distincts ; aucun accès MEDIAS, Skia ou permission utilisateur ajoutés.
- [Preuve Windows native #41181274995](https://github.com/TobbyCy/RomothequeManager/actions/runs/41181274995) sur source `65060daa` : **247 tests PASS**, cinq nouvelles captures RS-A à 1120×720 et 760×480, et 23 captures de régression Avalonia/Media/SQLite, EXE normal sans `NEXT05_CI`, ZIP de 23 fichiers contrôlé (SHA-256 `deda6422c46bbbbc14313dee62d7cbc976e8fdfa80300fdc5f42cea87b4cdeaa`). Workflow probatoire supprimé du HEAD final, code inchangé ; revue DEV 2 et CI du dernier HEAD à constater séparément.
- `REAL_LIBRARY_SCAN_HOLD`, `REAL_MEDIA_SCAN_HOLD`, `REAL_MEDIA_PREVIEW_HOLD`, `REAL_CATALOG_STORAGE_HOLD` restent actifs. Aucun SSD/ROMothèque personnelle, USB physique, non-admin/ACL, Release ou fusion autonome autorisé.

## NEXT-05 / RS-A — ROMS Windows handles confinés (PR #114, fixtures uniquement)

- Scanner de métadonnées ROMS interne aux fixtures Windows, sélection explicite, ReadGrant volatile/versionné, refus de tous les accès via l'API publique REAL_LIBRARY_SCAN_HOLD.
- Énumération par handles Windows (NtCreateFile relatif, FileIdBothDirectoryInfo), refus reparse et traversal, budgets, annulation, conservation Current Complete en cas d'échec.
- RS-A01 à 06 exécutés sur Windows Server 2022 (build 20348, .NET 10.0.14) : 6/6 PASS natifs dans le run de preuve #41134189591 ; jonction réelle refusée, rename parent tenté et bloqué, swap final réellement réussi côté attaquant mais lecture refusée, racine remplacée à chemin identique rejetée. Nouveau processus sans grant, révocation entre threads, budgets et exclusions validés ; documentation/CI finale du HEAD sans workflow temporaire encore requise. Aucune ROM réelle, aucun MEDIAS/Skia, aucune fusion ni Release.

## NEXT-05 / Avalonia SQLite + associations manuelles (PR #112, DRAFT)

- Restauration sur **fixtures IDEAL** du dernier inventaire SQLite via `ICatalogStore`, consultation historique **OFFLINE / STALE** dans les vues Avalonia existantes, sans scan ni revalidation implicite.
- Sélection explicite de jaquette depuis un index médias synthétique sauvegardé, confirmation séparée, décision manuelle persistée et historisation des remplacements/révocations ; identité durable root/plateforme/chemin, jamais ID de run.
- Recette programmée : MSTest, Windows natif, captures UI supplémentaires et vérification ZIP/SHA-256 du HEAD de PR. **Résultats encore à vérifier.** Permissions publiques d'accès réel inchangées ; aucun accès personnel, merge ni Release.

PERSIST-01 #111 — correctifs P1 validés sur l'ancien HEAD (nouvelle CI après synchronisation à vérifier) : restore validé sur staging (schéma/FK/version/colonnes) avant remplacement, rollback après swap ; claims append-only par sujet et révision, provenance historique préservée, migration v2→v3 et tests adversariaux. ZIP Windows / nouveau HEAD et DEV 2 re-revue requis, aucun accès réel.

## PERSIST-01 — SQLite minimal sur fixtures (backend en revue)

- Stockage applicatif `ICatalogStore` et schéma SQLite versionné : Library, StorageRoot, ScanSnapshot, MetadataClaim, UserDecision, sans ES-DE maître, sans identité de jeu fabriquée.
- `SaveAttempt` transactionnel conserve Current Complete en cas de Partial/Failed/Offline/Stale, historique des décisions manuelles et claims sourcées, `LoadLastKnown` restaure Offline/Stale sans scan ni SSD.
- Backup SQLite cohérent avec `integrity_check`, migration v1→v2→v3, journal DELETE, sauvegarde/restauration de fixtures. Dépendances natives épinglées et lockfiles générés sur Windows.
- Canaris PERS-01…08 Windows et tests source-neutral, CI/ZIP et revue DEV 2 à valider. **Stockage personnel, scan réel, UI, fusion et Release désactivés.**

## NEXT-05 — DEV 3 / Avalonia médias sur fixtures (PR en revue)

- Fiche observée enrichie d'une galerie de candidats/références sourcés et d'aperçus PNG/JPEG déclenchés manuellement à partir de tampons RGBA du service #108, sans fichier lu par la View/VM. Ambiguïtés, absent localement et non comparable affichés sans choix automatique.
- Annulation, changement de sélection/run et Dispose des ressources de présentation, permissions de scan et preview personnelles toujours refusées ; harnais Windows NEXT-05 réutilisé avec 9 captures historiques et 10 médias supplémentaires sur fixtures fictives uniquement.
- Pas de décision d'association, ajout manuel, persistance, refonte graphique V3, scan personnel, fusion ni Release dans ce lot ; CI/ZIP à vérifier sur HEAD final.

## NEXT-06 — Backend de présentation et aperçu média (PR #108, revue en cours)

- Services source-neutral `IMediaPresentationService` et `IMediaPreviewService` ; lecture des candidats de #107 sans ES-DE obligatoire, états de provenance sans validation automatique.
- Preview PNG/JPEG réel sur fixtures synthétiques via SkiaSharp déjà présent, service public `REAL_MEDIA_PREVIEW_HOLD` ; sources ≤8 Mio, dimensions ≤2048 px/axe et 3,5 Mpx, buffers RGBA cumulés ≤16 Mio, sortie ≤512 px, cache nul et sémaphore unitaire.
- Canaris MU-01..08 ajoutés ; CI, consommation de mémoire native, Windows GUI/ZIP et test SSD réel à qualifier au HEAD exact. Aucun View/ViewModel, SQLite, scan privé, scraper, écriture source, fusion ou Release.

## NEXT-05 / DEV 3 — interface Avalonia MVVM des fichiers observés (30.10.2026)

- Nouvelle fenêtre « Inventaire de fichiers · sans ES-DE » accessible depuis l'accueil vide et la barre latérale. Service normal InventoryApplicationService() toujours fail-closed : aucun scan personnel autorisé, aucune persistance.
- ObservedLibraryViewModel gère Dashboard / Collection / fiche, filtres plateforme-extension-type, recherche, pagination et sélection. Les lignes proviennent uniquement de InventoryObservationProjection.GetPage (NEXT-05 #105), sans duplication du scanner ni conversion en EsDeGame.
- Observations, couverture, disponibilité, fraîcheur, diagnostics, dernier complet et dernière tentative distingués. Jeu identifié et association candidate non établis sans preuves ; jaquettes locales non associées, l'index MEDIA indépendant est un lot futur.
- Un harnais NEXT05_CI strictement isolé fournit la preuve sur IDEAL, CHAOS et MYSTERY sans XML ; CI Windows, captures et ZIP standard autonome contrôlés sur HEAD. Aucun scan d'une ROMothèque personnelle, SSD réel, image réelle, SQLite, fusion ou Release.

## NEXT-05-2 — Media-index backend on synthetic fixtures (PR review pending)

- Independent explicit MEDIAS root indexing, no XML/ROM access: bounded PNG/JPEG header inspection (not image decoding), provenance and diagnostics, WebP marked unsupported, fail-closed production permission.
- Pure read-only media-to-file reconciliation with explicit scoped mapping, sourced exact paths, unique-convention candidates, ambiguity and offline/incomplete gates. No covers accepted silently, no SQLite or view changes.
- N05-M-A..J run exclusively on invented temporary fixtures, including 2,200 mixed references; results and CI require review at the exact PR HEAD. No personal scan, source write, scraper, fusion or Release.

## NEXT-05-1 — Inventaire autonome, backend fictif (PR à réceptionner)

- Projection pure des observations NEXT-02/03 par `InventoryObservationProjection.GetPage`, filtres plateforme/extension/type/texte, ordre stable, pagination et label provisoire issu du fichier. Aucun `GameId`, titre commercial ni média inventé ; `ConfirmedGameCount=null` conservé.
- Service de production toujours fail-closed ; racines temporaires fictives autorisées exclusivement dans les tests. Canaris N05-A à J : IDEAL/CHAOS/MYSTERY sans XML, archives, playlists, annulation, offline, reparse et 2 200 observations mesurées. CI HEAD final à vérifier.
- Pas de SQLite, scraper, scan personnel, fusion ou Release. Index médias indépendant et DocFX à traiter plus tard sous mandat distinct.

## NEXT-04.1 / UX P2 — Lisibilité des dates, diagnostics et actions (Produit #100, 30.10.2026)

- Dates ES-DE présentées en français à précision inchangée (année seule, jour ou horodatage à la seconde). Valeur brute ES-DE toujours visible en fiche ; aucune altération de l'importeur, des identités ou des métadonnées.
- Diagnostics regroupés par type et fréquence avec avertissements prioritaires, détails complets et pagination existante. Aucun diagnostic omis, même de code inconnu ; captures synthétiques Windows de la page de synthèse.
- Actions « Actualiser l'import » / « Voir l'import » explicitement marquées indisponibles avant un catalogue et expliquées ; « Exports · à venir » reste un texte informatif non cliquable. Accueil NoCatalog, clavier et grille #99 conservés.
- PR UX P2 séparée et sans Release ; tests et CI à qualifier au HEAD exact, aucun contenu utilisateur utilisé.

## NEXT-04.1 / ES-DE alternativeEmulator compatibility — PR #103 (30.10.2026)

- Compatibilité ciblée avec le seul format public ES-DE à deux éléments racine : `alternativeEmulator` contenant exactement un `label`, puis une unique `gameList`. Le lecteur XML document strict reste prioritaire ; le fragment n'est accepté qu'après validation de cette structure connue, sans suppression/réécriture de préambule.
- DTD, entités externes, caractères invalides, sources supplémentaires, traversées de chemins et dépassement de 8 Mio restent refusés ; aucune importation partielle automatique. Le label alternatif n'est ni un jeu ni une métadonnée, et n'affecte pas les identités/fingerprints.
- Recettes entièrement fictives : trois emplacements XML, six systèmes ordinaires plus cinq préambules, encodages UTF-8/16, attaques de racines, atomicité du dernier catalogue et test dans l'EXE Windows natif CI-only. CI et revue sur HEAD exact en cours ; aucune ROMothèque personnelle testée, pas de fusion/Release automatique.

## NEXT-04.1 — Accueil vide, démo opt-in, Collection alignée (Produit #100/#99)

- Mode `NoCatalog` par défaut : aucun jeu fictif visible, accueil ES-DE explicite avec trois étapes, navigation vide et avertissement sur l'absence de persistance. Démo uniquement après action explicite ; fixtures et tests de démonstration préservés.
- Collection importée : grille à colonnes égales et cartes étirées jusqu'au même bord que filtres/résultats, boutons « Ouvrir la fiche » de largeur uniforme sans calculs marginaux de carte.
- Recette native Windows étendue à 18 captures, dont accueil normal/compact, navigation vide, grille ultralarge et rendu DPI 150 % simulé. Import moteur strict et protections XML inchangés ; aucun scan personnel, aucune Release.

## PACKAGING-101 — candidat portable single-file (30 octobre 2026, Issue #101)

- Candidat Windows x64 autonome non tronqué : `PublishSingleFile=true`, bibliothèques natives extraites automatiquement, EXE unique en racine, deux assets officiels externes, notices/licences conservées. Le mode CI-only reste distinct du binaire utilisateur.
- Contrôles Windows prévus sur un HEAD précis : véritable GUI Avalonia/fixtures fictives, ZIP et SHA-256, ouverture sans SDK, redémarrage après déplacement du dossier et simulation de changement de lettre par `subst`. Un SSD physique reste **NOT_EXECUTED** jusqu'à recette humaine.
- Packaging, accueil vide #100 et correctif Collection #99 regroupés dans #102 ; correction moteur ES-DE #103 intégrée au code, sans nouvel import personnel encore exécuté. Aucune Release, écriture de ROM ni persistance.

## NEXT-04 / USAGE-01 — essai Windows autonome, preuve humaine en attente (Produit #97)

- Deux ZIP distincts dans l'artefact temporaire CI : application Windows autonome et démo ES-DE déjà générée (IDEAL, CHAOS, MYSTERY, MALFORMED, LONG 155 jeux fictifs), sans Node.js ni sources à préparer sur le PC du testeur.
- DEMO-FICTIVE vérifiée avant upload et après extraction, avec SHA ZIP, manifestes SHA-256 par fichier, hash générateur/contrats, notices et consignes. Aucune ROM ou jaquette réelle.
- Le vrai picker, le vrai dialogue de confirmation et le moteur ES-DE restent inchangés. HUM-01..10 à effectuer par un humain ; `USER_VALUE_PROVEN = HOLD` jusqu'à preuve, aucune Release.


## NEXT-04 / IMPORT-HARDENING — moteur ES-DE + test Windows natif (PR Draft)

- Lecture XML bornée à 8 Mio pendant la lecture, DTD/ressources externes interdites ; `.m3u` plafonnés à 64 Kio et 256 lignes, BOM Unicode conservés, refus fail-closed, dernier `Current` préservé.
- Confinement des chemins adapté à l'OS et refus des collisions d'identité/plateforme sous Windows (sans effacer de source).
- Tests adversariaux fictifs, mesure de 2 000 références et workflow Windows natif du binaire CI-only, variantes IDEAL/CHAOS/MYSTERY et vérification d'intégrité ; aucun scan réel, réseau, Release ni nouveau package.
- Commentaires XML C# sur les contrats de l'importeur ; pilote DOC-TECH-02 #89 séparé, toujours HOLD.


## NEXT-04 — UX & BRANDING HARDENING (PR #93, recette en cours)

- Logo officiel inchangé (PNG 1960×576, ICO multitailles) : présentation centrée 228×67 DIP, barre latérale 264 DIP, interpolation haute qualité et contrôles de packaging PNG/ICO/EXE.
- Dashboard avec indicateurs à provenance explicite, zone principale bornée ; Collection et fiches plus lisibles, affichage adaptatif jaquette/métadonnées, panneaux Diagnostics et Bibliothèques réorganisés.
- Import ES-DE : clarification du dossier racine, confirmation explicite, progression sans pourcentage inventé, diagnostics en sections ; pas de scan global, scraping, écriture ou catalogue persistant.
- Recette GUI étendue à 13 captures, dont 125/200 % **simulés** en rendu Avalonia ; comparatif avec captures du lot antérieur, sans revendiquer une validation humaine du DPI ni de l'ultralarge physique. Images privées de conception jamais copiées.
- API internes de présentation et matrice de références dans `docs/ux/next04-hardening-ux-branding.md` ; aucun changement du contrat moteur ES-DE/NEXT-03. **CI et recette sur HEAD final à valider**, revue DEV 2 puis décision Contrôleur, pas de Release.


## NEXT-04 / ESDE-REAL-IMPORT-01 — diagnostics et métadonnées de l'import (29.10.2026, PR Draft)

- Le parseur ES-DE unique différencie les refus du lecteur XML (syntaxe, encodage, DTD interdite) des XML valides mais non pris en charge, sans publier de chemin personnel. Retour utilisateur avec système et ligne/colonne si disponible ; validation réelle de la ROMothèque propriétaire encore non exécutée.
- Projection de `genre`, `developer` et `releasedate` optionnels, compatibilité des anciens constructeurs, empreinte sensible aux métadonnées, garde lecture seule et conservation du dernier import valide. Tests exclusivement synthétiques ; aucune recherche implicite, aucun moteur NEXT-03 ni nouvelle dépendance.
- Compatibilité bornée avec `ES-DE/gamelists/<système>/gamelist.xml` **dans la racine explicitement choisie**, sans découverte globale ni association automatique de médias. Trois sources possibles par système : refus de toute concurrence, chemins confinés et convention d'origine affichée localement en cas d'échec de lecture.
- Le backend #92 a été fusionné dans `main` par le Contrôleur le 29.10.2026 ; l'interface reste traitée séparément par DEV 3 #93. Aucune Release.

## NEXT-04 — DEV 3 / ES-DE UX alpha (PR Draft, 29.10.2026)

- Parcours systèmes → jeux ES-DE → fiches, recherche et pagination limitées à la plateforme réellement référencée dans le XML ; mode démo et moteur NEXT-03 inchangés. Retour fiche avec contexte de recherche, page, système et défilement conservé.
- Cartes Collection responsive (1/2/3 colonnes) et zone de contenu bornée pour grands écrans ; interpolation de qualité uniquement sur le logo officiel inchangé. Consentement spécifique à l'import ES-DE, réimport sans retour forcé au Dashboard et messages de limites sur la session en mémoire.
- Raccordement de la fiche aux métadonnées optionnelles `Genre`, `Developer`, `ReleaseDate` du parseur #92 : « Non renseigné » si absent, sans confondre développeur et éditeur. Le backend #92 a été fusionné dans `main` par le Contrôleur et synchronisé dans la branche #93, avec importeur et tests inchangés octet pour octet. #93 reste Draft non fusionnée dans `main`. Aperçu NEXT-03 (simulation) accessible depuis Bibliothèques, scan personnel toujours bloqué.
- Recette Windows sur fixtures synthétiques : 147 tests .NET PASS, 11 captures Avalonia, Collection profonde (155 éléments), page 2, scroll 490, métadonnées et contexte réimport/erreur XML vérifiés sur l'arbre UI combiné. Synchronisation de la branche sur `main` effectuée ; contrôles du SHA final à vérifier avant contre-revue. Aucun index persistant, scan personnel ni Release.

## NEXT-03 — Raccordement GUI canonique limité aux fixtures (29.10.2026, PR #86 Draft)

- Le même InventoryViewModel / XAML sert le mode historique SIMULATION et un mode de recette Windows du véritable service #85 (uniquement sous binaire `NEXT03_CI`), avec titres/provenances différenciés, confirmation, observations, annulation, couverture et diagnostics. Aucun second moteur, aucun changement backend.
- Banc Avalonia Windows sur fixtures synthétiques nouvellement générées sous RUNNER_TEMP ; vérification des générations, double départ refusé et conservation de Current après annulation Partial. Compilation **distincte** et sans harnais CI du ZIP portable, licences et SHA-256 vérifiés.
- Aucun scan personnel autorisé, fusion ou Release. Recette humaine Windows restant indépendante des preuves automatisées.


## NEXT-03 — préparation d'intégration #86 (29.10.2026, sans fusion dans main)

- Réunion des dépendances UX, DEMO-02, Bibliothèques et service backend sur la branche de préparation ; fondation #84 conservée via #86, sans seconde implémentation. Retrait du seam provisoire et consommation du contrat unique #85 ; moteur et dépendances NuGet inchangés.
- Résolution de MainWindow en conservant le sélecteur de scénarios, la prévisualisation explicitement SIMULATION et la page Bibliothèques. Tests de raccordement service/ViewModel sur fixtures créées exclusivement par le banc, recette Windows préparée et limites déclarées. Aucune autorisation de scan personnel, fusion ou Release.

## NEXT-03 / Consolidation pré-raccordement MVVM (28.10.2026 — PR #86 Draft)

- Protection contre un événement dispatcher obsolète du même contexte (Running reçu après Completed), confirmation retirée après refus de sélection, sélection/effacement bloqués pendant le scan et l'annulation.
- États de preuve complets et tentatives nommés séparément, nul ≠ zéro sur Failed, message sans données, diagnostics (sévérité/code/portée), couverture statut/partition et provenance observation ; disposition des commandes adaptée à 760×480.
- Tests supplémentaires exclusivement avec faux service/preview en mémoire et recette Windows préparée. Aucun scanner, donnée personnelle, vraie UI intégrée à Collection, fusion, maquette V2 ou Release.


## NEXT-03 / MVVM Avalonia — aperçu simulé (28.10.2026, Draft)

Ajout d'une fenêtre Avalonia typée dédiée, picker natif de chemin sans scan, confirmation à usage unique, commandes ViewModel, chargement/annulation, états Complete/Partial/Failed/Offline/Stale et inspecteur Observation fictifs. InventoryPreviewService est un simulateur sans accès aux données et non un deuxième moteur. Tests ViewModel / service simulé ; contrats #82/#83 et intégration #75/#76/#77 toujours en attente. Aucune Release ni bibliothèque réelle.


## NEXT-03 — Fondation MVVM isolée DEV 3 (28.10.2026 ; Draft)

- Ajout d'un `InventoryViewModel` **non raccordé** à Avalonia, notifications `INotifyPropertyChanged`, commandes `ICommand` et projections `Current/LastAttempt`, observations, diagnostics et compteurs `null` = Inconnu. Le service applicatif #83 est simulé pour les tests ; aucune nouvelle énumération ou lecture de disque.
- Contrat provisoire de passage en attente de DEV 1 et ADR `docs/ux/next03-mvvm-foundation.md`. `CommunityToolkit.Mvvm` évalué mais **pas encore introduit** (aucun changement de packages.lock ou licence ZIP) ; bindings typés prévus pour la future vue locale. Aucun changement des écrans, aucune fusion, Release ou ROM personnelle.

- DEMO-02 revue croisée avec la recette DEV 2 #76 (28 scénarios) et la UI Draft DEV 3 #77 : matrice détaillée des preuves moteur versus tests UI, état NOT_EXECUTED/NOT_IMPLEMENTED pour les parcours absents. Deux tests ajoutés pour exécuter le script Windows en sortie temporaire et tester l'annulation NEXT-02 sans perdre le dernier complet. Aucun moteur, fixture canonique, scan réel ou Release modifié.

## DEMO-02 — recettes synthétiques E2E (DEV 1 / PR Draft #75, 28.10.2026)

- Procédure Windows PowerShell 5.1 opt-in pour générer et vérifier IDEAL/CHAOS/MYSTERY à destination locale nouvelle et explicitement choisie ; aucun écrasement, téléchargement, scan réel ni Release.
- Quatre tests E2E supplémentaires exécutent les moteurs existants NEXT-01 et NEXT-02 sur les fixtures DEMO-01, avec les 30 titres, 13 plateformes, oracles 32/30/180, 33/30/166, 32/0/0, diagnostics de fichiers/médias absents, 3 doublons candidats, playlist multidisque et stabilité de relecture. Aucun nouveau moteur ni modification de la génération historique.

## LIBRARY-UX-02 — sélection des scénarios et médias ES-DE (PR Draft #77, 28.10.2026)

- Sélection explicitement fictive IDEAL, CHAOS, MYSTERY ; navigation par Dashboard, Collection et fiche. Scénarios IDEAL/CHAOS décrits par contrats, mais non revendiqués comme des fichiers observés ; MYSTERY ne présente **aucun jeu identifié**.
- Galerie d'images limitée aux types pris en charge par l'import ES-DE existant : jaquette, miniature, logo ; badges honnêtes pour média introuvable, illisible, non renseigné, provenance du contrat et ROM seulement prévue. Pas de scraping, scan, export ou gestion de bibliothèque réelle.
- Tests unitaires du contrat de présentation, capture Windows cinq vues aux deux tailles et artifact ZIP de démonstration avec provenance SHA. Workflow nouveau ajouté à la liste de chemins autorisés **exactement par son nom**, avec contrôles de sécurité associés ; protections de Release et baseline historique inchangées.
- **En cours de revue** : aucune fusion ni Release avant CI complète et recette propriétaire.

## NEXT-03 — service applicatif DEV 1 (PR Draft #85, 28.10.2026)

- Service `IInventoryApplicationService` ajouté dans l'assembly V2 **existante**, sans extraire Core ni modifier l'unique moteur NEXT-02. Interfaces et coordination sans dépendance Avalonia/MVVM au niveau source.
- Préflight limité à la sélection locale consentie, scan de fixture en tâche de fond, single-flight, annulation, `Current/LastAttempt`, observation/diagnostic/couverture et états d'origine. Le constructeur public bloque les scans de vraies bibliothèques.
- Nouveaux tests synthétiques IDEAL/CHAOS/MYSTERY, annulation pendant playlist, concurrence, échec Offline, propagation de l'état Stale simulé et intégrité des fixtures. **PR Draft, aucun raccordement Avalonia, fusion ni Release.**

## DEMO-LIBRARY-01 — catalogue canonique DEV 2 (PR Draft #72, 28.10.2026)

- Le propriétaire a fixé **30 titres et 13 plateformes** dans [Issue #71](https://github.com/TobbyCy/RomothequeManager/issues/71). Les références de conception sous `contracts/demo-library-01/` et `docs/architecture/` contiennent métadonnées, médias projetés, scénarios IDEAL/CHAOS/MYSTERY et oracles arithmétiques. Le générateur DEV 1 #73 attend son alignement sur cette version canonique.
- **Documentation exclusivement** : aucun fichier ROM, BIOS, image, XML ni test de génération sur ce catalogue livré ; aucune Release ni modification du produit.

## NEXT-01 / PR #64 — recette HEAD exact et hors ligne (28.10.2026)

- Le ZIP Windows CI est construit depuis le HEAD courant de #64, vérifié par SHA, plutôt que l'ancien `e775cdd6` ; manifeste et sidecar SHA-256 exacts.
- Vérifications spécifiques Windows et fixtures fictives : PE WinExe, ouverture via ShellExecute, lancement offline sous blocage Windows Firewall sortant pour l'exécutable, import/diagnostics visibles dans l'arbre Avalonia, supports locaux, XML malformé et restauration.
- La recette manuelle de Tobby (Explorer, clavier/GUI et déconnexion réelle du PC) reste distincte ; aucun moteur NEXT-02, scan réel, merge ou Release.


Ce journal distingue le **code fusionné sur `main`**, les preuves CI attachées à un SHA et les **versions réellement distribuées**. Le projet ne possède encore **aucune GitHub Release** ni ZIP Windows autorisé au téléchargement.


## Vérification de packaging de recette — NEXT-01-UX (28 octobre 2026)

- Workflow GitHub Actions [#40689889760](https://github.com/TobbyCy/RomothequeManager/actions/runs/40689889760) : ZIP autonome corrigé figé sur `e775cdd64522d45ee2fb848df232fa1f19023326` et [artifact #12816457518](https://github.com/TobbyCy/RomothequeManager/actions/runs/40689889760/artifacts/12816457518). Le ZIP applicatif pèse **75 362 552 octets** et son SHA-256 vérifié indépendamment est **`fe6a4d59adfae5ea171c229838d17fdeef4bd23a41e03924d2e5a93f6c407364`** ; manifeste embarqué et 62 tests .NET PASS.
- Exécutable extrait : PE Windows GUI (subsystem 2), smoke PASS. **Tobby doit encore tester en double-cliquant le .exe** et en lisant les diagnostics ES-DE de la fixture ; les tests ne simulent pas le geste dans Explorer.
- Après une intégration concurrente sur `main`, la référence de base de la PR #64 a été rafraîchie pour éliminer un échec de politique `MERGE_PARENTS_INVALID` lié au décalage d'historique. Politique maintenue inchangée, nouveau passage CI requis pour qualifier l'ensemble.
- Aucun merge de PR, aucune Release, aucun scan ni modification d'une ROMothèque réelle.


## En cours — NEXT-01-UX / ZIP corrigé WinExe (28 octobre 2026)

- Recette UX-ARTIFACT-CI désormais **figée sur le source correctif `e775cdd64522d45ee2fb848df232fa1f19023326`**, et non plus l'ancien SHA `cc35abf7`. Les garde-fous PR #64 / acteur propriétaire / droits read-only / fixture fictive restent intacts.
- ZIP autonome win-x64, licences et fichier .sha256, vérification à l'extraction, smoke et inspection PE du **sous-système Windows GUI** sur l'exécutable extrait. Guide Tobby actualisé : double-clic sans console et diagnostics d'import en français.
- CI et artifact Actions requis avant livraison. Aucun merge, Release ou scan réel.


## En cours — VISUAL-POC-01 retouches P1/P2 (28 octobre 2026)

- Navigation latérale plus contrastée : active/inactive/survolée/indisponible, avec canaris de lisibilité des textes.
- Cartes et marges moins hautes, sidebar déroulante à 760×480, six captures Windows à plusieurs échelles de rendu.
- Pas de nouvelles fonctionnalités : mode ES-DE de lecture seule, droits branding réservés et gates de distribution inchangés.


## En cours — VISUAL-POC-01 / Issue #67 (28.10.2026)

- Nouvelle PR Draft empilée sur #64 : Dashboard Darcula Retro centré sur la collection, accès bibliothèques, Exports désactivés et diagnostics avec états et origine explicites. Aucun moteur, scan automatique ni opération physique.
- Transfert octet pour octet des quatre SVG/ICO officiels depuis Pilotage (le PNG public reste inchangé) ; icône de fenêtre/barre des tâches et ressource ICO native de l'exécutable. Droits graphiques réservés, GO propriétaire limité à Produit et artifacts de test.
- Contrôles d'assets et workflows CI révisés **sans relâcher les règles de sécurité**, nouveaux tests fictifs et captures de rendu Avalonia sur runner Windows à confirmer. Maquette historique non disponible pour comparaison pixel à pixel ; aucun merge/Release.


## En cours — NEXT-01-UX-FIX / PR #64 (28 octobre 2026)

- Recette Windows propriétaire : l'exécutable Avalonia devient une application graphique `WinExe` pour éviter la console ouverte au double-clic. Le smoke Windows automatisé reste requis dans la CI.
- Diagnostics ES-DE : libellés français compréhensibles, regroupement « points à vérifier / informations », plateforme et entrée identifiables, sans assimiler une ROM ou un média indisponible à une suppression. Erreurs de gamelist, XML et réimport explicitées, dernier catalogue valide conservé.
- Correctifs uniquement de présentation et de configuration Windows ; adaptateur ES-DE de lecture seule inchangé. Quatre canaris MSTest synthétiques ajoutés. La CI du nouveau HEAD et le verdict externe seront documentés sur [PR #64](https://github.com/TobbyCy/RomothequeManager/pull/64). Aucun merge ou Release autonome.


## En cours — NEXT-01-UX / Issue #61 (PR #64 prête pour revue, pas sur main)

- Sur la base de l'adaptateur [#62](https://github.com/TobbyCy/RomothequeManager/pull/62) désormais fusionné, présentation Avalonia de la bibliothèque explicitement sélectionnée : séparation démo/import, Dashboard/Collection/fiches et jaquettes locales via API de lecture seule. Les images invalides ou absentes peuvent céder la place à la miniature, puis à un message explicite ; A/B synthétique désactivé pour le catalogue importé.
- Recherche et pagination à 50 jeux ; navigation conservant les identités et refus des erreurs de chargement/réimport sans perdre le dernier catalogue validé. Douze tests sources synthétiques NEXT-01-UX, incluant 2 000 entrées fictives et l'isolation de la comparaison A/B.
- **CI/captures à valider sur le HEAD exact après reciblage sur main** ; ce lot n'est ni fusionné, ni distribué, ni autorisé à scanner le SSD personnel. Aucune ROM/BIOS/média tiers n'entre dans GitHub.

## En cours — NEXT-02-INVENTORY / Issue #65 (PR Draft, fixtures exclusivement synthétiques)

- Correctif P1 de la PR #68 : lecture `.m3u` bornée **pendant** les I/O (octets/lignes), annulation effective transformée en `PARTIAL/CANCELLED`, handle refermé ; accès fichiers concurrents traités conservativement en `PARTIAL/UNREADABLE_ENTRY` ; nouveaux tests synthétiques. Le contrôle de taille préalable seul n'est plus considéré suffisant.

- Moteur candidat .NET en lecture seule, autonome d'ES-DE, avec sélection explicite de racine `ROMS/`, observation de fichiers distincte des identités de jeux, plateformes/alias explicitement configurés, extensions conservatrices, relations `.m3u`, groupes de doublons seulement potentiels et limites de ressources.
- `InventorySnapshot` indépendant, `COMPLETE/PARTIAL/FAILED`, couverture, disponibilités, compteurs inconnus et conservation du dernier snapshot complet lorsque le scan suivant échoue. Aucun scan automatique au lancement, aucune opération sur ROMothèque réelle.
- Dix-huit tests MSTest synthétiques proposés/exécutables sur répertoires temporaires ; preuves CI et revue DEV 2 à rattacher au HEAD exact. Contrat candidat : PR documentaire #66, non gelé sans revue.

## En cours — NEXT-01-A / Issue #61 (PR Draft, données synthétiques)

- Adaptateur .NET de lecture seule des métadonnées ES-DE vers une projection de catalogue en mémoire, avec chemins ROM/médias confinés, identités relatives, jaquettes et playlists multidisques.
- Préparation complète avant réimport, sans perte silencieuse du dernier état utile ; identité de bibliothèque stable pour la session et déplacement explicite vérifié, ouverture de média revalidée à la demande, avec tests synthétiques de lecture, sécurité, idempotence et indisponibilités.
- Aucun branchement UX, accès à une collection personnelle, modification des fichiers sources, merge ni nouvelle Release dans ce lot DEV 1.

## En cours — DIST-ZIP-01 / Issue #50 (PR Draft, aucun upload)

- Suite au GO propriétaire de **préparation de distribution de test**, ajout d'une voie `workflow_dispatch` manuelle et distincte du CI PR/push : phase `validate` sans upload, phase `publish` réservée à un SHA/ZIP/tag exacts et aux variables d'approbation finale du propriétaire.
- L'emballage vérifie les notices tierces et leur inventaire, le SHA-256, deux archives identiques, les fichiers extraits un à un, l'absence de contenus interdits et le smoke des quatre vues depuis le ZIP extrait.
- La publication visée, **après un second GO final**, est une **GitHub Pre-release « POC SYNTHÉTIQUE / TEST UNIQUEMENT »**, jamais une version stable : brouillon, retéléchargement/contrôle des deux assets, puis publication.
- **HOLD actuel :** notices validées DEV 2 non encore intégrées, contrôle indépendant Agent et artefact exact non vérifié pour distribution ; aucun ZIP téléchargeable.

## Intégré sur `main` — V2-01C / [PR #53](https://github.com/TobbyCy/RomothequeManager/pull/53) (non distribué)

- [Issue #50](https://github.com/TobbyCy/RomothequeManager/issues/50) : préparation d'un ZIP Windows x64 autonome (runtime .NET inclus), avec SHA-256, manifeste et notice explicite « POC SYNTHÉTIQUE ».
- **CI ciblée :** publication self-contained et tests depuis un dossier extrait distinct, comparaison de la liste des fichiers et contrôle d'absence de ROM/BIOS/sauvegardes. Le smoke automatisé exige l'ouverture de la fenêtre et quatre vues ; les parcours clavier, focus, scrolling et redimensionnement interactifs restent à tester sur Windows externe.
- **Distribution suspendue :** ZIP et empreinte restent temporaires dans le runner CI. Aucun upload GitHub Actions, Release, Pre-release, signature ou partage public n'est autorisé ; revue des notices tierces et GO propriétaire distinct nécessaires.

## Intégré sur `main` — V2-01B / [PR #48](https://github.com/TobbyCy/RomothequeManager/pull/48) (non distribué)

- [Issue #47](https://github.com/TobbyCy/RomothequeManager/issues/47) : quatrième vue Avalonia Bibliothèques & snapshots, trois bibliothèques cohérentes avec six jeux fictifs, états indépendants, provenance, sélection A/B et comparaison **préqualifiée** à six classes. Modèle de présentation synthétique versionné, sans moteur métier C# parallèle.
- **Contrats et garde-fous intégrés :** validation fail-closed des partitions de couverture (multi-root, ROMS/MEDIAS, preuves uniques/exactes), champs A/B et scopes concordants avec les générations canoniques, refus des faux COMPLETE, rendu honnête de l'index inaccessible et libellé « Disponibilité ». Canaris MSTest positifs/négatifs.
- **Oracle de démonstration :** fixture `UI-only / handcrafted deterministic demo / NOT_ENGINE_VERIFIED`, trois paires A/B dont les six classifications et dénominateurs A∪B sont vérifiés contre des attentes codées en dur. Aucune prétention d'équivalence avec le comparateur PS5.1.
- **Limite UX :** `INTERACTIVE_KEYBOARD_SCROLL_NOT_TESTED` ; le smoke quatre vues ne certifie pas le parcours clavier/focus/scroll réel.
- **Statut : code intégré, pas distribué.** Le ZIP V2-01C demeure un candidat sous contrôle CI ; aucune Release.

## Intégré sur `main` — 23 octobre 2026 (non distribué)

- **V2-01A / [PR #42](https://github.com/TobbyCy/RomothequeManager/pull/42), Issue #41 — fusionnée :** premier POC Windows .NET 10 / Avalonia 12.1.4 en lecture seule, avec trois vues (Dashboard, Collection/recherche, Fiche) et six jeux entièrement fictifs en mémoire. Deux `packages.lock.json` et `global.json` SDK **10.0.401** exact (`rollForward=disable`). **8 MSTest** et smoke de fenêtre Avalonia démontrés sous Windows CI. Aucun scan, export, ROM/BIOS, persistance ou réseau métier.
- **CI-DOTNET-01 / [PR #45](https://github.com/TobbyCy/RomothequeManager/pull/45), Issue #43 — fusionnée :** provisionnement explicite du SDK 10.0.401 dans le job `.NET` via `actions/setup-dotnet` épinglé à `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4.3.1), après diagnostic hors dépôt. Pin, locks, tests non vides, canaris négatifs, gate final et interdiction d'upload ZIP inchangés. La [CI `push main` #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607), sur `408d6263be6ae1fbc77c31f6eb9fb530cb4f76e6`, est **SUCCESS**. La validation sur une image où le SDK est initialement absent reste une limite distincte du succès du run.
- **PUB-04 / [Issue #44](https://github.com/TobbyCy/RomothequeManager/issues/44) — terminé :** les quatre anciens artefacts ZIP `v2-01a-win-x64-candidate` ont été supprimés de GitHub Actions et la purge a été contre-vérifiée ([preuve](https://github.com/TobbyCy/RomothequeManager/issues/44#issuecomment-6359600451)). Les nouveaux ZIP restent produits/hashés **temporairement dans le runner**, sans téléversement. Aucune autorisation de redistribution ni garantie sur d'éventuelles copies déjà téléchargées.
- **Prototype web et moteur PowerShell :** les démonstrations locales, projections de bibliothèques/snapshots, catalogue synthétique, validations Node/Edge headless, Pester et préflights restent dans le dépôt public. Le contrat DTO 0.2 et la stack Windows ne sont pas adoptés comme architecture finale.
- **Fonctions futures non livrées :** persistance, favoris/tags, collecte réelle, export E2–E6 et distribution Windows/SteamOS. La quatrième vue Bibliothèques/snapshots est intégrée via V2-01B, sans validation UX interactive externe.

Les notes ci-dessous sont **conservées comme historique de rédaction des sprints**. Les termes « candidat », « PR Draft », « à revalider », « non fusionné » et les états d'artefacts qu'elles contiennent décrivaient leur moment d'écriture ; **ils ne constituent pas l'état courant**. Consulter [l'état produit](docs/product-status.md) pour l'inventaire actualisé.

---

## Archives des notes de sprint (contenu historique conservé)

## Non publié — développement

- **CI-DOTNET-01 / Issue #43 (candidat)** : l'image `windows-2022` mise à jour (20261021.343) n'inclut plus le SDK `10.0.401`, remplacé par `10.0.402`. Provisionnement explicite du SDK **exact** via `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4.3.1) seulement pour le job .NET, diagnostic hors dépôt, garde CI strict et canaris négatifs. Le `global.json` reste intact ; tests, gate final et interdiction des uploads ZIP à revalider sur le HEAD du correctif. Aucune Release ni fusion par DEV 1.


- Revue V2-01A : ajout de `global.json` (SDK .NET **10.0.401** exact et roll-forward désactivé), validation stricte et canaris. Audit des 225 entrées du ZIP : seulement licence MIT du Produit et notice générique, aucune attribution tierce vérifiée. **Upload des nouveaux ZIP suspendu**, build/tests/ZIP SHA toujours exécutés ; suppression manuelle des artefacts historiques requise par le Contrôleur. Aucune Release ni adoption définitive.


- V2-01A / Issue #41 (PR #42 candidate) : fenêtre Avalonia 12.1.4 sous .NET 10 avec trois vues et six jeux entièrement fictifs. Deux locks NuGet générés réellement, 8 tests MSTest exécutés, ouverture Windows des trois vues et ZIP `win-x64` self-contained de 75 Mo avec SHA-256 et artefact CI temporaire. [Preuve sur SHA source intermédiaire](https://github.com/TobbyCy/RomothequeManager/actions/runs/39904964788) ; CI du HEAD de livraison, PC externe sans runtime, licences tierces et revue indépendante restent à valider. Aucune Release, aucune ROM, aucun accès StateRoot, aucune adoption définitive.


- VAL-01 / Issue #39 : smoke **Microsoft Edge réel sous Windows**, en mode headless sur les deux pages HTML `file://` et données fictives. Pilotage CDP par Node natif sans dépendance téléchargée ; gestes souris, navigation, fiches, comparaison, index inaccessible, OFFLINE/STALE/UNKNOWN, exceptions JavaScript et requêtes réseau non autorisées. Contrôle obligatoire dans CI-0 ; preuve Edge et gate à vérifier sur SHA exact, aucun navigateur humain ni Release présumés.

- Issue #2 : correction de l'indentation du commit conditionnel dans le test Python de routage CI (`tests/test_ci_policy.py`, lignes 555–556) ; remplacement d'un canari négatif de chemin devenu obsolète (`.github/CODEOWNERS`, autorisé) par `.github/CODEOWNERS.txt` (toujours interdit et non ignoré par Git). Huit chemins négatifs contrôlés sans assouplir l'allowlist. Une assertion PS5.1 Bibliothèques découvert après déblocage de Python conserve son critère strict et affiche désormais l'exception observée en cas de refus incorrect ; gate complet à revalider.

- Issue #37 : les canaris de collision d'identité et de preuve utilisaient des références partagées aux snapshots nominaux. Chaque registre volontairement muté utilise désormais deux snapshots indépendants ; assertions additionnelles contre les mutations croisées. Validateur, refus d'index et gates inchangés ; CI à vérifier sur le HEAD exact.

- Couverture Pester CI : contrôle fermé des 22 noms de modules actuellement suivis (18 historiques + 4 extensions), tout en conservant la mesure reproductible des 18 modules historiques et de l'export E1. Aucun seuil de couverture artificiel et aucune suppression de canari ; nouvelle preuve runtime attendue sur le HEAD final.

- Guides du produit, de la contribution et de la sécurité, lexique et documentation d'architecture.
- Parcours d'interface et critères d'acceptation décrits pour les travaux futurs.
- Préparation des contrôles du moteur PowerShell 5.1 sur données synthétiques.
- Licence MIT adoptée pour les éléments originaux autorisés (`SPDX-License-Identifier: MIT`, copyright 2026 Cyril Tobler) ; garde CI à chemin exact pour `LICENSE` et admission du script préflight synthétique déjà suivi `tests/Invoke-CiE1PreflightSynthetic.ps1`. Aucun droit tiers ou média externe concédé.

**Aucune application Windows, ZIP ni GitHub Release n'a encore été publiée.** Les PR non fusionnées ne constituent pas des fonctionnalités disponibles.

## En préparation — UI #11 (PR Draft #28, non fusionnée)

- Prototype **hors ligne HTML/CSS/JavaScript natifs**, situé dans `prototype/ui11/` : navigation Dashboard, Bibliothèques, Collection, Comparaison et inspecteur.
- Quatre bibliothèques **fictives** et historique daté ; états ONLINE/OFFLINE/INACCESSIBLE/UNKNOWN, FRESH/STALE/UNKNOWN, dernier complet vérifié indépendant de la dernière tentative.
- Sélection A/B, six classifications, filtres et détail ; couverture A/B toujours affichée, y compris en cas de liste vide sous scan partiel. Aucun libellé de suppression physique.
- Miroir JavaScript du contrat de comparaison #26 **pour démonstration uniquement**, non une exécution PowerShell ni une preuve de parité avec Dev 1.
- Test V8 à DOM simulé effectué en session ; smoke Node.js fourni mais **non exécuté**, pas de capture ni de test navigateur graphique, CI/Windows **NOT_EXECUTED**.
- **Non livré** sur `main`, aucun exécutable Windows, ZIP ou Release, aucune lecture/écriture ROM.


- Sprint #13, **PR Draft #28 uniquement** : adaptateur de projection JSON synthétique `0.1.0-synthetic`, deux modes, contrôle des six comptes, de la couverture et refus sans paire précalculée. **Non fusionné, non publié** ; aucune parité PS5.1 revendiquée.


- **Sprint #15, PR Draft #28** : contrat candidat catalogue de jeux et fiche projetée, provenance datée, variantes, observations ROM séparées, navigation vers snapshots ; sample synthétique hors ligne et smoke Node ajouté (NOT_EXECUTED). Adaptateur registre aligné sur `countKnown`/compteur null. 9 contrôles V8 avec DOM simulé PASS, aucune parité PS5.1 ni recette navigateur.

## Comparaison de snapshots synthétiques — Issue #9

- Module PowerShell de comparaison A/B avec six classifications et couverture prouvée des deux générations.
- Identités `romUnitId` comparées ordinalement et sensibles à la casse ; canaris synthétiques associés.
- Aucun effet sur les fichiers ROM ; tests PS5.1 et CI sur l'arbre consolidé **non vérifiés**.

## Projection synthétique des bibliothèques — Issue #10

- Registre de bibliothèques et générations, disponibilité et fraîcheur distinctes, couverture fiable ou déclarée inconnue.
- Exporteur JSON synthétique pour interface hors ligne et canaris négatifs ; aucun effet physique sur ROM.
- Tests PowerShell 5.1 et CI sur cette intégration : **non vérifiés**.

## Catalogue synthétique partagé — PR #29

- Modules PowerShell de projection et validation du catalogue, contrat DTO candidat 0.2, consommateur JS canonique, provenance et compteurs fail-closed.
- Tests et fixtures négatives versionnés ; contrat encore candidat, pas d'export réel de collection.
- Tests Node/PS5.1 et CI de l'arbre consolidé **non vérifiés**.

## Catalogue consultable et fiches — PR #30

- Extension du prototype UI aux fiches, recherche et filtres sur données synthétiques, avec validation JS partagée et contrôles négatifs.
- Tests UI supplémentaires versionnés ; V8 simulé historique ne certifie pas Node natif, Edge ni la chaîne PS5.1→JSON→JS sur cet arbre.
- Aucune application autonome ni Release.

## CI publique et sécurité de contribution — PR #31

- Raccordement de huit suites Node du prototype à la CI, dont la vérification Unicode alimentée par le producteur PowerShell.
- Vérification des événements et permissions minimales GitHub Actions, CODEOWNERS et gate final fail-closed.
- Règles d'administration de `main` à configurer après PUBLIC. Exécution Actions sur cet arbre non vérifiée.

## Convention

Après chaque changement intégré, préciser le comportement, la compatibilité, les risques, les tests réellement exécutés et la référence de l'Issue ou PR de **ce produit**. Une version publiée nécessite un tag, des artefacts vérifiés et des notes de limites. Ne jamais annoncer un test non exécuté comme PASS.

## DEMO-LIBRARY-01 — DEV 1 / PR #73 (DRAFT)

- Générateur Node sans téléchargement ni dépendances supplémentaires, produisant des ROMs fictives en texte et de vrais PNG originaux sous destination explicitement choisie et inexistante. Source canonique stricte : PR DEV 2 #72, HEAD `968d1f61420a5d0b70eb5160e9d57d505560f2b7`, 30 titres et 13 plateformes (Xbox, PS2, PS3, PS4, PC compris). Oracles : IDEAL 32/30/180, CHAOS 33/30/166, MYSTERY 32/0/0. Tests synthétiques de génération, import ES-DE, inventaire, médias, sécurité et intégrité. Aucun merge ni Release.
