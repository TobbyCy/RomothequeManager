# Vue 02 — Collection (conception validée, non implémentée)

**Disposition initiale : Mixte grille**. Galerie centrale de jaquettes et inspecteur de l'élément sélectionné à droite. Quatre modes doivent rester accessibles sans perte de contexte : **Grille, Liste, Mixte grille, Mixte liste**.

## Interactions

Recherche par titre et filtres de plateforme, bibliothèque, genre, région, favoris, tag et état connu. Tri explicite ; combinaison et remise à zéro des filtres claire. Après ouverture d'une fiche complète, retour avec sélection, filtres, tri et position de défilement préservés. Inspecteur contextuel redimensionnable avec jaquette, métadonnées sourcées, variantes et lien « Ouvrir la fiche ».

## Données manquantes et ambiguës

Jeu, variante, unité ROM, fichier de scan et entrée ES-DE ne sont pas interchangeables. Pas de regroupement automatique sur le seul titre. Média indisponible = placeholder explicite ; source ROM OFFLINE ≠ jeu supprimé ; partial/stale/unknown restent visibles et n'effacent pas les entrées d'un snapshot cohérent. Favoris/tags futurs exigent stockage personnel durable indépendant de l'index ; non livré aujourd'hui.

## Réception future

COL-01 quatre modes ; COL-02 retour sans perte de filtre/scroll ; COL-03 source Offline avec index disponible ; COL-04 jaquette absente et fallback ; COL-05 variantes non fusionnées ; COL-06 navigation clavier et contrôles accessibles. **NOT_EXECUTED.**
