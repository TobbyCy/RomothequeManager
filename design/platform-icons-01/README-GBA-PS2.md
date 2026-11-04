# PLATFORM-ICONS-01 — GBA et PS2 (candidat graphique)

Lot autorisé par #120/6631392880, **design-only**. Branche dédiée issue de main ; seuls les fichiers du sous-arbre graphique #120 sont repris du commit historique 2a2d0d3c0eb8520271ab8dafab224707850b23bd, sans `identity-v3-proposal` ni code.

**Livrables nouveaux** : 2 sujets × 3 styles (`console-name`, `controller`, `console-silhouette`) × Dark/Light = 12 variantes, **24 SVG de source** + **2 planches comparatives SVG**, 84 PNG transparents 16/24/32/48/64/128/256 dans le ZIP `PLATFORM_ICONS_GBA_PS2_CANDIDATE.zip`. Pour `console-name`, dès 32 px badges 3:1 ; en 16/24 px pictogrammes seuls 1:1. Les autres styles sont 1:1. PNG : rendu optique pixel-grid (pas un rendu anti-aliasé du SVG), destiné à lecture/revue et non certifié identique au rendu Avalonia.

Le fallback neutre historique est préservé. `platformKey=null`, `platformMappings=[]` : **aucun mapping ni appartenance à une bibliothèque réels**. Absence de style, thème ou asset devra produire un fallback neutre côté runtime futur ; ce fonctionnement est **NOT_TESTED**. Windows/Avalonia, focus, DPI, 760×480, et CI native : **NOT_TESTED**. Droits : `OWNER_DECISION`, revue indépendante DEV 2 indispensable. Pas d'intégration, fusion ni Release.

## Finalisation #164 — état du 04.11.2026

Le QA du dépôt qa-gba-ps2.json présente le vrai CI_FAIL / PATH_POLICY_BLOCK observé, ainsi que le blocage latent ZIP binaire. Consultez GRAPHIC164_VISUAL_REVIEW.md, FALLBACK_DISTRIBUTION.md, PLATFORM_ICONS_NEXT_PACK_PROPOSAL.md et CI_PATH_POLICY_REQUIREMENTS.md.

IMPORTANT : le ZIP original est conservé octet pour octet et contient encore son QA initial (CI=PENDING). La documentation versionnée dans le dépôt fait foi pour la CI ; les SVG et PNG restent conformes au snapshot revu par DEV 2. Ne pas annoncer une archive synchronisée ni un résultat Windows/Avalonia PASS. Toute régénération ultérieure requiert nouvelle QA binaire et revue indépendante.
