# ADR-0003 — Identités et entrées ES-DE

**État historique : ACCEPTED, avec précision de portée de fileId.**

## Contexte

Plusieurs variantes ou fichiers peuvent partager un titre ; les métadonnées ES-DE peuvent être ambiguës ou se répéter. Déduire l'identité d'un nom de fichier peut entraîner de faux regroupements.

## Décision

Garder **gameId**, **variantId**, **romUnitId** et **metadataEntryId** distincts, préserver la provenance et les relations plusieurs-vers-un, refuser une fusion automatique à partir de noms voisins. Une entrée ES-DE de type **folder** reste un dossier, non un jeu. Les références non résolues sont qualifiées au lieu d'être inventées.

**Précision essentielle : fileId est un identifiant éphémère local à chaque scan**, attribué lors d'un inventaire ; il ne constitue pas un ID persistant inter-scans. Référencer un fichier par contexte de bibliothèque et de scan lorsque nécessaire, ne jamais faire correspondre deux inventaires par fileId seul.

## Conséquences

Les rapprochements restent explicables, au prix de conserver les ambiguïtés et d'exiger une validation humaine lorsque les preuves ne concordent pas. Aucun code V2 n'adopte automatiquement de nouveaux DTO en publiant ce résumé.
