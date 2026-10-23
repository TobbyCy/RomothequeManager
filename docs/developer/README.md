# Guide de démarrage pour développeurs

**État vérifié au 23 octobre 2026 :** le dépôt public comprend le moteur Windows PowerShell 5.1, le prototype web local et le **POC Windows V2-01A** sous .NET 10 / Avalonia 12.1.4. Le POC est **fonctionnel sur données synthétiques** mais **n'est ni adopté comme architecture définitive ni distribué**. Référence : `main=408d6263be6ae1fbc77c31f6eb9fb530cb4f76e6` et [CI `push main` #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607) **SUCCESS**.

## Environnements et prérequis

| Domaine | Prérequis réel | État / frontière |
| --- | --- | --- |
| Moteur historique et inventaire E1 | **Windows PowerShell 5.1 Desktop** | Sources intégrées ; tests sur fixtures fictives uniquement |
| Tests métier PowerShell | **Pester 4.10.1**, scripts et fixtures de `tests/` | CI-0 à CI-4 applicables et gate final verts sur le SHA cité |
| Prototype web | Navigateur moderne ; Node.js pour tests automatisés | Pages `prototype/ui11/` utilisables hors ligne avec faux jeux ; Edge headless testé en CI |
| **POC Windows V2-01A** | **SDK .NET 10.0.401 exact** pour développer ; Windows `win-x64` pour le smoke natif | `src/RomothequeManager.V2/`, Avalonia 12.1.4, **trois vues et six jeux fictifs** |
| Tests .NET | Python, Git, SDK exact, NuGet et packs de restauration | `tests/dotnet/RomothequeManager.V2.Tests/`, **8 MSTest** ; locks NuGet obligatoires |

`global.json` à la racine impose `10.0.401`, `rollForward=disable` et `allowPrerelease=false`. Un SDK `10.0.402` seul ne convient **pas**. Les deux projets .NET possèdent leur `packages.lock.json` et utilisent `--locked-mode`.

## Vérification .NET locale — Windows

Depuis la racine du dépôt, dans une copie de travail **sans données ROM réelles** :

```powershell
git status --short
dotnet --version
python -B tests/ci_policy.py dotnet
```

- `dotnet --version` doit retourner **exactement `10.0.401`**.
- La dernière commande exécute le garde réellement utilisé par le job CI : vérification de `global.json`, présence des projets/locks, restauration verrouillée et auditée, build Release strict, tests MSTest et contrôle d'un rapport TRX **non vide et sans échec**. Elle nécessite Git, Python, NuGet accessible et les dépendances du projet.
- **Ne pas substituer** `dotnet --version` exécuté hors dépôt à la preuve du pin : le dossier courant influe sur la résolution de `global.json`.

Dans GitHub Actions, le job `.NET / locked build and nonempty tests` examine d'abord les SDK du runner depuis `RUNNER_TEMP`, puis provisionne `10.0.401` avec `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4.3.1). Il exécute ensuite le garde ci-dessus, publie temporairement un dossier self-contained `win-x64`, lance une **vraie fenêtre Avalonia** en mode smoke, et calcule le SHA-256 d'un ZIP candidat.

**Le ZIP n'est pas envoyé à GitHub Actions ni publié en Release.** Les notices et obligations des composants tiers doivent être qualifiées avant toute distribution. Les quatre anciens artefacts téléchargeables ont été [purgés de GitHub](https://github.com/TobbyCy/RomothequeManager/issues/44#issuecomment-6359600451). Le succès du run ne prouve ni installation sur un PC externe sans runtime/SDK, ni le comportement sur un runner initialement dépourvu de SDK 10.0.401 ; cette dernière validation est suivie dans [Issue #43](https://github.com/TobbyCy/RomothequeManager/issues/43).

## Frontières fonctionnelles

- **POC V2-01A intégré :** Dashboard, Collection/recherche, Fiche ; lecture seule en mémoire de six jeux inventés. Aucun accès ROM, BIOS, StateRoot, XML ES-DE, disque externe ou stockage personnel.
- **Prototype web distinct :** Bibliothèques, comparaison A/B et projections synthétiques dans `prototype/ui11/`. Il ne faut **pas** attribuer ces vues au POC Avalonia V2-01A.
- **V2-01B future :** quatrième vue Bibliothèques/snapshots dans Avalonia ; pas encore intégrée.
- **Fonctionnalités ultérieures :** persistance, import de collections, favoris/tags, exports E2–E6, usage SteamOS/ARM et distribution Windows restent non livrés.

## Contribuer et vérifier les preuves

1. Choisir une Issue **Produit** autorisée et lire [CONTRIBUTING](../../CONTRIBUTING.md).
2. Isoler un changement court, inspecter `git diff --check` et les chemins réellement modifiés.
3. Exécuter les contrôles applicables, noter le **SHA exact**, les versions, les tests détectés/exécutés, les canaris et les limites.
4. Ouvrir une PR Draft, obtenir une revue indépendante et la CI requise. **Aucune fusion, Release ou publication d'artefact sans autorisation du propriétaire.**
5. Ne jamais inclure de chemins personnels, données ROM/BIOS réelles, secrets, captures privées ni médias tiers non autorisés.

## Références

[Architecture moteur](../architecture/moteur-actuel.md) · [Contrat moteur](../architecture/contrat-moteur-historique.md) · [POC .NET V2-01A](v2-01a-poc.md) · [ADR](../decisions/README.md) · [Pester](pester-execution.md) · [Couverture](pester-coverage.md) · [Préflight synthétique](preflight-synthetique.md) · [CI adaptative](ci-adaptative.md) · [Workflow Git](workflow.md) · [État produit](../product-status.md).
