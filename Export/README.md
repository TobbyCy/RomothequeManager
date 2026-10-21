# Export E1 — inventaire et sécurité

La couche Export E1 du moteur PowerShell est conçue pour **inventorier en lecture seule** et produire des diagnostics structurés sur des données autorisées. Son existence ne donne **aucune autorisation d'exécuter une collecte sur un disque réel**.

## Ce qui existe

- Modules PowerShell 5.1 et script d'inventaire en lecture seule ; leur disponibilité dépend de la version réellement intégrée dans le dépôt.
- Suites synthétiques et vérifications de chemins, identités, rôles ES-DE game/folder, variantes et cas négatifs.
- Préflight diagnostique distinct du lancement complet et de toute opération physique.

## Ce qui n'existe pas comme permission

E2 (sélection), E3–E5 (dépendances, préparation, simulation) et E6 (copie réelle) ne deviennent pas des capacités opérationnelles du fait d'une maquette, d'un contrat ou d'une PR. Les conversions, scraping et opérations irréversibles sont hors des tests de contribution.

## Tests

Pour comprendre les préflights et les limitations des fixtures : [préflight synthétique](../docs/developer/preflight-synthetique.md), [qualité Pester](../docs/developer/pester-execution.md), [sécurité](../docs/user/securite-et-limites.md). Les commandes et leur compatibilité exactes seront confirmées avec l'intégration effective des scripts par le responsable technique.
