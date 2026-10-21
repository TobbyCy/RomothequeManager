# Contrat fonctionnel du moteur historique (Rev.3)

**Résumé public de référence, pas remplacement normatif de la spécification source ni mise à jour du schéma.** Le moteur Windows PowerShell 5.1 traite des entités distinctes : jeux, variantes, unités physiques, fichiers observés, entrées et observations de métadonnées ES-DE.

## Identités

- gameId : entité logique de jeu ; ne doit pas être inventée à partir d'une simple ressemblance de titre.
- variantId : variante/région/révision distincte.
- romUnitId : unité ROM identifiée de manière persistante par le mécanisme du moteur.
- metadataEntryId : entrée ES-DE pouvant être ambigüe ; les entrées folder restent des dossiers.
- fileId : référence **locale au scan**, à réassocier au contexte libraryId + scanId lors d'un inventaire. Ne jamais l'utiliser seul pour lier deux scans.

## Conditions de sécurité

Les décisions de rapprochement doivent être traçables, révisables selon leur propre gouvernance et explicites lorsqu'elles sont ambiguës. Un inventaire ne permet pas de conclure qu'un fichier absent d'un périmètre non analysé a été supprimé. Le moteur historique et son StateRoot ne sont jamais importés comme données de test réelles dans les fixtures GitHub.

## Référence de conformité

Les sources transférées sous Modules/ restent l'autorité de l'implémentation. Toute évolution de schéma, codec ou format de transaction doit passer par une PR technique avec tests de compatibilité et preuve de non-régression, pas par une édition silencieuse de ce résumé.

Les contrats de futures projections V2 sont des **propositions NON ADOPTÉES**, même s'ils figurent dans des études UX.
