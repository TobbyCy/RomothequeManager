# GRAPHIC164 — Revue visuelle statique du lot GBA + PS2

Statut : GRAPHIC164_VISUAL_REVIEW_READY, sous réserves. Contrôle géométrique et colorimétrique uniquement, aucune capture ni recette Windows/Avalonia exécutée.

## Dimensions, marges, styles

Sources 64×64 contrôlées : GBA console silhouette x=6–58, GBA controller x=5–59, PS2 console silhouette x=17–47, PS2 controller x=3–61. Aucun débordement de viewBox détecté dans ces études. Badges console-name 192×64 et 3:1 ; pictogrammes 1:1. Sources petites distinctes pour 16/24. Les différences de silhouette horizontale GBA et verticale PS2 sont intentionnelles, pas des mappings de plateformes.

## Lisibilité par taille (risques, sans faux PASS)

- 16 px : PS2 détails de 2/64 unités = 0,5 px, GBA 3/64 = 0,75 px. Sous-pixel : lecture réelle non attestée. Utiliser source -small sans nom.
- 24 px : détail PS2 = 0,75 px, GBA = 1,125 px. Vérification visuelle humaine requise.
- 32 px : badge 96×32, blocs de lettres 5/64 × 32 = 2,5 px, donc nom intégré insuffisant comme seule étiquette accessible.
- 48 px : badge 144×48, blocs de lettres 3,75 px ; aucun niveau de lisibilité humaine certifié.
- 256 px : badge 768×256 ou icône 256×256 ; PNG pixel-grid et SVG vectoriel ne sont pas garantis identiques sur un moteur antialiasé.

## Contrastes indicatifs calculés, fonds des planches uniquement

- Dark primaire #e3ecf5 sur #17212f : 13,58:1 ; accent #ec6f5b : 5,40:1.
- Light primaire #233043 sur #f0f4f7 : 12,05:1 ; accent #b34237 : 5,06:1.
- Fallback sur fonds correspondants : Dark 13,02:1 ; Light 13,01:1.
- Attention : remplissage interne Light #dfe8f1 sur #f0f4f7 : 1,12:1. N'utiliser que derrière les contours foncés, jamais comme information autonome.

## Décision ciblée

Pas de P1 visuel démontré sur rendu effectif. Les faibles traits aux petites tailles, la taille perçue du fallback small (boîte 30/64) et le contraste du remplissage Light sont des réserves optiques explicites. Ne pas modifier un SVG sans réexporter et revalider les PNG du ZIP ; les sources et le ZIP déjà vérifiés indépendamment restent inchangés dans ce correctif documentaire.

Style par défaut proposé après décision propriétaire et droits : console-silhouette à partir de 32 px, plus différenciant qu'une manette générique. À 16/24, variante optique contrôlée ; si validation ou droits absents, fallback neutre. console-name exclusivement dans un emplacement 3:1 et toujours avec un libellé accessible fourni par l'UI.

GRAPHIC164_VISUAL_REVIEW_READY / WINDOWS_NOT_TESTED / REAL_RENDER_NOT_TESTED / RIGHTS_OWNER_DECISION.
