# Contribuer à ROMothèque Manager

Merci de contribuer en respectant deux invariants : **aucune donnée réelle** et **aucune modification implicite des bibliothèques**. Ce guide est destiné à des développeurs humains ; aucun accord de contribution ne constitue une permission d'exécuter une opération sur des ROM.

## Signaler et proposer

Choisir un formulaire Bug ou Fonctionnalité, avec version/OS, reproduction à l'aide de fixtures inventées, attendu/observé, limites et preuve expurgée. Si une même demande existe, la compléter plutôt que créer un doublon. Pour une vulnérabilité, suivre [SECURITY.md](SECURITY.md) : **ne jamais la détailler dans une Issue publique**.

## Développer

1. Lire la [roadmap](docs/roadmap.md), les [ADR adoptés](docs/decisions/README.md), l'[architecture](docs/architecture/moteur-actuel.md) et le [guide développeur](docs/developer/README.md).
2. Créer une branche dédiée depuis une base à jour (feature/, fix/, docs/, chore/). La demande correspondante doit être identifiable par sa **référence Produit**.
3. Limiter le diff à l'objectif, respecter les schémas/identifiants existants, conserver les octets protégés lorsque requis. Pas de changement de code PS5.1 « cosmétique » qui détruit BOM/CRLF.
4. Utiliser uniquement des jeux de tests synthétiques (Pester, canaris, préflight). Distinguer résultats réellement exécutés, refus attendus et scénarios seulement proposés.
5. Ouvrir une PR Draft avec le modèle fourni. Ajouter base/HEAD, changements et effets, tests applicables, limites, documents à tenir à jour, licence des assets et risques.
6. Répondre à une revue indépendante, obtenir le contrôle final applicable et **laisser le mainteneur autorisé fusionner**. Un statut Project « Done » ou une approbation humaine sans CI ne prouve pas que le code a été testé.

## CI adaptative et documentation

Les changements Docs/PS/.NET/Shared/Infra entraînent des contrôles distincts ; le gate final doit refuser toute vérification obligatoire absente, skipped, failed ou cancelled. Voir [CI adaptative](docs/developer/ci-adaptative.md). **.NET** n'est pas un test réussi lorsqu'aucun projet .NET n'existe. Tout changement de fonctionnalité documente son état réellement livré dans [statut](docs/product-status.md) et [changelog](CHANGELOG.md) selon la politique de CI.

Les contributions de tiers passent par les protections GitHub disponibles, sans exécution privilégiée de PR issues de forks. N'ajoutez ni token ni secret. Les images/icônes, extraits externes et dépendances ne sont pas redistribuables sans vérification des droits.

## Qualité

Un correctif n'est prêt que si ses tests positifs et négatifs pertinents sont reproductibles, la provenance/portée est explicite, les cas OFFLINE/STALE/partial n'inventent pas de suppression, la documentation correspond au comportement livré et aucun élément confidentiel n'a été inclus.
