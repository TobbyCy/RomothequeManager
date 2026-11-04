# GRAPHIC164_CI_REQUIREMENTS_FINAL — inventaire exact (informatif)

**Aucun GO de modification de la politique dans cette PR.** Ce document est destiné au futur lot infrastructure approuvé par le propriétaire. État au HEAD de référence `ffc112e795ed911e4fe954029ab54341a3472ed7` : Adaptive #41886656338 FAILURE / `TRACKED_PATH_NOT_ALLOWED:design/platform-icons-01/CI_PATH_POLICY_REQUIREMENTS.md`; le second blocage est le décodage strict UTF-8 d'un ZIP binaire dans `tests/ci_policy.py::audit_entries`.

## Liste exacte des fichiers qui apparaîtront dans la PR #164

**82 chemins sous `design/platform-icons-01/`**, et aucun chemin ailleurs. Inventaire figé à cette mise à jour, à comparer au diff GitHub du HEAD exact avant toute modification de policy.

```text
design/platform-icons-01/CI_PATH_POLICY_REQUIREMENTS.md
design/platform-icons-01/CI_REQUIREMENTS_FINAL.md
design/platform-icons-01/CONSERVATION_90.md
design/platform-icons-01/CONTRAT_CANDIDAT.md
design/platform-icons-01/FALLBACK_DISTRIBUTION.md
design/platform-icons-01/FUTURE_AVALONIA_CONTRACT.md
design/platform-icons-01/GRAPHIC164_VISUAL_REVIEW.md
design/platform-icons-01/PACKAGE_CONSISTENCY.md
design/platform-icons-01/PI01_Controle_16_64.png
design/platform-icons-01/PI01_Direction_Dark_Light.png
design/platform-icons-01/PLATFORM_ICONS_01_Preproduction.zip
design/platform-icons-01/PLATFORM_ICONS_GBA_PS2_CANDIDATE.zip
design/platform-icons-01/PLATFORM_ICONS_NEXT_PACK_PROPOSAL.md
design/platform-icons-01/PROVENANCE-GBA-PS2.md
design/platform-icons-01/PROVENANCE.md
design/platform-icons-01/README-GBA-PS2.md
design/platform-icons-01/README.md
design/platform-icons-01/font-provenance.json
design/platform-icons-01/licenses/DejaVu-LICENSE-upstream.txt
design/platform-icons-01/licenses/DejaVu-notice.txt
design/platform-icons-01/pack-manifest-gba-ps2.json
design/platform-icons-01/pack-manifest.json
design/platform-icons-01/qa-gba-ps2.json
design/platform-icons-01/qa.json
design/platform-icons-01/sheets/GBA_PS2_dark.svg
design/platform-icons-01/sheets/GBA_PS2_light.svg
design/platform-icons-01/sources/fallback/dark-small.svg
design/platform-icons-01/sources/fallback/dark.svg
design/platform-icons-01/sources/fallback/light-small.svg
design/platform-icons-01/sources/fallback/light.svg
design/platform-icons-01/sources/study-gba/console-name/dark-small.svg
design/platform-icons-01/sources/study-gba/console-name/dark.svg
design/platform-icons-01/sources/study-gba/console-name/light-small.svg
design/platform-icons-01/sources/study-gba/console-name/light.svg
design/platform-icons-01/sources/study-gba/console-silhouette/dark-small.svg
design/platform-icons-01/sources/study-gba/console-silhouette/dark.svg
design/platform-icons-01/sources/study-gba/console-silhouette/light-small.svg
design/platform-icons-01/sources/study-gba/console-silhouette/light.svg
design/platform-icons-01/sources/study-gba/controller/dark-small.svg
design/platform-icons-01/sources/study-gba/controller/dark.svg
design/platform-icons-01/sources/study-gba/controller/light-small.svg
design/platform-icons-01/sources/study-gba/controller/light.svg
design/platform-icons-01/sources/study-nes/console-name/dark-small.svg
design/platform-icons-01/sources/study-nes/console-name/dark.svg
design/platform-icons-01/sources/study-nes/console-name/light-small.svg
design/platform-icons-01/sources/study-nes/console-name/light.svg
design/platform-icons-01/sources/study-nes/console-silhouette/dark-small.svg
design/platform-icons-01/sources/study-nes/console-silhouette/dark.svg
design/platform-icons-01/sources/study-nes/console-silhouette/light-small.svg
design/platform-icons-01/sources/study-nes/console-silhouette/light.svg
design/platform-icons-01/sources/study-nes/controller/dark-small.svg
design/platform-icons-01/sources/study-nes/controller/dark.svg
design/platform-icons-01/sources/study-nes/controller/light-small.svg
design/platform-icons-01/sources/study-nes/controller/light.svg
design/platform-icons-01/sources/study-playstation/console-name/dark-small.svg
design/platform-icons-01/sources/study-playstation/console-name/dark.svg
design/platform-icons-01/sources/study-playstation/console-name/light-small.svg
design/platform-icons-01/sources/study-playstation/console-name/light.svg
design/platform-icons-01/sources/study-playstation/console-silhouette/dark-small.svg
design/platform-icons-01/sources/study-playstation/console-silhouette/dark.svg
design/platform-icons-01/sources/study-playstation/console-silhouette/light-small.svg
design/platform-icons-01/sources/study-playstation/console-silhouette/light.svg
design/platform-icons-01/sources/study-playstation/controller/dark-small.svg
design/platform-icons-01/sources/study-playstation/controller/dark.svg
design/platform-icons-01/sources/study-playstation/controller/light-small.svg
design/platform-icons-01/sources/study-playstation/controller/light.svg
design/platform-icons-01/sources/study-ps2/console-name/dark-small.svg
design/platform-icons-01/sources/study-ps2/console-name/dark.svg
design/platform-icons-01/sources/study-ps2/console-name/light-small.svg
design/platform-icons-01/sources/study-ps2/console-name/light.svg
design/platform-icons-01/sources/study-ps2/console-silhouette/dark-small.svg
design/platform-icons-01/sources/study-ps2/console-silhouette/dark.svg
design/platform-icons-01/sources/study-ps2/console-silhouette/light-small.svg
design/platform-icons-01/sources/study-ps2/console-silhouette/light.svg
design/platform-icons-01/sources/study-ps2/controller/dark-small.svg
design/platform-icons-01/sources/study-ps2/controller/dark.svg
design/platform-icons-01/sources/study-ps2/controller/light-small.svg
design/platform-icons-01/sources/study-ps2/controller/light.svg
design/platform-icons-01/templates/console-name-grid-192x64.svg
design/platform-icons-01/templates/icon-grid-64.svg
design/platform-icons-01/tokens.json
design/platform-icons-01/v3-preservation-manifest.json
```

Le sous-ensemble à traiter de manière binaire comprend **deux archives ZIP nommées exactement** : `PLATFORM_ICONS_01_Preproduction.zip` (historique, blob original `8ddb05c2d1f4826d3fba4d43d555e0f53e9c8dcc`, à conserver/contrôler), `PLATFORM_ICONS_GBA_PS2_CANDIDATE.zip` (nouveau blob Git `d66bc2a9ed3e773490b9c4fc20704f94e7248f59`). Deux aperçus historiques `.png` sont également suivis. Les 84 PNG GBA/PS2 du candidat sont internes à l'archive.

## Politique minimale attendue — sous GO infrastructure distinct

- Accès strict aux chemins exacts ci-dessus, **pas de `design/.*`, `*.zip`, répertoire arbitraire, TTF ou extension nouvelle**. Préserver contrôles Unicode/casse/collisions, statuts Git, modes et reparse/symlinks.
- ZIP : validation fail-closed des **119 entrées stockées** du candidat : 84 PNG, 24 SVG variantes, 2 planches SVG, 4 SVG fallback neutres et 5 documents de provenance/QA/manifeste/licence ; refuser traversal, absolus, slash inverse, liens, noms dupliqués, compression/méthode non autorisée, tailles excessives, entrées inattendues, CRC invalides et archives imbriquées. Ne pas supposer que les noms permis hors ZIP rendent les contenus permis dedans.
- PNG : signature, IHDR, dimensions/ratio listées au manifeste, taille bornée, CRC des chunks, `colorType=3`, `tRNS` index transparent zéro et IEND sans traîne ; vérifier 84/84 correspondances. SVG : éléments permis seulement, pas script, `foreignObject`, `image`/réseau, références distantes, import police, data URI ; tailles et viewBox conformes, et 4 fallback SVG présents.
- Manifestes et QA : `runtimeContract=false`, `platformMappings=[]`, 12 `platformKey=null`, fallback sans mapping ; concordance exacte manifeste/ZIP/SVG et QA synchronisée. Fiche juridique maintenue : marques `OWNER_DECISION`, ancien DejaVu `NOT_VERIFIED`.
- Canaris positifs (pack valide, 82 chemins attendus) et négatifs (chemin externe, zip ailleurs, CRC cassé, signature/dimension/alpha erronée, conflit manifeste, traversal, symlink, SVG script/réseau, TTF, doublon, archive zip bomb). Ne pas masquer un échec par `SKIPPED` ou politique générique.

**Aucune CI verte revendiquée.** L'inventaire devra être recompté si le lot infrastructure ajoute des preuves ou si de nouveaux fichiers apparaissent ; ne pas modifier cette PR pour contourner le classement.

`GRAPHIC164_CI_REQUIREMENTS_FINAL / INFRA_OWNER_GO_REQUIRED / POLICY_UNCHANGED`.
