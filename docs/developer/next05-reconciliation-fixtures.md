# NEXT-05 — Réconciliation XML ↔ ROMS ↔ MEDIAS, fixtures synthétiques

Mandat : diagnostic **strictement fictif** (aucune collection personnelle), le 04.11.2026. Périmètre contrôlé : importeur ES-DE, références relatives, scanner Windows RS-A (métadonnées uniquement), index MEDIAS de test et `MediaLinkProjection`. Les nombres ci-dessous décrivent **la fixture créée par le test** `REC_01`, pas la collection réelle du propriétaire.

## Matrice de référence de la fixture

| Plateforme | Références XML | Fichiers ROMS observés | Références indisponibles | Images déclarées XML | Covers locales observées | Variantes par nom | Rapprochements / prudence |
|---|---:|---:|---:|---:|---:|---|---|
| GB | 19 | 19 | 0 | 19 | 19 | 0 | 19 références XML exactes (pas une attribution validée de jaquette) |
| PS1 | 106 (53 CUE + 53 CHD) | 159 (53 BIN + 53 CUE + 53 CHD) | 0 | 53 | 53 | **53 groupes** de même nom | 53 références XML exactes pour CUE ; 106 suggestions ambiguës BIN/CHD ; **aucune suppression ni fusion** |
| PS2 | 5 (2 ISO + 2 CHD + 1 absent) | 4 (2 ISO dans ISOS, 2 CHD dans CHD) | 1 | 2 | 3 | 0 prouvé | 2 références exactes ; 1 cover candidat non déclaré ; 1 sans candidat |
| PS3 | 1 référence de **dossier de jeu** | 12 fichiers *internes* dans PS3_GAME (dont PARAM.SFO et EBOOT.BIN) | 0 après correction du statut dossier | 1 | 1 | 0 prouvé | 0 jeu validé par les seuls fichiers internes ; pas de fausse jaquette automatique |
| **Total** | **131** | **194** | **1** | **75** | **76** | **53 groupes candidats** | **74 références reliées à observations de fichiers, 106 ambiguïtés, 1 candidat unique, 1 absent dans portée comparable** |

Les 12 fichiers internes PS3 **ne représentent pas 12 jeux**. La référence XML PS3 pointe sur un dossier ; **sa seule existence n'est plus suffisante**. Les deux marqueurs ordinaires et confinés `PS3_GAME/PARAM.SFO` et `PS3_GAME/USRDIR/EBOOT.BIN` doivent exister. Il s'agit d'une preuve structurelle minimale, **jamais d'une preuve de jouabilité**. La couverture RS-A ne crée pas d'observation « jeu PS3 » à partir du dossier ; elle énumère seulement des fichiers. La cover PS3 reste ainsi **déclarée et observée**, mais sans rattachement automatique à une observation de jeu. Un dossier PS2 quelconque ne doit **pas** être reconnu comme fichier ROM.

Une correspondance de nom ou de media ne signifie **ni doublon binaire, ni identité de jeu, ni couverture approuvée**. L'import XML ne prouve pas l'existence actuelle d'une jaquette sans un index MEDIAS séparément autorisé. `MediaLinkProjection` est une suggestion pure et utilise des IDs de scans, une plateforme et une portée comparables.

## Contrats et contrôles négatifs

1. La source `<game><path>./Fiction-PS3-Directory</path>` est un dossier sous `ROMS/ps3`, contenant `PS3_GAME/PARAM.SFO`, `PS3_GAME/USRDIR/EBOOT.BIN` et dix fichiers .dat fictifs. `RomExists` reflète la présence des deux marqueurs PS3 ordinaires, vérifiés sous le même périmètre ROMS, sans lecture de contenu ni test de jouabilité. Les dossiers vides, arbitraires, partiels, absents ou liés ne sont pas acceptés.
2. Les fichiers `PS3_GAME/...` peuvent être observés comme éléments techniques par l'inventaire natif, mais ne doivent jamais générer de propositions de « jeu » dans `MediaLinkProjection`, même si `EBOOT.BIN` a une extension candidate. Les ISO PS3 autonomes hors `PS3_GAME` ne sont pas bloquées par cette exclusion ciblée.
3. Les 53 groupes PS1 même basename entre .bin/.cue/.chd restent **candidats variantes** sans dédoublonnage, même si la cover de nom correspondant existe.
4. Le cas PS2 `ROMS/ps2/ISOS/Fiction-PS2-Unavailable.iso` n'existe pas : une seule référence XML doit être indisponible, sans inventer de suppression physique.
5. Une référence media `../../../../outside-the-selected-media/escape.png` est refusée par l'importeur ; un `MediaScanId` périmé produit `NOT_COMPARABLE`, jamais une absence certaine.
6. Le scanner RS-A ne reçoit une permission qu'après sélection explicite de `rm-rsa-fixture-<GUID>/ROMS` et consentement ; le test refuse le démarrage sans grant et le rejeu du grant. Il ne lit que les attributs des fichiers fictifs. L'indexeur média est la **seam de test existante** et n'est pas un contournement de l'interface de production #161.

## Écart ciblé (avant correction)

- **P1 statut PS3** : `EsDeCatalogueImporter.Read` utilisait uniquement `File.Exists(romFile)` avant #165, puis une première version de #165 acceptait trop largement `Directory.Exists(romFile)`. Le correctif final exige **à la fois** `PS3_GAME/PARAM.SFO` et `PS3_GAME/USRDIR/EBOOT.BIN`, sous confinement/reparse existant. Sans eux : `RomExists=false` et `ROM_MISSING`.
- **P1 faux candidat PS3** : `MediaLinkProjection.Reconcile` acceptait les fichiers `.../PS3_GAME/.../EBOOT.BIN` comme `ArchiveCandidate`, donc comme proposition de fichier jeu. Une exclusion de **proposition** ciblée à l'intérieur du composant `PS3_GAME` est nécessaire ; le scanner natif doit continuer d'observer ces fichiers comme **fichiers**, sans les promouvoir.

La correction reste limitée à ces deux règles et à leurs canaris ; **aucune modification des grants, de la GUI #159, du backend média #161, aucune fusion ou Release**.

## Vérification CI

- `REC_01` : valeurs exactes du tableau, complétude de scans fictifs, suggestions non approuvées et dénombrements indépendants.
- `REC_02` : PS3 structuré accepté ; vide, arbitraire, incomplet, mauvais type, absent et dossier PS2 non acceptés ; ISO PS3 autonome conservée ; identités GameId/VariantId indépendantes.
- `REC_03` : refus traversal media XML et comparaison refusée sur génération périmée.
- `REC_04` : refus des liens symboliques de dossier PS3 et marqueur `PARAM.SFO` (sous réserve du privilège Windows de création de liens ; aucun accès hors fixture).
- `REC_05` : refus traversal de chemin ROMS PS3 déclaré en XML.

Les chiffres constituent des **oracles attendus** jusqu'à exécution de la CI Windows du HEAD exact ; ne pas déclarer PASS sans ses journaux.
