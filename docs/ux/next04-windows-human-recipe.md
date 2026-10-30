# NEXT-04 / USAGE-01 — recette humaine Windows sans Node.js

**État : HUM-01..10 = NOT_EXECUTED.** Le vrai exécutable, le picker natif, la confirmation et le moteur #96 sont utilisés, **pas** un mode simulé.

## Préparation (deux ZIP, un seul artefact)

Ouvrir le [workflow Windows UX](https://github.com/TobbyCy/RomothequeManager/actions/workflows/next04-ux-proof.yml) sur le **dernier HEAD de #93** ; télécharger l'artefact temporaire contenant **un ZIP application**, **un ZIP DEMO-FICTIVE**, leurs `*.zip.sha256` et les captures CI. Vérifier séparément les SHA avec `Get-FileHash -Algorithm SHA256 <fichier.zip>` et décompresser dans deux dossiers distincts. L'application Windows est autonome : **aucun Node.js, Git, SDK, source ou installation** nécessaires.

Lancer `RomothequeManager.V2.exe`, puis **Bibliothèques → Import ES-DE**. Sélectionner **`DEMO-NEXT04/IDEAL`** (contient directement `ROMS/`), jamais son sous-dossier `ROMS/`. Annuler une fois le picker, puis la confirmation, puis recommencer et **confirmer** l'import réel en lecture seule.

Le pack a les racines suivantes : `IDEAL` (30 jeux entièrement fictifs, 13 systèmes) ; `CHAOS` (manquants) ; `MYSTERY` (sans XML, refus attendu) ; `MALFORMED` (XML cassé, refus attendu) ; `LONG` (155 jeux fictifs GBA, test réel page 2/scroll). Les pseudo-ROMs sont des **fichiers texte inertes**, les PNG sont générés, sans couverture commerciale. Le pack comporte notices, SHA par fichier et provenance. Aucune bibliothèque privée n'est autorisée.

## Grille à renseigner : PASS / FAIL / NOT_EXECUTED

| ID | Action humaine Windows | Attendu |
| --- | --- | --- |
| HUM-01 | Vérifier 2 ZIP/hash, lancer EXE, icône/logo | Autonome et utilisable |
| HUM-02 | Ouvrir picker, cliquer **Annuler** | Aucun import |
| HUM-03 | Choisir IDEAL puis **Annuler la confirmation** | Aucun import |
| HUM-04 | Rechoisir IDEAL, **Confirmer** | 30 jeux / 13 systèmes ; Systèmes → Collection → Fiche → retour |
| HUM-05 | Recherche/filtre, fiche, retour, réimport | États, métadonnées et sélection cohérents |
| HUM-06 | Import CHAOS, puis tenter MYSTERY et MALFORMED | Manquants documentés, refus sans perte de dernier `Current` |
| HUM-07 | Import LONG, page 2, scroll/recherche/fiche/retour/réimport | Pagination 50, position conservée |
| HUM-08 | Choisir `IDEAL/ROMS` au lieu d'`IDEAL` | Refus clair, sans scan implicite ni chemin sensible |
| HUM-09 | Tab/Shift+Tab/Entrée/Échap/focus, redimensionnement | Parcours atteignable, icône/logo lisibles |
| HUM-10 | Relancer EXE et vérifier fichiers fictifs inchangés | Pas de persistance ni modification source |

Pour un **FAIL** : `action → attendu → observé → sévérité P0/P1/P2`. Relever Windows, HEAD, **SHA de chacun des deux ZIP**, écran/DPI **réellement** testés et, au besoin, captures **uniquement fictives**. Les DPI simulés en CI ne sont pas des tests humains. Ne pas inférer la réussite du picker depuis les 13 PNG.

**Interdits :** ROMs personnelles, BIOS, jeux réels, suppression, export, release, scan NEXT-03 réel. **USER_VALUE_PROVEN reste HOLD** avant la recette humaine attestée et décision du Contrôleur.
