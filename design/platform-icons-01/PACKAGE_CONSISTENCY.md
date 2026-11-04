# GRAPHIC164_PACKAGE_CONSISTENCY_READY — Réconciliation physique et documentaire

**ZIP candidat versionné :** `design/platform-icons-01/PLATFORM_ICONS_GBA_PS2_CANDIDATE.zip`. Nouveau **blob Git SHA-1** : `d66bc2a9ed3e773490b9c4fc20704f94e7248f59` (empreinte d'objet Git, **pas** un SHA-256). L'archive a été reconstruite depuis les données de l'ancien ZIP stocké (blob `c0cb5c18465ed8d6b0219f0144945c2326e74fa2`) avec les quatre SVG fallback historiques et la synchronisation de cinq documents. Les 84 PNG et les 26 SVG source/planches **restent byte-identiques** à l'ancien paquet.

## Bilan contrôlé

| Propriété | Ancien ZIP | ZIP réconcilié |
|---|---:|---:|
| Entrées | 115 | 119 |
| PNG GBA/PS2 | 84 | 84 (identiques) |
| SVG GBA/PS2 | 24 | 24 (identiques) |
| Planches comparatives SVG | 2 | 2 (identiques) |
| SVG fallback neutre | 0 | 4 (ajoutés, copiés depuis les sources inchangées du dépôt) |
| Documents ZIP | 5 | 5 (alignés avec le dépôt) |

**Validation effectuée au moment de la reconstruction :** 119 entrées ZIP stockées, absence de doublon, noms relatifs sûrs, CRC local de chaque entrée, cohérence répertoire central/en-têtes, 84/84 PNG avec signature, dimensions concordantes au manifeste, palette 8-bit et alpha `tRNS` (index zéro), CRC de tous les chunks PNG et fin `IEND` sans traîne. Comparaison binaire sur les 84 PNG et 26 SVG originaux : **aucune modification**. Les cinq fichiers internes `qa-gba-ps2.json`, `pack-manifest-gba-ps2.json`, `README-GBA-PS2.md`, `PROVENANCE-GBA-PS2.md`, `licenses/DejaVu-LICENSE-upstream.txt` ont été égalés au contenu versionné de cette PR.

**Manifestes :** 12 assets candidat, 3 styles, Dark/Light, 7 tailles exportées, 84 chemins PNG. `platformKey=null` sur les 12 assets et les 4 fallback ; `platformMappings=[]`, `runtimeContract=false`. Le fallback figure désormais dans l'archive **en SVG seulement**. Un runtime n'acceptant que PNG devra disposer de PNG fallback produits et validés sous mandat distinct.

**CI / QA :** l'archive et le QA du dépôt consignent `CI_FAIL / PATH_POLICY_BLOCK` constaté sur l'ancien HEAD `ffc112e7…` et le blocage secondaire ZIP binaire. La CI du nouveau HEAD n'était **pas exécutée** au moment du paquetage, et sa valeur pourra différer : vérifier après push et publier un addendum GitHub. Aucune recette Windows/Avalonia réussie, aucun droit Nintendo/Sony ni licence DejaVu historique certifiés. La reconstruction **ne résout pas** la politique CI.

**Limites de cette preuve :** contrôles de conteneur, métadonnées et byte-identité, pas validation graphique Windows, pas exécution sous consommateur Avalonia, pas audit juridique ni certification de protection against zip bombs par la CI. Nouvelle vérification et approbation DEV 2 au HEAD exact requises avant intégration.

`GRAPHIC164_PACKAGE_CONSISTENCY_READY / RIGHTS_AND_INFRA_HOLD / DRAFT_ONLY`.
