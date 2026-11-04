# GRAPHIC164_FUTURE_AVALONIA_CONTRACT — spécification préparatoire uniquement

**Statut :** DESIGN_CONTRACT_ONLY / NO_IMPLEMENTATION / NO_PLATFORM_MAPPING. Les assets de la PR #164 ne sont pas intégrés à Avalonia et ne sont pas approuvés pour la diffusion en tant que marques Nintendo/Sony.

## Données et sélection

Entrées futures envisagées : (1) `PlatformKey` canonique stable provenant d'un contrat #7 approuvé, (2) `StyleId` parmi `console-silhouette`, `controller`, `console-name`, (3) thème `dark` ou `light`, (4) hauteur cible en pixels/DPI et (5) registre d'assets soumis à approbation de droits. Les `designId` `study-gba` et `study-ps2` ne sont PAS des `PlatformKey` et ne doivent jamais servir de mapping automatique.

Précondition de résolution : `runtimeContract=true` uniquement après un GO produit distinct et une table explicite validée `(PlatformKey, StyleId, Theme) → asset` avec statut `APPROVED_FOR_DISTRIBUTION`, provenance, attribution et chemins locaux sûrs. **La version actuelle reste `runtimeContract=false`, `platformMappings=[]`, `platformKey=null` pour toutes les études.** Elle ne doit pas déclencher la sélection de ces icônes dans l'application.

Algorithme FUTUR (pseudocode normatif, non exécuté) :

```text
resolveIcon(platformKey, style, theme, targetHeight):
  if platformKey absent/unknown OR pack disabled: return neutralFallback(theme, targetHeight)
  if no explicit approved mapping for exact platformKey/style/theme: return neutralFallback(theme, targetHeight)
  if rightsStatus != APPROVED_FOR_DISTRIBUTION: return neutralFallback(theme, targetHeight)
  asset = validatedLocalAsset(mapping, style, theme, targetHeight)
  if missing/unsafe/decode failed/corrupt/incorrect size: return neutralFallback(theme, targetHeight)
  return asset with separate accessible platform label
```

Pas de déduction à partir de noms traduits, labels ES-DE, noms de dossiers, chemins ROM, variantes PlayStation, ou correspondances approchées. Pas de réseau ni de scraping. Si la variante Dark/Light souhaitée manque, utiliser le **fallback neutre du thème courant** ; ne jamais substituer silencieusement une icône d'une autre plateforme ou un logo tiers non approuvé. Si le fallback correspondant est corrompu, prévoir un placeholder neutre intégré au code produit futur, validé séparément.

## Formats, DPI et dimensions

| Hauteur cible | Source prévue | Règle |
|---|---|---|
| 16 et 24 px | `-small.svg` ou PNG exact 16/24 | Picto 1:1 sans texte. Simplification optique et vérification visuelle réelle obligatoires ; possibilité de fallback neutre tant que non vérifié. |
| 32 et 48 px | SVG normal / PNG exact | Icône carrée 1:1 ; `console-name` uniquement dans un conteneur 3:1 (96×32, 144×48). |
| 256 px | SVG normal / PNG 256 | Picto 256×256 ; badge 768×256 seulement en surface 3:1. Pas d'étirement, conserver marges transparence. |

64 et 128 px suivent les mêmes règles que 32–256. Conversion DIP→pixels doit utiliser le facteur DPI réel avant choix de source. Si une taille exacte n'existe pas, choisir un SVG local approuvé, ou un raster égal/supérieur redimensionné en conservant ratio et contraste. Ne pas agrandir une source raster 16 px jusqu'à 256 px.

## Style par défaut et limites

Proposition propriétaire : `console-silhouette` à partir de 32 px **si la représentation a été approuvée juridiquement** ; sinon fallback neutre. Les petites tailles 16/24 doivent pouvoir utiliser une forme optique simplifiée ; aucune preuve de rendu Avalonia/Windows, DPI 100–200 %, écran 760×480, zoom ou clavier n'existe à ce jour. Le style `controller` peut être ambigu entre consoles ; `console-name` consomme une largeur triple et ne remplace jamais le texte accessible distinct.

## Distribution et sécurité

Le candidat ZIP contient depuis la réconciliation les 4 fallback SVG et les 84 PNG sujet, mais aucun PNG fallback. Future intégration : charger des ressources **approuvées et embarquées dans l'application**, jamais à partir du SSD ROMS/MEDIAS ; valider `AssetPath`, CRC/tailles/contenu et absence de références SVG externes. Fallback sur ressource manquante, corrompue ou inconnue sans fuite de chemin utilisateur. Réglage de style optionnel : persistance dans préférences applicatives sous GO, non dans bibliothèque utilisateur. Ne pas activer les clés métier sans contrat #7.

Revue future DEV 3/DEV 2 après mandat distinct : 16/24/32/48/256, Dark/Light, ratios, DPI 100/125/150/200, viewport 760×480 et 1120×720, tabulation, zoom, alt/label accessible, fichiers manquants et corrompus. CI verte et droits approuvés nécessaires avant toute distribution.

`GRAPHIC164_FUTURE_AVALONIA_CONTRACT / NOT_IMPLEMENTED / NEXT05_NOT_BLOCKED`.
