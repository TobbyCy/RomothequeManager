# GRAPHIC164 — Fallback universel (préparation, pas d'intégration)

Sources existantes et inchangées : sources/fallback/{dark,light}.svg et sources/fallback/{dark,light}-small.svg. Ces quatre SVG neutres ne sont liés à aucune PlatformKey ni marque. Contraste de contours indicatif sur fonds des planches > 13:1 ; la version small utilise une boîte de 30/64 et exige une capture réelle à 16/24 avant une distribution.

IMPORTANT : le ZIP GBA/PS2 actuellement livré ne contient PAS ces quatre fallback. Il n'est donc PAS un paquet autonome complet. Conserver sa valeur de snapshot de candidat de design ; ne pas présenter un fonctionnement runtime testé.

## Contrat futur de distribution

1. Sous nouveau GO seulement : empaqueter les assets approuvés, les quatre SVG fallback et, si nécessaire, leurs propres PNG 16/24/32/48/64/128/256, licence/provenance, manifeste et SHA-256 de l'archive.
2. Vérifier CRC ZIP, paths relatifs, absence traversal, symlinks, doublons, scripts, TTF, fichiers externes et limites de tailles. Revalider la concordance manifeste/source/PNG et les cas corrompus avant diffusion.
3. Comportement spécifié mais NON IMPLÉMENTÉ : plateforme inconnue, asset absent, style/thème indisponible, fichier corrompu, ou droits non approuvés => fallback neutre lié au thème disponible, sans mapping deviné ni lecture automatique de ROM ou média.
4. L'interface future fournira un libellé accessible séparé et testera focus, DPI, zoom, 760×480 et Dark/Light après GO spécifique DEV 3.

Droits : Nintendo/GBA et Sony/PS2 restent OWNER_DECISION / NOT_APPROVED_FOR_BRAND_DISTRIBUTION ; les anciens glyphes DejaVu restent de version exacte NOT_VERIFIED.

Le QA actuel du dépôt remplace le QA historique CI=PENDING embarqué dans le ZIP initial. Cette archive n'a pas été régénérée dans la présente correction, pour ne pas casser la validation binaire antérieure.


**État actuel :** les quatre SVG fallback sont présents dans le ZIP candidat réconcilié, sans mapping `PlatformKey` ; ne pas utiliser le pack pour une distribution officielle sans droits, CI verte et mandat d'intégration.
