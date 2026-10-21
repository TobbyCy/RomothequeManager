# Lexique ROMothèque Manager

| Terme | Sens dans le produit |
| --- | --- |
| Bibliothèque | Ensemble logique de sources de jeux/métadonnées, distinct d'une lettre de lecteur |
| Jeu | Entité gameId, différente de ses variantes et fichiers |
| Variante | Région, langue, révision ou autre déclinaison documentée |
| Unité ROM | Ensemble physique/logique représenté par romUnitId |
| Fichier observé | Entrée inventoriée, fileId **local au scan** |
| ES-DE | EmulationStation Desktop Edition et ses données de catalogue/métadonnées |
| Entrée game / folder | Métadonnée de jeu / dossier ; un folder n'est jamais automatiquement un jeu |
| Snapshot | Représentation datée d'un inventaire, avec portée et limites de couverture |
| OFFLINE | Support explicitement observé absent, **pas** collection vide ni jeux supprimés |
| INACCESSIBLE | Source observée présente ou connue mais non lisible dans le contexte donné |
| STALE | Donnée devenue trop ancienne pour un usage selon une politique à définir ; ne vaut pas suppression |
| UNKNOWN / non observé | Preuve d'état actuel absente ; ne pas choisir ONLINE ou OFFLINE par défaut |
| PARTIAL | Résultat partiel dans un périmètre déclaré ; comparaison hors périmètre non concluante |
| CI | Tests automatisés sur fichiers/fixtures, pas preuve sur une bibliothèque réelle |
| Pre-release | Version de développement à tester, pas une application de production réputée stable |
| SHA-256 | Empreinte cryptographique d'un fichier pour vérifier son intégrité de téléchargement ; ne constitue pas une signature de confiance en soi |

La [fiche de sécurité](securite-et-limites.md) explique les limites de lecture et la différence entre aperçu, simulation et opération réelle.
