# Guide de démarrage pour développeurs

**État au 23 octobre 2026 :** le dépôt public comprend le moteur Windows PowerShell 5.1, le prototype web local et le **POC Windows V2-01A/B** sous .NET 10 / Avalonia 12.1.4, avec quatre vues sur six jeux fictifs et trois bibliothèques fictives. V2-01B est intégrée via [PR #48](https://github.com/TobbyCy/RomothequeManager/pull/48) ; **V2-01C prépare uniquement le packaging**, sans autorisation de distribution.

## Environnements et prérequis

| Domaine | Prérequis réel | État / frontière |
| --- | --- | --- |
| Moteur historique et inventaire E1 | **Windows PowerShell 5.1 Desktop** | Sources intégrées ; tests sur fixtures fictives uniquement |
| Tests métier PowerShell | **Pester 4.10.1**, scripts et fixtures de `tests/` | CI-0 à CI-4 applicables et gate final verts sur le SHA cité |
| Prototype web | Navigateur moderne ; Node.js pour tests automatisés | Pages `prototype/ui11/` utilisables hors ligne avec faux jeux ; Edge headless testé en CI |
| **POC Windows V2-01A/B** | **SDK .NET 10.0.401 exact** pour développer ; Windows `win-x64` pour le smoke natif | `src/RomothequeManager.V2/`, Avalonia 12.1.4, **quatre vues, six jeux fictifs et trois bibliothèques fictives** |
| Tests .NET | Python, Git, SDK exact, NuGet et packs de restauration | `tests/dotnet/RomothequeManager.V2.Tests/`, **31 MSTest** ; locks NuGet obligatoires |

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

Dans GitHub Actions, le job `.NET / locked build and nonempty tests` examine d'abord les SDK du runner depuis `RUNNER_TEMP`, puis provisionne `10.0.401` avec `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4.3.1). Il exécute le garde ci-dessus, puis produit dans `RUNNER_TEMP` un **ZIP V2-01C autonome `win-x64` et son fichier SHA-256**. La CI extrait ce ZIP dans un autre dossier, vérifie le manifeste et les fichiers requis, refuse des extensions ROM/BIOS sensibles et lance la vraie fenêtre Avalonia depuis les fichiers extraits avec les **quatre vues synthétiques**.

**Le ZIP et son SHA-256 restent en quarantaine sur le runner : aucun artefact téléchargeable, Pre-release ou Release.** Les licences/notices des composants tiers et l'essai sur un Windows x64 externe sans runtime préinstallé restent à valider avant toute publication. Le smoke extrait sur le runner équipé du SDK ne remplace pas cet essai. Voir le [guide packaging V2-01C](v2-01c-packaging.md), les anciens artefacts [purgés](https://github.com/TobbyCy/RomothequeManager/issues/44#issuecomment-6359600451) et le suivi SDK [#43](https://github.com/TobbyCy/RomothequeManager/issues/43).

## Frontières fonctionnelles

- **POC V2-01A/B intégré :** Dashboard, Collection/recherche, Fiche et Bibliothèques/snapshots avec A/B ; six jeux fictifs et trois bibliothèques fictives, lecture seule. Aucun accès ROM, BIOS, StateRoot, XML ES-DE, disque externe ou stockage personnel.
- **Prototype web distinct :** l'interface `prototype/ui11/` conserve ses propres projections synthétiques ; ne pas lui attribuer les résultats précalculés de la fixture Avalonia.
- **V2-01C en préparation :** ZIP autonome, manifeste, SHA-256 et smoke depuis extraction, toujours non distribué. Clavier/focus/scroll/ComboBox interactifs et essai Windows externe restent `NOT_TESTED` tant que les critères de l'Issue #50 ne sont pas exécutés.
- **Fonctionnalités ultérieures :** persistance, import de collections, favoris/tags, exports E2–E6, usage SteamOS/ARM et distribution Windows restent non livrés.

## Contribuer et vérifier les preuves

1. Choisir une Issue **Produit** autorisée et lire [CONTRIBUTING](../../CONTRIBUTING.md).
2. Isoler un changement court, inspecter `git diff --check` et les chemins réellement modifiés.
3. Exécuter les contrôles applicables, noter le **SHA exact**, les versions, les tests détectés/exécutés, les canaris et les limites.
4. Ouvrir une PR Draft, obtenir une revue indépendante et la CI requise. **Aucune fusion, Release ou publication d'artefact sans autorisation du propriétaire.**
5. Ne jamais inclure de chemins personnels, données ROM/BIOS réelles, secrets, captures privées ni médias tiers non autorisés.

## Références

[Architecture moteur](../architecture/moteur-actuel.md) · [Contrat moteur](../architecture/contrat-moteur-historique.md) · [POC .NET V2-01A](v2-01a-poc.md) · [Packaging V2-01C](v2-01c-packaging.md) · [ADR](../decisions/README.md) · [Pester](pester-execution.md) · [Couverture](pester-coverage.md) · [Préflight synthétique](preflight-synthetique.md) · [CI adaptative](ci-adaptative.md) · [Workflow Git](workflow.md) · [État produit](../product-status.md).
