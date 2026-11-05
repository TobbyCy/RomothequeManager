# PLATFORM-ICONS-01 — GBA et PS2 (candidat graphique)

Lot autorisé par #120/6631392880, **design-only**. Branche dédiée issue de main ; seuls les fichiers du sous-arbre graphique #120 sont repris du commit historique 2a2d0d3c0eb8520271ab8dafab224707850b23bd, sans `identity-v3-proposal` ni code.

**Livrables nouveaux** : 2 sujets × 3 styles (`console-name`, `controller`, `console-silhouette`) × Dark/Light = 12 variantes, **24 SVG de source** + **2 planches comparatives SVG**, 84 PNG transparents 16/24/32/48/64/128/256 dans le ZIP `PLATFORM_ICONS_GBA_PS2_CANDIDATE.zip`. Pour `console-name`, dès 32 px badges 3:1 ; en 16/24 px pictogrammes seuls 1:1. Les autres styles sont 1:1. PNG : rendu optique pixel-grid (pas un rendu anti-aliasé du SVG), destiné à lecture/revue et non certifié identique au rendu Avalonia.

Le fallback neutre historique est préservé. `platformKey=null`, `platformMappings=[]` : **aucun mapping ni appartenance à une bibliothèque réels**. Absence de style, thème ou asset devra produire un fallback neutre côté runtime futur ; ce fonctionnement est **NOT_TESTED**. Windows/Avalonia, focus, DPI, 760×480, et CI native : **NOT_TESTED**. Droits : `OWNER_DECISION`, revue indépendante DEV 2 indispensable. Pas d'intégration, fusion ni Release.

## Finalisation #164 — état du 04.11.2026

Le QA du dépôt qa-gba-ps2.json présente le vrai CI_FAIL / PATH_POLICY_BLOCK observé, ainsi que le blocage latent ZIP binaire. Consultez GRAPHIC164_VISUAL_REVIEW.md, FALLBACK_DISTRIBUTION.md, PLATFORM_ICONS_NEXT_PACK_PROPOSAL.md et CI_PATH_POLICY_REQUIREMENTS.md.

IMPORTANT : l'archive ZIP candidate a été régénérée exclusivement pour synchroniser manifeste, QA, README, provenance et licence avec le dépôt et pour inclure les quatre SVG fallback historiques. Les 84 PNG et les 26 SVG candidats/planches précédents sont byte-identiques. Les CRC ZIP et chunks PNG, tailles, alpha, et les 119 entrées ont été revérifiés après reconstruction. Cette validation du paquet ne vaut ni CI verte, ni validation Windows/Avalonia, ni droit de redistribution.

## Archive réconciliée — package-only

Le ZIP comporte désormais 119 fichiers : 84 PNG GBA/PS2, 24 SVG de variantes, 2 planches SVG, 4 SVG fallback neutres, 5 documents synchronisés. Il ne contient toujours **aucun PNG fallback** et n'est donc pas garanti exploitable tel quel par un runtime PNG-only. Voir PACKAGE_CONSISTENCY.md et FUTURE_AVALONIA_CONTRACT.md pour les conditions futures. Les exports originaux restent octet-identiques, aucune adoption officielle ni PlatformKey.
