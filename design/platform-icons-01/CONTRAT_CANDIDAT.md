# Contrat graphique candidat — à examiner par DEV 2

Ce document et `pack-manifest.json` décrivent la livraison du Graphiste. **Ils ne constituent pas un nouveau schéma runtime ni une décision d'architecture.** Aucun code de résolution n'est livré.

## Frontières d'identité

- `designId` identifie une variante graphique de préproduction.
- `platformKey=null` signifie qu'aucun rattachement au catalogue n'est autorisé à ce stade.
- `exampleDisplayName` nomme seulement le sujet de l'étude. Un nom traduit, label ES-DE ou chemin de fichier ne devient jamais une clé.
- La première collection cible est la liste propriétaire complète une fois explicitement validée ; elle n'est pas déduite de ces prototypes.
- Après validation : DEV 2 vérifie les PlatformKey existants et la correspondance explicite ; le Graphiste ne crée, renomme ni rapproche aucune clé. Toute entrée ambiguë reste non rattachée.

## Axes candidats du pack

`plateforme validée` × `style` × `thème` × `hauteur en pixels`.

Styles descriptifs : `console-name`, `controller`, `console-silhouette`. Thèmes : `dark`, `light`. Les noms définitifs des contrats d'intégration restent à arbitrer par DEV 2/DEV 3.

| Cas | Comportement graphique proposé |
| --- | --- |
| Hauteur 16 ou 24 px | Source optique `-small.svg`, pictogramme seul, rapport 1:1. |
| Style console + nom, hauteur ≥32 px | Rapport 3:1. Ne pas écraser dans un emplacement carré. |
| Autres styles | Rapport 1:1. |
| Clé inconnue, mapping absent, style ou asset indisponible | Icône neutre du thème, sans substitution automatique par une autre console. |
| Emplacement avec rapport différent | Mise à l'échelle proportionnelle et centrage ; jamais d'étirement. |
| Texte omis de l'icône | Prévoir un libellé/accessibilité distinct dans l'interface future ; aucune image seule n'est une description accessible. |

Le remplacement neutre est carré dans tous les styles. Le futur hôte pourra le centrer dans l'espace réservé, sans inventer de logo. Un fichier corrompu doit aussi aboutir au remplacement ; la stratégie et les tests de chargement appartiennent à l'intégration future.

## DPI et choix de taille

Les nombres du pack sont des **pixels physiques exportés**, pas des unités Avalonia. Le futur hôte devra choisir la résolution selon la taille logique et l'échelle réelle, ou utiliser le SVG si sa chaîne de rendu le permet. Les points de changement 24→32 doivent être testés sur 100/125/150/200 % avant adoption. Rien n'est prétendu testé ici sur Windows.

## Réception indépendante attendue

1. Plateformes et mapping : cohérence avec #7, liste explicitement approuvée, inconnus préservés.
2. Droits : provenance de chaque famille, conditions de la police, marques et formes physiques ; aucun téléchargement de logos de tiers.
3. Comportement candidat : ratio des badges, libellés distincts, absence et corruption d'assets, thème et DPI.
4. Décision de production : validation de la direction et de la liste ; mandat d'intégration séparé. Aucun sélecteur ou mécanisme de préférences implémenté par ce lot.
