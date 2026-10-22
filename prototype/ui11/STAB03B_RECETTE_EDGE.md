# STAB-03B — recette indépendante Node / Edge et compatibilité

**Statut candidat : READY_FOR_AGENT_RETEST**, sans fusion. Version produit : PR Draft #30, au SHA exact communiqué dans le checkpoint final. L'Agent doit repartir de ce SHA puis vérifier que ses résultats concernent le checkout réel. Aucun test sur ROM, BIOS, disque personnel ou StateRoot.

## Cinq suites Node (commande depuis la racine)

```powershell
node .\prototype\ui11\smoke.cjs
node .\prototype\ui11\projection-smoke.cjs
node .\prototype\ui11\integrated-smoke.cjs
node .\prototype\ui11\t2-catalogue.test.cjs
node .\prototype\ui11\t4-catalogue.test.cjs
```

En plus des cinq bilans finaux, contrôler le code de sortie **0** et l'absence d'exception involontaire ; relever version Node et SHA. Simulations V8 effectuées par DEV 2 au fil du mandat : 9 + 10 + 42 + 20 + 32 = **113 assertions PASS** ; elles ne sont pas une preuve d'exécution Node native. Vérifier en particulier les mutations de compteurs connus/inconnus et le vrai `discNumber` multidisque.

## Recette Edge 3 modes / interface partagée

Tester avec la version Edge et Windows exactes inscrites dans le compte rendu, d'abord par ouverture locale `file://`, sans serveur réseau. Pour chaque mode, ouvrir DevTools Console avant les interactions, noter toute erreur JS et photographier seulement des données **fictives**.

| Mode | Entrée existante (aucune nouvelle application indépendante) | Contrôles minimaux |
| --- | --- | --- |
| Prototype standard | `prototype/ui11/index.html` | Dashboard → Bibliothèques → Comparaison A/B → Collection → Fiche → retour. Jeu T1 manuel. |
| Projection intégrée | `prototype/ui11/demo-integree.html` | Collection accessible **sans** `projectedCollection is not defined`, jeu synthétique inconnu, Fiche avec provenance ; comparaison précalculée six classes ; pas de recalcul métier. |
| Catalogue 0.2 candidat | `prototype/ui11/demo-t4-catalogue.html` | Collection/Fiche, identité composée, `snap-ref` distinct de `gen-ref`, deux unités/disques, OFFLINE et STALE, qualité/compteur/couverture. Même `app.js` et consommateur partagé. |

Sur chaque mode, suivre la navigation Dashboard, Bibliothèques, Comparaison, Collection et Fiche ; filtrer plateformes, bibliothèque, qualité, couverture ; saisir et effacer recherche ; changer l'ordre ; ouvrir/fermer la fiche ; utiliser Tab/Shift+Tab, Entrée/Espace sur les contrôles pertinents et retour. Vérifier le focus, le texte d'états `OFFLINE / STALE / UNKNOWN` et la différence entre compteur inconnu et zéro. Vérifier le refus d'un A/B sans paire de comparaison précalculée, sans comparaison JS implicite. La démo T4 n'invente pas de comparaison A/B.

## Véritable JSON émis par Dev 1 — Unicode de bout en bout

Sur un **checkout séparé** du producteur Dev 1 PR #29 et sous Windows PowerShell 5.1, générer le JSON **synthétique** avec le script réellement versionné, sans substituer manuellement les chaînes :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Export-SyntheticCanonicalCatalogue.ps1 -OutputJson "$env:TEMP\rm-stab03b-synthetic.json"
```

Reporter SHA du checkout producteur, SHA-256 du fichier émis, mode d'encodage et succès des validations PS du producteur. Sur le checkout de la PR #30, utiliser le **même fichier brut**, sans modification :

```powershell
node .\prototype\ui11\unicode-producer.test.cjs "$env:TEMP\rm-stab03b-synthetic.json"
```

Le test refuse tout encodage UTF-8 invalide ou caractère U+FFFD, exige exactement `Étoile des disques` (U+00C9), cherche `Étoile`, vérifie identité composée et provenance sans mutation et rend Collection/Fiche en DOM simulé. Une erreur `UNICODE_PRODUCER_TITLE_MISSING` se répare **chez le producteur**, jamais par une substitution arbitraire dans l'UI.

Dans Edge, ouvrir la **Collection existante** d'un des trois modes, utiliser « Charger un JSON UTF-8 issu de l’exporteur synthétique PS5.1 » pour sélectionner ce fichier synthétique. Le navigateur lit uniquement ce fichier choisi localement : aucun accès réseau ni copie ROM. Vérifier `Étoile des disques`, recherche `Étoile`, provenance, snapshot/génération et variantes/multidisques ; recharger la page pour revenir aux fixtures. Le champ est une voie de recette synthétique, pas une ingestion de bibliothèques réelles. Une vue importée sans comparaisons précalculées **refuse** le calcul A/B.

## Contrat et garde-fous PS → DTO → JS → UI

| Étape | Responsable | Invariant / preuve requise | État DEV 2 |
| --- | --- | --- | --- |
| `Export-SyntheticCanonicalCatalogue.ps1` | Dev 1 | Octets JSON UTF-8, U+00C9, identités, provenance, 3 observations / 2 variantes | Runtime PS5.1 **NOT_EXECUTED** ici |
| `CatalogueProjection.psm1` et `CatalogueDtoContract.psm1` | Dev 1 | DTO 0.2 candidat, validateur indépendant et erreurs négatives | Source consultée ; runtime **NOT_EXECUTED** |
| `adapters/shared/catalogue-t3-consumer.js` | Dev 1 (copie SHA identique sur PR #30) | Contrat candidat, identité composée, scopes, références, variantes, disque, Unicode | Source synchronisée, tests V8 positifs/négatifs |
| `t2-catalogue-model.js` | Dev 2 | Validation complémentaire, projection des champs pour la fiche, compteur inconnu ≠ zéro | Test V8 ; Node natif non exécuté |
| `app.js` / 3 entrées existantes | Dev 2 | Navigation, rendu commun, comparaisons précalculées, recherche et import local du JSON synthétique | Test V8 / DOM simulé ; Edge non exécuté |

## Travail restant et critères de réception

L'Agent doit rapporter **PASS/FAIL/BLOCKED** pour chaque suite Node, chaque mode Edge, l'export PS5.1 brut, l'Unicode et les négatifs ; conserver la sortie console ainsi que SHA/date/OS/runtime. Toute divergence du consommateur de Dev 1 doit être synchronisée à l'identique, sans adopter seul le contrat 0.2. DEV 2 ne modifie pas le routage CI : Dev 1 peut raccorder les cinq chemins ci-dessus et le test Unicode facultatif, après validation de son propre contrôle CI.

**Aucune intégration finale ni passage PUBLIC** ne peut être inféré d'un succès V8 ou d'une capture navigateur. Demander une revue indépendante sur la PR #30.
