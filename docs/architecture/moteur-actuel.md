# Architecture actuelle et trajectoire — lecture publique

**Architecture implémentée historique :** un noyau Windows PowerShell 5.1 structuré en modules, avec identifiants métier stables, rapprochements de métadonnées, journaux/transactions et couche Export E1. Les sources et leurs tests sont en cours de revue ; consulter les fichiers et résultats présents sur le commit effectivement testé.

## Modules et frontières

- Noyau identité/configuration/inventaire/associations/métadonnées : fichiers sous Modules/, avec sérialisation et contrôles explicites.
- Export E1 : observation read-only, sans conversion, suppression ni écriture sur données personnelles.
- Tests Pester : synthétiques, canaris d'omission, contrôles PowerShell 5.1 et sécurité de chemins.
- Préflight : diagnostic séparé de l'export complet, sans pouvoir d'autoriser un scan réel.

## Invariants

Identifiants gameId, variantId et romUnitId distincts des titres/chemins. metadataEntryId identifie les entrées ES-DE ; une entrée folder n'est pas un jeu. **fileId n'est stable que dans une exécution de scan donnée**, pas une identité inter-inventaire. Absence d'élément dans un scan partiel ≠ suppression physique. Une observation de disponibilité n'est pas un résultat de scan, et un snapshot daté n'établit pas l'état actuel.

## V2 — POC Avalonia publié et trajectoire future

**État actuel (24.10.2026) :** un [POC Windows x64 V2-01C autonome est publié en Pre-release](https://github.com/TobbyCy/RomothequeManager/releases/tag/poc-v2-01c-20261023-4cf475fbd1ec), avec [.ZIP téléchargeable](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip) et [fichier `.sha256`](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip.sha256) ; **SHA-256 attendu** `22328d682b7c93edcdeeb735c9913880e68dec64c54b159bd6e6de77bcc7eaaf`. Extraire le ZIP dans un dossier Windows x64, puis lancer `RomothequeManager.V2.exe` depuis le dossier extrait, sans SDK/runtime .NET externe. Ce POC **.NET 10 / Avalonia** présente les vues Dashboard, Collection, Fiche et Bibliothèques, sur **six jeux et trois bibliothèques fictifs uniquement**. Le [premier lancement utilisateur est confirmé](https://github.com/TobbyCy/RomothequeManager/issues/50#issuecomment-6381903496), mais les contrôles UX détaillés restent non attestés.

**Trajectoire future, distincte du POC livré :** séparation moteur/services/projection, catalogue personnel autonome, persistance (dont SQLite à étudier), import ES-DE, jobs/annulation, lecture fiable des médias, gestion Offline/Stale métier, comparaisons de snapshots réelles, exports et opérations physiques. Aucune de ces capacités n'est livrée par cette Pre-release, qui ne lit ni ne modifie de collection réelle. Les réflexions historiques présentant .NET/Avalonia comme simples **candidats** précédaient l'intégration du POC ; elles ne décrivent plus son état actuel.

Voir [contrat historique](contrat-moteur-historique.md), [ADR publiables](../decisions/README.md) et [qualité](../developer/ci-adaptative.md).
