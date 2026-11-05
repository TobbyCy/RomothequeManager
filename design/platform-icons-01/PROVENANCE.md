# Provenance des prototypes PLATFORM-ICONS-01

**Création :** 01.11.2026, Graphiste/Codex, pour le projet ROMothèque Manager, sur mandat de préproduction du propriétaire. Dessins construits directement en géométrie SVG ; aucun bitmap génératif ni modèle de logo importé.

| Groupe | Origine des formes | Nom / marques | Statut de réception |
| --- | --- | --- | --- |
| `study-nes` — trois styles, deux thèmes | Construction originale de pictogrammes schématiques de console et de contrôleur ; aucun visuel externe téléchargé ou décalqué | « NES » en caractères DejaVu Sans ordinaires ; aucun mot-symbole constructeur | PROTOTYPE ; examen DEV 2 en attente |
| `study-playstation` — trois styles, deux thèmes | Construction originale de pictogrammes schématiques ; touches représentées par des points génériques | « PlayStation » en caractères ordinaires ; aucun emblème PS ni mot-symbole officiel | PROTOTYPE ; examen DEV 2 en attente |
| `fallback` | Quatre compartiments géométriques originaux, indépendant d'un constructeur | Aucun nom de marque | PROTOTYPE ; examen DEV 2 en attente |
| Gabarits | Grilles, marges et axes dessinés pour ce lot | Aucun | Source de travail |

Les noms et silhouettes renvoient à des matériels existants. **L'absence d'import d'un logo ne vaut pas une conclusion sur tous les droits attachés aux noms ou formes.** Aucun droit de marque ni autorisation d'un constructeur n'est revendiqué. La diffusion de prototypes dans la branche graphique ne vaut pas approbation d'un usage officiel ou d'une Release. DEV 2 doit examiner ces éléments avant adoption. La licence MIT du code ne s'étend pas implicitement à ces graphismes ; conditions générales du projet : [BRANDING.md](../../BRANDING.md).

## Typographie

DejaVu Sans, fichier installé dans l'environnement de production ; les glyphes de noms ont été convertis en tracés. Aucun fichier de police n'est embarqué dans les assets, aucune police de constructeur n'est utilisée. La notice fournie par le paquet de polices est conservée dans `licenses/DejaVu-notice.txt`. L'empreinte du fichier de police utilisé est consignée dans `font-provenance.json`.

Les planches utilisent aussi DejaVu Sans Bold pour leurs titres éditoriaux, hors icônes. Les PNG dérivent des SVG de `sources/` avec Inkscape 1.2.2 ; les correspondances et empreintes sont disponibles dans `pack-manifest.json` et `qa.json`.

## Sources non utilisées

Aucun pack de logos Internet, image constructeur, jaquette commerciale, fichier ROM/BIOS/XML, capture de collection personnelle, ni élément du Pilotage privé n'a servi de source graphique. Aucun scraping ou accès à une bibliothèque réelle n'a été effectué. Les deux noms d'étude ne constituent pas un inventaire de la collection propriétaire.
