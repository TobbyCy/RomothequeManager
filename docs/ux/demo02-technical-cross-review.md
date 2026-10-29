# DEMO-02 — revue technique croisée DEV 1 / contrat #76 / UI #77

**Périmètre contrôlé :** contrat DEV 2 [#76](https://github.com/TobbyCy/RomothequeManager/pull/76), HEAD `58967bbc0d1452253c2c3aa977b8184aa364ff68` ; DEV 3 [#77](https://github.com/TobbyCy/RomothequeManager/pull/77), HEAD `efe43b67e3df057bca6dabd5ca0c5f2183584fa8` ; DEV 1 [#75](https://github.com/TobbyCy/RomothequeManager/pull/75). Ces trois PR sont **indépendantes et Draft** à cette revue. Aucun code de #76 ou #77 n'est repris dans #75.

## Méthode d'interprétation du PASS/FAIL

- `AUTO_MOTEUR` = la propriété de données, d'import NEXT-01 ou d'inventaire NEXT-02 est effectivement couverte par MSTest **sur fixtures synthétiques**, soit dans DEMO-02 soit dans les tests préexistants nommés ci-dessous. Ce n'est **pas un PASS UX**.
- `PARTIEL_MOTEUR` = le contrat dispose d'une vérification partielle ou d'un test moteur historique indépendant, sans le chemin complet de la recette #76.
- `MANUEL_UI` = un comportement UI est présent dans la PR Draft #77, mais son parcours humain et son rattachement aux snapshots réels ne sont pas vérifiés par DEV 1.
- `NON_IMPLÉMENTÉ_UI` = le parcours spécifique demandé par #76 n'est pas raccordé dans #75 / #77 ; une CI verte du générateur ne l'implique pas.
- `NOT_EXECUTED` = pas d'exécution démontrée de la recette UX complète. **Aucune des 28 lignes n'est déclarée PASS UX complète par DEV 1.** Les assertions ci-dessous portent sur le sous-ensemble technique.

| ID | Preuve technique DEV 1 (ou déjà en `main`) | Reste de recette UX #76 / compatibilité #77 | Verdict croisé |
|---|---|---|---|
| LB02-01 | Générateur explicite `--output`, script Windows testé sur temp isolé ; pas de scan réel | Affichage d'accueil fictif #77, ouverture et absence de scan à vérifier sur UI réelle | AUTO_MOTEUR + MANUEL_UI |
| LB02-02 | Trois dossiers et manifests distincts vérifiés | Sélection `IDEAL` UI #77 ; pas de `libraryId` observé pour aperçu contractuel | PARTIEL_MOTEUR + MANUEL_UI |
| LB02-03 | `Demo02_ThreeSyntheticLibrariesMatchContractWithBothExistingEngines` : 30 XML / 32 ROMS / `ConfirmedGameCount=null` | #77 aperçu **contractuel**, pas de couplage live NEXT-02/ES-DE avec trois compteurs sourcés | AUTO_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-04 | `Demo02_MultidiscRemainsOneImportedGameWithTwoObservedMembers` ; playlist 1+2, une fiche ES-DE | Fiche *Metal Deer Salad* prévue #77, relation aux trois observations pas affichée | AUTO_MOTEUR + MANUEL_UI |
| LB02-05 | 13 plateformes avec alias explicites, Xbox/PS2/PS3/PS4/PC et titres vérifiés | #77 montre plateformes de **contrat**, preuve de couverture runtime absente | AUTO_MOTEUR + MANUEL_UI |
| LB02-06 | `Demo01_IdealEsDeImportsAllGamesAndOriginalPngMedia`, `Demo01_OnlySimulatedNonExecutableFilesAndVerifiablePngMedia` : PNG réels, trois rôles ES-DE seulement | #77 galerie importée via `TryOpenMedia` ; test physique de décodage/rôles restants non joué ici | AUTO_MOTEUR + MANUEL_UI |
| LB02-07 | `Demo02_ThreeSyntheticLibrariesMatchContractWithBothExistingEngines` : CHAOS 30 XML / 33 ROMS, 005/017 absents | #77 présente les manques **prévus**, pas des statuts runtime de NEXT-02 | AUTO_MOTEUR + MANUEL_UI |
| LB02-08 | `Demo02_MissingMediaAndMissingRomsHaveSeparateReadableDiagnostics` : trois `POTENTIAL_DUPLICATE`, jamais hash binaire ; 004/013/027 | #77 mentionne « candidat » générique mais ne rend pas les groupes/motifs NEXT-02 | AUTO_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-09 | CHAOS 14 PNG absents dans manifeste, diagnostics NEXT-01 pour rôles XML ; six rôles ≠ trois importés | #77 fallback importé proposé ; parcours Windows/PNG absent à confirmer | AUTO_MOTEUR + MANUEL_UI |
| LB02-10 | MYSTERY 32 fichiers, aucun XML, `ConfirmedGameCount=null`, import NEXT-01 refuse | #77 masque titres/identifiants, mais **ne présente pas le décompte observé par NEXT-02** | AUTO_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-11 | MYSTERY zéro PNG, zéro XML ; aucune inférence de 180 médias manquants | #77 « inconnus » côté preview ; aucun diagnostic médias runtime, conforme à la limite actuelle | AUTO_MOTEUR + MANUEL_UI |
| LB02-12 | NEXT-02 `CoverageProof` = ROMS et sous-plateformes, pas MEDIAS (`InventoryReaderTests`) | Badge de couverture média/UI #77 non implémenté | AUTO_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-13 | `Demo02_CancelledSyntheticNext02AttemptPreservesLastComplete` + `InventoryReaderTests.T08_PartialDoesNotReplaceLastCompleteSnapshot` | Dates/dernière tentative non raccordées à #77 | AUTO_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-14 | `InventoryReaderTests.P1_CancelDuringPlaylistReadReturnsPartialAndReleasesHandle` : seam synthétique indépendant de DEMO-01 | Annulation UI au cours d'un scan non implémentée | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-15 | `InventoryReaderTests.T15_CompleteSnapshotSurvivesLostDrive` et T07 : root absent, dernier complet conservé | #77 aucune vue de la dernière tentative FAILED/couverture | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-16 | `LibraryViewTests.OfflineStaleHistoryRemainsAccessible` (projection synthétique indépendante) | Pas d'index persistant Offline raccordé au sélecteur #77 | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-17 | `LibraryViewTests.InaccessibleIndexRendersNoConsultableHistoryOrCount` | Affichage indisponibilité depuis un index inaccessible non raccordé à #77 | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-18 | `LibraryViewTests.OfflineStaleHistoryRemainsAccessible` | Stale/horodatage réel non exposés dans l'aperçu #77 | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-19 | `LibraryViewTests.UnknownAvailabilityHasNoInventedObservationTime` | Aucun probe réel et pas d'état Unknown de scan dans #77 | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-20 | `LibraryViewTests.IncompleteCoverageStaysUnknown`, `NotObservedNeverBecomesDeletedClassification` et comparateur préexistant | #77 désactive la comparaison A/B en import ; aucun diff NEXT-02 rendu dans la vue scénario | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-21 | `LibraryViewTests.SelectionRejectsSameReversedAndUnknownSnapshots` pour le modèle historique | Sélection inter-bibliothèques A/B dans l'interface #77 non disponible | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-22 | Test UI #77 `MysteryNeverLeaksHiddenOracleGameIdentities` : `Select` vide fiche et recherche | Parcours humain changement de bibliothèque avec compare/filtre non exécuté ; comparaison #77 non implémentée | PARTIEL_MOTEUR + MANUEL_UI |
| LB02-23 | `InventoryReaderTests.T11_IdempotentMetadataAndVerifiedRelocation`, T17 pour rejet, NEXT-01 relocation vérifiée | Aucun flux UI USB/SSD réassociation ni boîte de confirmation #77 | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-24 | `EsDeCatalogueImporterTests.FailedSecondImportKeepsLastValidSnapshot`, `XmlMalformedAndDtdAreRejected`, média refusé ; PNG négatif canonique non matérialisé | UI #77 inclut smoke XML corrompu isolé ; recette complète PNG canonique et reprise sur UI Windows non exécutée | PARTIEL_MOTEUR + MANUEL_UI |
| LB02-25 | Aucune assertion moteur sur des clics Dashboard (hors objet DEMO-02) | Bouton #77 « Parcourir cette collection », mais pas de lien compteur/diagnostic vers source/couverture/date | NON_IMPLÉMENTÉ_UI |
| LB02-26 | `LibraryViewTests.IncompleteCoverageStaysUnknown` et NEXT-02 preuves ROMS uniquement | Vue plateforme hors couverture non raccordée à #77 | PARTIEL_MOTEUR + NON_IMPLÉMENTÉ_UI |
| LB02-27 | Aucun test DEV 1 de clavier/lecteur d'écran ; #77 revendique captures Windows et tailles de fenêtre | Contrôle humain 1120×720 / 760×480, focus, clavier, lecteur d'écran, DPI : **NOT_EXECUTED** | NOT_EXECUTED + MANUEL_UI |
| LB02-28 | Code DEMO-01 explicitement opt-in, fixtures temporaires, manifests SHA-256, réimports sans mutation ; test Windows lancé uniquement en sortie neuve | Absence complète d'effets réseau/système/usage personnel sur UI Windows à vérifier ; aucune certification générale | AUTO_MOTEUR (périmètre synthétique) + MANUEL_UI |

## Compatibilité technique DEV 3 #77

**PASS (compatibilité des contrats synthétiques)** : #77 lit les mêmes `catalogue.json` et `scenarios.json` en ressources embarquées et garde ses trois aperçus **sans appeler les moteurs**. CHAOS indique 005/017 manquants sans « supprimé », MYSTERY ne révèle aucun titre `demoId`, l'import volontaire de répertoire passe par l'importeur NEXT-01 et `TryOpenMedia`. Aucun moteur parallèle demandé ou ajouté dans #75.

**Écart fonctionnel documenté (non P1 DEV 1)** : la vue de scénario DEV 3 consomme des **prévisions** et n'affiche donc pas les `scanId`, `CoverageProof`, `ObservedFileCount` réellement produits par NEXT-02 (LB02-03/08/10/12/13/14/15/20/26) ; elle n'affiche pas une vue historique Offline/Partial/Stale, ni un comparateur A/B concret. Les futures recettes liées doivent rester `NOT_IMPLEMENTED`, pas `PASS`. #75 ne doit pas fabriquer un adaptateur UI/backend sans mandat DEV 3 distinct.

**Risque d'intégration :** #77 et #75 modifient les allowlists `tests/ci_policy.py` et `tests/Invoke-CiStatic.ps1` indépendamment ; lors de fusions futures, conserver **l'union minimale** des chemins autorisés, puis relancer CI complète et revue du diff. Aucune fusion autorisée dans ce mandat.

## Actions DEV 1 / P1 ciblés

- **Couverture ajoutée** : le lanceur PowerShell `Invoke-Demo02Recipe.ps1` est désormais **effectivement exécuté sur le runner Windows** en sortie temporaire choisie avec `-SkipDotNetTests`, vérifie les trois manifests et le refus d'écraser une sortie existante. Le lanceur complet **avec son `dotnet test` enfant** reste couvert indirectement par les commandes CI/tests distincts, pas joué récursivement.
- **Couverture ajoutée** : annulation d'une tentative NEXT-02 sur IDEAL après scan complet, `LastAttempt=PARTIAL` et `Current` conservé.
- Aucun changement de générateur, d'importeur, d'inventaire ou des cinq blobs normatifs. Aucune automatisation des 28 scénarios UX revendiquée.
- **GO requis** pour tout nouveau raccordement UX, monitoring de média global, simulation Offline index persisté ou scan de vraie bibliothèque.

**Verdict global : `DEMO02_TECHNICAL_REVIEW_READY` sous réserve de CI verte sur le HEAD de ces deux tests. `UX_CONTRACT_FULL_28_PASS=NO` ; recette visuelle et états UI restent partiels ou non implémentés. Aucune fusion ni Release.**
