# V2-01C — préparation du ZIP Windows x64 (non distribué)

**Mandat :** [Issue Produit #50](https://github.com/TobbyCy/RomothequeManager/issues/50). **Décision :** GO technique packaging uniquement ; **HOLD publication**. Le code de V2-01A/B, les quatre vues et les données fictives restent inchangés.

## Résultat attendu en CI

Sur `windows-2022`, avec SDK .NET `10.0.401` exact et NuGet en mode verrouillé :

1. `dotnet publish` produit une application `win-x64` **self-contained**, non trimée et non single-file, contenant notamment `RomothequeManager.V2.exe`, `coreclr.dll` et `hostfxr.dll`.
2. La CI ajoute `LICENSE`, `POC-NOTICE.txt` et `POC-MANIFEST.json` (six jeux fictifs, trois bibliothèques fictives, quatre vues, source du run et verrou `HOLD_OWNER_GO`).
3. Elle crée `RomothequeManager-V2-01C-win-x64.zip` et `RomothequeManager-V2-01C-win-x64.zip.sha256` **dans `RUNNER_TEMP` uniquement**.
4. Elle recalcule le SHA-256, extrait le ZIP dans **un dossier différent**, vérifie les fichiers essentiels, l'identité de la liste de fichiers et l'absence d'extensions ROM/BIOS/sauvegardes/clefs connues.
5. Elle lance l'exécutable **extrait** avec `--smoke-window`, sans compter sur un runtime .NET global, et exige `POC_WINDOW_OPENED_PASS views=Dashboard,Collection,GameDetail,Libraries fixtures=6 synthetic=1`.
6. Elle émet `V2C_ZIP_VERIFIED_PASS` avec empreinte, taille, nombre de fichiers et source du run, puis `POC_ZIP_QUARANTINED_NOT_UPLOADED`.

Ces preuves valident une **préparation reproductible et un smoke automatisé dans le runner CI**. Elles ne démontrent **pas** le démarrage interactif d'un poste utilisateur réel.

## Frontière de distribution

- Aucun `actions/upload-artifact`, aucune Release/Pre-release, aucun lien de téléchargement, aucune copie hors de la quarantaine.
- Le ZIP et son `.sha256` disparaissent avec le runner ; **ils ne sont pas téléchargeables par l'utilisateur**.
- `POC-MANIFEST.json.sourceCommit` identifie le `GITHUB_SHA` du run. Sur une PR, il peut s'agir du commit de fusion temporaire évalué par GitHub Actions ; la branche et son HEAD doivent être relevés séparément.
- Les licences/mentions tierces doivent être revues avant une distribution. L'application n'est pas signée ; SmartScreen peut avertir. Aucun de ces éléments n'autorise à publier.
- Une décision explicite ultérieure du propriétaire est nécessaire avant tout upload ou Pre-release. Le Contrôleur décide seul des merges.

## Matrice de réception

| ID | Contrôle | Preuve attendue | Limite |
| --- | --- | --- | --- |
| ZIP-01 | SDK exact, restore verrouillé, tests .NET | Job `ci-dotnet` vert | Même SHA uniquement |
| ZIP-02 | Exécutable et runtime autonomes | `coreclr.dll`, `hostfxr.dll` présents dans ZIP extrait | Smoke automatisé |
| ZIP-03 | Notice, licence, manifeste | Présence et validation CI | Notice tierce complète à revoir |
| ZIP-04 | ZIP et empreinte SHA-256 | `V2C_ZIP_VERIFIED_PASS` | Artefacts temporaires |
| ZIP-05 | Contenu exclusivement POC fictif | Contrôle d'extensions + manifeste | Pas un audit exhaustif de tous les formats |
| ZIP-06 | Lancement depuis extraction | `POC_WINDOW_OPENED_PASS` sur l'exécutable extrait | Pas de parcours interactif |
| ZIP-07 | Aucune publication | `POC_ZIP_QUARANTINED_NOT_UPLOADED` + `ci-policy-gate` | GO propriétaire futur |
| UXZIP-01–12 | Quatre vues, navigation souris/clavier, focus, scrolling, redimensionnement, DPI | **NOT_TESTED** jusqu'à essai externe Windows propre, selon [Issue #50](https://github.com/TobbyCy/RomothequeManager/issues/50) | Ne pas déduire PASS du smoke |

## Essai externe ultérieur — après autorisation de mise à disposition

Sur une VM ou un Windows x64 propre, sans SDK/runtime .NET préinstallé si possible :

1. Récupérer **uniquement le ZIP explicitement autorisé** et son `.sha256`, puis comparer `Get-FileHash -Algorithm SHA256` à l'empreinte annoncée.
2. Extraire dans un nouveau dossier utilisateur, hors du dépôt. Vérifier `LICENSE`, `POC-NOTICE.txt`, `POC-MANIFEST.json`.
3. Démarrer `RomothequeManager.V2.exe` sans élévation ni installation. Contrôler absence d'accès à une ROM, BIOS ou bibliothèque réelle.
4. Vérifier les quatre vues, les six jeux fictifs, les trois bibliothèques fictives, les états OFFLINE/STALE et la comparaison A/B sur leurs fixtures.
5. Effectuer les vérifications UXZIP-01–12 de DEV 3 : navigation souris/clavier/Tab, focus visible, ComboBox, défilement, redimensionnement et DPI. Noter chaque PASS/FAIL ; **aucun PASS présumé avant exécution**.
6. Consigner OS/build, architecture, SHA du ZIP, résultat SmartScreen et éventuelles limites.

## Revues avant décision

- **DEV 2** : cohérence documentaire, identité visuelle/BRAND-01, licence et mention des limites.
- **Agent** : indépendance de l'extraction, fichiers attendus/interdits, SHA-256, absence d'upload, adéquation des preuves CI.
- **Contrôleur** : vérifie HEAD, CI, verdicts et décide du merge éventuel. **Pas de merge autonome.**

## DIST-ZIP-01 — distribution de test contrôlée (préparation, aucun upload à ce stade)

Décision [Issue #50](https://github.com/TobbyCy/RomothequeManager/issues/50) du 23.10.2026 : **GO_PREPARE_TEST_DISTRIBUTION**, mais **RELEASE_HOLD_UNTIL_NOTICES_AND_FINAL_ARTIFACT_VERIFIED**. La fusion du packaging V2-01C [PR #53](https://github.com/TobbyCy/RomothequeManager/pull/53) n'a livré aucun ZIP téléchargeable.

Le workflow distinct `.github/workflows/dist-zip.yml` est **exclusivement manuel** (`workflow_dispatch`, sélection de `main`) :

1. **`phase=validate`** : impose un SHA source exact, le SDK 10.0.401, les locks NuGet, les notices tierces et l'inventaire validés ; construit le ZIP Windows x64 dans `RUNNER_TEMP`, le recompile/emballe de façon ordonnée, vérifie deux ZIP successifs identiques, le SHA-256, chaque fichier après extraction et le smoke Avalonia depuis l'extraction. **Aucun téléversement ; le ZIP disparaît avec le runner.** Les logs livrent l'empreinte et l'identité du commit.
2. **Revue finale** : DEV 2 confirme `docs/legal/THIRD-PARTY-NOTICES.txt` et `docs/legal/THIRD-PARTY-INVENTORY.md` pour les dépendances **réellement embarquées** ; Agent contre-vérifie le HEAD, la CI, la liste de fichiers et le SHA. Le Contrôleur soumet à Tobby le commit, le hash et les limites connues.
3. **`phase=publish`**, uniquement après **GO final explicite** : mêmes sources figées, même validation préalable, puis quatre variables de dépôt contrôlées par le propriétaire doivent concorder : `DIST_ZIP_FINAL_GO=true`, `DIST_ZIP_APPROVED_SOURCE_SHA`, `DIST_ZIP_APPROVED_SHA256`, `DIST_ZIP_APPROVED_TAG`. Les champs manuels `source_sha`, `expected_sha256` et `release_tag` doivent aussi concorder. **Toute différence bloque l'upload.**
4. Après ce dernier contrôle, le workflow crée une **Release brouillon et pré-release** avec le ZIP et `.sha256`, retélécharge les deux assets et compare leur contenu au candidat validé. Il ne publie la **Pre-release** que si les deux fichiers sont intacts. Un échec laisse au maximum un brouillon, jamais une version stable.

Tag de publication attendu : `poc-v2-01c-YYYYMMDD-<12 premiers caractères du SHA source>`. Le ZIP contient `POC-NOTICE.txt`, `POC-MANIFEST.json`, `LICENSE`, `LEGAL.md`, `THIRD-PARTY-NOTICES.txt` et `THIRD-PARTY-INVENTORY.md`. Les notices ou l'inventaire manquants provoquent un **FAIL**, sans substitution par une notice fictive.

### Limites conservées

- Le succès d'une PR et même d'un run `validate` **ne fournit aucun téléchargement**. Le hash d'un run doit correspondre exactement au hash approuvé ; toute variation exige une nouvelle revue/approbation.
- La répétition de l'archive ZIP dans un même run prouve un emballage déterministe de fichiers identiques, **pas** la reproductibilité complète de `dotnet publish` entre deux machines/runs.
- Une pré-release de test reste publique dès sa publication finale ; le GO final doit donc être conscient de cette visibilité. Aucun canal de distribution ne doit contenir de ROM, BIOS, clés, secrets ni données personnelles.
- La recette interactive Windows `UXZIP-01–12`, dont clavier/focus/scroll/resize, reste **NOT_TESTED** avant essai externe du ZIP téléchargé exact. L'Agent consigne la vérification binaire ; DEV 3/Tobby consigne l'UX.
- Aucune Release stable, installateur, signature, scan réel ou extension de périmètre n'est compris dans DIST-ZIP-01.
