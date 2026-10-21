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

Les identités `romUnitId`, `gameId` et `variantId` sont émises par le moteur d'identité du catalogue : **pas d'identité automatique à partir du chemin, du titre ou de `fileId`**. `fileId` est local à un scan et interdit dans les entrées comparées. `observationDigest` correspond à l'empreinte d'observation calculée par le mécanisme d'identité existant ; une empreinte manquante est une information inconnue, pas une égalité. Chaque entrée déclare une plateforme présente dans `coverage`. Les identités répétées, générations identiques, dates incohérentes, schémas non reconnus ou périmètres de bibliothèque différents sont rejetés.

## Interprétation

La comparaison porte sur la **même bibliothèque logique** et deux générations datées, avec `After` non antérieur à `Before`. Elle est déterministe et **sans effets de bord** : aucune lecture de disque, réseau, XML, fichier ROM ou registre persistant.

| Classe | Condition |
| --- | --- |
| `added` | Identité présente après, absente avant, **et** snapshot avant et couverture de la plateforme complets |
| `modified` | Même `romUnitId` et propriétaire stable, deux digests connus et différents |
| `unchanged` | Même `romUnitId` et propriétaire stable, deux digests connus et égaux |
| `notObserved` | Identité présente avant mais absente après, **et** snapshot après et couverture de la plateforme complets |
| `notComparable` | Périmètre précédent/suivant non prouvé, résultat de scan failed, propriétaire conflictuel ou digest non connu |

`notObserved` signifie **absent de l'inventaire daté dans un périmètre vérifié**, jamais supprimé physiquement. Tout item absent d'un scan `partial` ou `failed`, d'une plateforme `partial` ou `unobserved`, est `notComparable`. On ne déduit aucune suppression d'une absence de résultat. Les observations d'éléments présents sont comparables même si d'autres plateformes ne le sont pas, sauf en cas de scan `failed`. Un changement de propriétaire pour le même `romUnitId` n'est pas une modification anodine : `notComparable`.

**Disponibilité et fraîcheur sont orthogonales** à la qualité de la capture. Un ancien snapshot complet reste consultable quand le support est `OFFLINE` ou `STALE` ; cela ne signifie ni nouveau scan réussi, ni suppression. Le comparateur reporte ces états, mais n'effectue aucune connexion, tentative de scan ou réassociation USB.

## Sortie

`schemaVersion`, `libraryId`, `beforeGenerationId`, `afterGenerationId`, dates, états de capture/disponibilité/fraîcheur, `counts` et `entries`. Chaque entrée contient `romUnitId`, `console`, `classification`, `reason`, `beforeDigest`, `afterDigest` et identités logiques ; tri ordinal par `romUnitId`. Les erreurs sont explicites et bloquantes pour une entrée structurellement invalide ; pas de réparation automatique d'un registre.

## Frontière d'architecture et tests

Ce composant est une **projection en mémoire** destinée à une future vue Bibliothèques (LIB-03/LIB-06), non une nouvelle opération du moteur historique. Les fixtures sont créées en mémoire par `tests/Invoke-CiSnapshotComparison.ps1`. Elles couvrent l'égalité, les changements, les ajouts, l'absence observée seulement si prouvée, les lacunes `partial`/`failed`, les zones inconnues, `OFFLINE`/`STALE`, les conflits et la validation stricte. Le harness rejoint le gate Windows existant ; **l'exécution réelle reste NOT_EXECUTED tant qu'aucun résultat daté au HEAD de la PR n'est disponible**. Aucun test sur collection réelle, et aucune protection CI contournée.

**Coordination Dev 2 :** ce contrat n'impose pas de stockage SQLite, d'API, de technologie UI ou de nouveaux DTO au moteur existant ; toute adoption dans les bibliothèques et snapshots nécessite une revue de l'architecture sur le HEAD courant.
