# Architecture actuelle et trajectoire — lecture publique

**Architecture implémentée historique :** un noyau Windows PowerShell 5.1 structuré en modules, avec identifiants métier stables, rapprochements de métadonnées, journaux/transactions et couche Export E1. Le code et les tests sont en cours de transfert vers un dépôt produit à historique indépendant ; consulter leur HEAD après la PR technique.

## Modules et frontières

- Noyau identité/configuration/inventaire/associations/métadonnées : fichiers sous Modules/, avec sérialisation et contrôles explicites.
- Export E1 : observation read-only, sans conversion, suppression ni écriture sur données personnelles.
- Tests Pester : synthétiques, canaris d'omission, contrôles PowerShell 5.1 et sécurité de chemins.
- Préflight : diagnostic séparé de l'export complet, sans pouvoir d'autoriser un scan réel.

## Invariants

Identifiants gameId, variantId et romUnitId distincts des titres/chemins. metadataEntryId identifie les entrées ES-DE ; une entrée folder n'est pas un jeu. **fileId n'est stable que dans une exécution de scan donnée**, pas une identité inter-inventaire. Absence d'élément dans un scan partiel ≠ suppression physique. Une observation de disponibilité n'est pas un résultat de scan, et un snapshot daté n'établit pas l'état actuel.

## V2 à l'étude

Une future application Windows portable pourrait séparer moteur, services de lecture, projection de catalogue, données personnelles et interface. C#/.NET, Avalonia, SQLite et des contrats V2 sont des **candidats**, non des technologies adoptées par ce document. Les jobs, l'annulation, la publication fiable, Offline/Stale et les comparaisons de snapshots exigent encore des tests futurs sur fixtures. Aucun SDK/.NET ni GUI n'est livré aujourd'hui.

Voir [contrat historique](contrat-moteur-historique.md), [ADR publiables](../decisions/README.md) et [qualité](../developer/ci-adaptative.md).
