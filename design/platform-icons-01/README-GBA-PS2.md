# PLATFORM-ICONS-01 — GBA et PS2 (candidat graphique)

Lot autorisé par #120/6631392880, **design-only**. Branche dédiée issue de main ; seuls les fichiers du sous-arbre graphique #120 sont repris du commit historique 2a2d0d3c0eb8520271ab8dafab224707850b23bd, sans `identity-v3-proposal` ni code.

**Livrables nouveaux** : 2 sujets × 3 styles (`console-name`, `controller`, `console-silhouette`) × Dark/Light = 12 variantes, **24 SVG de source** + **2 planches comparatives SVG**, 84 PNG transparents 16/24/32/48/64/128/256 dans le ZIP `PLATFORM_ICONS_GBA_PS2_CANDIDATE.zip`. Pour `console-name`, dès 32 px badges 3:1 ; en 16/24 px pictogrammes seuls 1:1. Les autres styles sont 1:1. PNG : rendu optique pixel-grid (pas un rendu anti-aliasé du SVG), destiné à lecture/revue et non certifié identique au rendu Avalonia.

Le fallback neutre historique est préservé. `platformKey=null`, `platformMappings=[]` : **aucun mapping ni appartenance à une bibliothèque réels**. Absence de style, thème ou asset devra produire un fallback neutre côté runtime futur ; ce fonctionnement est **NOT_TESTED**. Windows/Avalonia, focus, DPI, 760×480, et CI native : **NOT_TESTED**. Droits : `OWNER_DECISION`, revue indépendante DEV 2 indispensable. Pas d'intégration, fusion ni Release.
