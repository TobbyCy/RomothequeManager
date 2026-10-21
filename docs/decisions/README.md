# Décisions d'architecture publiables

Les quatre ADR ci-dessous présentent **des règles historiques adoptées**, rédigées ici sans ancien lien de mandat ni coordination interne. Leur application dans la nouvelle base est à vérifier à partir du code et des tests effectivement importés.

| ADR | Décision | Portée |
| --- | --- | --- |
| [0001](0001-isolation-des-donnees-reelles.md) | Isoler les données utilisateur des tests | Tous modules/tests |
| [0002](0002-tests-ci-et-validation-locale.md) | Distinguer CI, préflight et effet réel | Validation |
| [0003](0003-identites-stables-et-metadonnees.md) | Préserver identités et rôles ES-DE | Inventaire et catalogue |
| [0004](0004-documentation-synchronisee.md) | Documentation liée à chaque changement | Contribution/CI |

**Attention :** une proposition de technologie .NET/Avalonia/SQLite, un modèle InventorySnapshot ou une maquette acceptée pour sa présentation ne constituent pas automatiquement un ADR de stack adopté. Toute nouvelle décision devra disposer d'une Issue Produit autonome, des options contradictoires, d'un responsable, d'une date, d'une motivation et d'une revue. Les décisions non adoptées n'apparaissent pas comme « ACCEPTED » ici.
