# T4-B — Matrice de convergence Produit #29 → UI #30

**État : candidat non normatif / PR Draft / HOLD_MERGE.** Référence source Dev 1 Produit #29 : `e38736775f9ba7b8a83ec178575ad1644e8b40a8`, base #27 `355ea45bc72190a61e43b2570982dcfaa716d982`. Référence initiale Dev 2 #30 : `b46dd5d846bd1a5483e601d6db859970850a331e`, base #28 `e3fc250deac26c10cbd4810980fb8f9fbbbea817`. Les versions et SHA ci-dessus sont datés du 22.10.2026 : relire GitHub au moment de la recette.

| Étape | Source | Cible / situation T4-B | Verdict |
| --- | --- | --- | --- |
| Producteur PowerShell | `Modules/CatalogueProjection.psm1` (#29) | `ConvertTo-RmCanonicalCatalogueDto` | Code présent sur #29 ; PS5.1 NOT_EXECUTED |
| Contrat DTO | `Modules/CatalogueDtoContract.psm1` (#29), `0.2.0-ui-catalogue-candidate` | `registry + games + diagnostics + countKnown + gameCount` | Candidat, non adopté |
| Validateur PowerShell | `Assert-RmCanonicalCatalogueDto` (#29) | Validation indépendante du producteur | Code présent ; PS5.1 NOT_EXECUTED |
| Échantillon partagé | `tests/fixtures/CatalogueT3CanonicalSample.json` (#29) | Copie explicitement étiquetée dans `t4-catalogue-demo.js` (#30) | Fichier de démonstration statique, non export runtime |
| Consommateur T3 | `adapters/shared/catalogue-t3-consumer.js` (#29) | Même chemin canonique dans #30 ; blob identique à Dev 1 (aucune deuxième implémentation) | Contrat candidat, synchronisation à maintenir |
| Contrôle du registre | `registry 0.1.0-synthetic` | `projection-adapter.js` (#30) | Corrige l'index accessible avec `countKnown=false` sous couverture partielle |
| Modèle de présentation | `t2-catalogue-model.js` (#30) | Transforme explicitement observations → unités UI, variantes objet → IDs d'affichage, provenance tableau → référence d'affichage | Compatible T3 candidate, préserve le consommateur T1 pour tests anciens |
| Vue principale | `index.html` + `app.js` (#30) | Collection/Fiche, recherche, filtres, tri, diagnostics, OFFLINE/STALE, couverture, disque et liens snapshot | V8 simulé ciblé PASS ; navigateur NOT_EXECUTED |

## Règles de cohérence

- Le DTO T3 est distingué par son **`schemaVersion`**. Aucune conversion automatique d'un DTO incompatible ou d'un état `null` en zéro.
- La clé jeu est `JSON.stringify([libraryId,platform,gameId])`, sensible à la casse. `romUnitId` et `variantId` ne fusionnent pas les jeux.
- Le consommateur JS T3 canonique partagé avec #29 valide schéma, disponibilité et fraîcheur par rapport au registre, provenance snapshot/génération, doublons, couvertures, compteurs, numéros de disque, variantes et preuves de titres inconnus ou contradictoires. Le modèle UI **ne revalide plus ces invariants métier** : il projette les données déjà acceptées, utilise les cartes de bibliothèque de présentation et vérifie les références de diagnostics utiles à l'affichage. Aucune authentification ni preuve physique n'est revendiquée.
- Le `countKnown=false` d'une génération partielle reste possible avec index accessible ; `gameCount=null` pour un total global non prouvé. OFFLINE ne signifie pas index indisponible, STALE ne signifie pas observation actuelle.
- Le registre et la comparaison demeurent des entrées **précalculées** du moteur. Aucune duplication du comparateur PowerShell n'est introduite par T4-B.
- La fiche affiche les informations prouvées par le DTO : `title=null` reste inconnu, les observations n'inventent ni chemin physique ni média.
- `demo-t4-catalogue.html` est une **seconde entrée de données dans la même application**, pas un catalogue concurrent. Un seul shell, un seul `app.js`, un seul parcours Collection/Fiche. La démonstration T3 a une génération unique et **ne prétend pas fournir une comparaison A/B**.

## Séquence de recette — sans ROM réelle

Depuis un checkout autorisé de #30, sur des fixtures fictives :

```powershell
node prototype/ui11/smoke.cjs
node prototype/ui11/projection-smoke.cjs
node prototype/ui11/integrated-smoke.cjs
node prototype/ui11/t2-catalogue.test.cjs
node prototype/ui11/t4-catalogue.test.cjs
```

Depuis un checkout autorisé de #29, dans **Windows PowerShell 5.1** :

```powershell
powershell.exe -NoProfile -File .\tests\Invoke-CiCatalogueProjection.ps1
powershell.exe -NoProfile -File .\tests\Invoke-CiCanonicalCatalogue.ps1
powershell.exe -NoProfile -File .\tests\Export-SyntheticCanonicalCatalogue.ps1 -OutputJson "$env:TEMP\rm-t4-contract.json"
```

Ensuite contrôler manuellement que le JSON **réellement exporté** valide les deux consommateurs sur les SHA exacts, y compris un cas complet, multidisque, partiel, OFFLINE/STALE, inaccessible, identité ambiguë, titre nul et preuve falsifiée. Conserver code de sortie, stdout/stderr, SHA de l'export et de chaque branche.

Navigateur : ouvrir `prototype/ui11/demo-t4-catalogue.html` en `file://`, vérifier Dashboard → Bibliothèques → Collection → Fiche, filtres, clavier, défilement, zoom 200 %, largeurs 320/375/768/1280, retour et états d'erreur. Comparaison A/B à tester avec la fixture intégrée existante ou un DTO comparateur précalculé, **pas** avec la fixture T3 mono-snapshot.

**Preuves au moment de l'écriture :** V8 pur 14/14, V8 UI DOM simulé 9/9. **Node, PowerShell 5.1, navigateur réel, CI, parité JSON runtime : NOT_EXECUTED** au checkpoint d'intégration initial. Les commandes précédentes sont un plan de recette, pas une affirmation d'exécution.

## Points pour le Contrôleur et Dev 1

1. Vérifier à chaque avancée de Dev 1 que `adapters/shared/catalogue-t3-consumer.js` est strictement identique aux deux HEAD ; ne jamais conserver deux validateurs JS divergents.
2. Les contrôles `availability/freshness`, `discNumber` et titres contradictoires/absents ont été regroupés dans le consommateur JS partagé ; maintenir la vérification **d'équivalence PS ↔ JS sur l'export synthétique réellement produit** et ses falsifications, toujours NOT_EXECUTED en runtime ici. Ne pas déduire une équivalence PowerShell de cette inspection de code.
3. Régler le chaînage des PR empilées et gates CI sans rebase ou merge implicites.
4. Exiger export PS5.1 et tests navigateur réels avant verdict de parité/recette.


## Addendum STAB-03B — correction des régressions

L'ancienne référence à une copie locale du consommateur T3 est historique et **n'est plus valable**. Les trois entrées HTML chargent désormais le chemin canonique partagé dans l'arbre de PR #30, à contenu strictement égal à la branche Dev 1 contrôlée. La Collection/Fiche est commune pour les DTO legacy, T1 et T3 ; le JSON de l'exporteur synthétique PS5.1 peut être importé depuis la Collection, après validation intégrale et sans créer de nouvelle page. Les comparaisons du DTO d'import ne sont pas reconstruites en JS.

[Recette STAB-03B](STAB03B_RECETTE_EDGE.md) : suites 9 + 10 + 42 + 20 + 34 = 115 assertions PASS **sous V8 simulant Node** ; Le parcours supplémentaire `ui-journey.test.cjs` comporte 42 vérifications V8 simulées sur les trois modes ; `synthetic-import.test.cjs` est une suite distincte, non rejouée pendant INT-03. Node natif, Edge, parité Unicode de l'export PS5.1 et gates CI restent NOT_EXECUTED. Aucun élément de ce document n'adopte contractuellement la version 0.2 ni ne prouve un accès à des ROM.
