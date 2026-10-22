# Contrat v1 — comparaison de deux snapshots d'inventaire

**Statut : implémentation expérimentale, PR Draft liée à l'Issue Produit #9.** Aucun scan ni accès à un catalogue personnel. Le module `Modules/SnapshotComparison.psm1` compare deux objets déjà construits en mémoire. Ce contrat ne modifie ni le schéma du moteur historique ni le StateRoot.

## Objet d'entrée

`Compare-RmInventorySnapshots -Before $before -After $after` accepte deux objets PowerShell dont la forme minimale est :

```powershell
[pscustomobject]@{
    schemaVersion = '1.0.0'
    libraryId = 'library-fixture'             # identité logique, pas lettre de lecteur
    generationId = 'generation-before'       # distinct pour chaque génération
    capturedAtUtc = '2026-10-20T10:00:00Z'
    scanStatus = 'complete'                  # complete | partial | failed
    sourceAvailability = 'OFFLINE'           # ONLINE | OFFLINE | INACCESSIBLE | UNKNOWN
    freshness = 'STALE'                     # FRESH | STALE | UNKNOWN
    coverage = @(
        [pscustomobject]@{ console='GBA'; status='complete' }
    )                                       # complete | partial | unobserved
    units = @(
        [pscustomobject]@{
            romUnitId='unit-persistent-1'
            gameId='game-persistent-1'
            variantId='variant-persistent-1'
            console='GBA'
            observationDigest=('a' * 64)    # SHA-256 de l'observation, optionnel si inconnu
        }
    )
}
```

Les identités `romUnitId` sont comparées de manière ordinale **sensible à la casse**. `Unit-A` et `unit-a` désignent deux unités distinctes ; la comparaison ne doit jamais les fusionner implicitement.

Les identités `romUnitId`, `gameId` et `variantId` sont émises par le moteur d'identité du catalogue : **pas d'identité automatique à partir du chemin, du titre ou de `fileId`**. `fileId` est local à un scan et interdit dans les entrées comparées. `observationDigest` correspond à l'empreinte d'observation calculée par le mécanisme d'identité existant ; une empreinte manquante est une information inconnue, pas une égalité. Chaque entrée déclare une plateforme présente dans `coverage`. Les identités répétées, générations identiques, dates incohérentes, schémas non reconnus ou périmètres de bibliothèque différents sont rejetés.

## Interprétation : C1 / intersection prouvée

Ce MVP compare uniquement des **unités ROM** appartenant à la même bibliothèque logique, sur deux générations datées (B non antérieure à A). La preuve de comparabilité est volontairement stricte : **pour une plateforme donnée, les deux snapshots doivent chacun déclarer `scanStatus='complete'` ET `coverage[console].status='complete'`**. Les deux conditions doivent être vraies côté A **et** côté B, y compris pour un élément seulement présent dans l'un des deux inventaires. Il n'existe **aucune inférence de complétude** à partir d'une liste de fichiers, d'une date, d'un disque accessible ou d'un compteur.

Le niveau `console` n'est pas une preuve pour plusieurs racines, disques ou domaines ROMS/MEDIAS. Les fixtures de ce contrat représentent **une source logique synthétique par console** ; si une bibliothèque réelle requiert un `rootKey` ou des domaines distincts et que le producteur ne peut pas garantir la complétude de ce périmètre, **ne pas alimenter le comparateur avec un indicateur `complete` trompeur**. Reporter la zone comme non prouvée. L'extension à des sous-zones exigera un autre contrat revu, sans extrapolation automatique.

## C2 — Six résultats, distincts et explicites

| `classification` (API) | Condition, **après** preuve C1 de couverture intégrale des deux côtés |
| --- | --- |
| `ADDED` | `romUnitId` présent seulement dans B, absent du périmètre entièrement observé en A |
| `CHANGED` | Unité commune, propriétaires `gameId`/`variantId`/`console` concordants, deux `observationDigest` connus et différents |
| `UNCHANGED` | Unité commune, propriétaires concordants, deux `observationDigest` connus et identiques |
| `NOT_OBSERVED_IN_B_COVERED_SCOPE` | Unité présente dans A, non retrouvée dans le périmètre entièrement observé en B |
| `UNKNOWN` | Donnée insuffisante : scan partial/failed, scope partial/unobserved, ou digest absent sur une unité commune |
| `NOT_COMPARABLE` | Incompatibilité démontrée (identité/propriétaire discordant) ou périmètre **non déclaré** dans A ou B, ne permettant pas de prouver une intersection commune |

La validation d'entrée refuse globalement les structures invalides (bibliothèque discordante, version non prise en charge, génération répétée, date impossible, identité dupliquée ou `fileId` inter-scans). `NOT_COMPARABLE` est une classification **locale** sur un `romUnitId` quand la contradiction est isolable ; les erreurs structurelles globales lèvent une exception et ne sont jamais transformées en résultats trompeurs. Une zone `partial` ou `failed` connue existe mais manque d'observations : `UNKNOWN` ; une plateforme **non déclarée** ne permet même pas d'établir l'intersection : `NOT_COMPARABLE`.

**Portée des digests :** `observationDigest` est une empreinte canonique d'**observation** d'unité (structure, chemins, tailles/horodatages observés selon le moteur), **pas** un hash du contenu binaire ROM. Ainsi `CHANGED` signifie *observation différente*, **jamais** « octets ROM modifiés » ; `UNCHANGED` signifie *empreintes observées égales*, **jamais** preuve d'identité physique du fichier. Les entrées communes fournissent `changedFields[]` ou `comparedFields[]` sur le champ `observationDigest` avec preuves A/B et niveau `observedStat`. Aucune affirmation de changement d'octets, d'effacement, de conversion ou de réassociation USB.

`NOT_OBSERVED_IN_B_COVERED_SCOPE` est exclusivement une absence **dans un inventaire daté** ; l'UI doit afficher « Non retrouvé dans l'inventaire B (zone couverte) », **jamais « supprimé »**. OFFLINE et STALE sont des états orthogonaux de consultation : une projection historique intégralement vérifiée reste comparable si elle est accessible. Un essai de scan partial/failed ne remplace pas un ancien snapshot complet.

## Sortie

Les champs racine `schemaVersion`, `libraryId`, identités de générations, dates, états de scan/disponibilité/fraîcheur, `counts` et `entries` sont conservés. Les compteurs sont maintenant indexés sur les **six valeurs exactes** de `classification` ci-dessus ; leur dénominateur correspond au nombre d'unités distinctes dans l'union A∪B pour **cette** comparaison, et non à tous les fichiers physiques d'une bibliothèque.

Chaque entrée triée ordinalement par `romUnitId` conserve `gameId`, `variantId`, `console`, `beforeDigest`, `afterDigest`, `reason` et `classification`, avec `changedFields[]`, `comparedFields[]` pour les seules preuves observables. `reason` est un code stable, pas une supposition. Aucun accès à disque, réseau, XML, registre ni données utilisateur réelles. Le résultat est reproductible sur les deux mêmes objets.

## Frontière d'architecture et tests

Ce composant est une **projection en mémoire** destinée à une future vue Bibliothèques (LIB-03/LIB-06), non une nouvelle opération du moteur historique. Les fixtures sont créées en mémoire par `tests/Invoke-CiSnapshotComparison.ps1`. Elles couvrent l'égalité, les changements, les ajouts, l'absence observée seulement si prouvée, les lacunes `partial`/`failed`, les zones inconnues, `OFFLINE`/`STALE`, les conflits et la validation stricte. Le harness rejoint le gate Windows existant ; **l'exécution réelle reste NOT_EXECUTED tant qu'aucun résultat daté au HEAD de la PR n'est disponible**. Aucun test sur collection réelle, et aucune protection CI contournée.

**Coordination Dev 2 :** ce contrat n'impose pas de stockage SQLite, d'API, de technologie UI ou de nouveaux DTO au moteur existant ; toute adoption dans les bibliothèques et snapshots nécessite une revue de l'architecture sur le HEAD courant.
