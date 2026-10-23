# V2-01A — POC Windows synthétique (Issue #41)

**Statut : candidat technique, non adopté, non distribué.** Un GO limité autorise
uniquement la démonstration Windows .NET 10 + Avalonia UI Community 12.1.4.

Trois vues : Dashboard, Collection, Fiche. Six jeux **inventés** et des
bibliothèques fictives OFFLINE/UNKNOWN/ONLINE avec provenance et horodatage.
L'absence de source n'est jamais interprétée comme une suppression de ROM.
Ni stockage ni lecture de ROM, BIOS, StateRoot, XML, réseau ou disque externe.

Le projet UI `src/RomothequeManager.V2` est distinct du moteur historique PS5.1.
Les fixtures sont codées en lecture seule et volontairement non représentatives
de la couverture d'une véritable collection.

Le projet de tests `tests/dotnet/RomothequeManager.V2.Tests` utilise MSTest.
Les deux `packages.lock.json` ont été **générés par NuGet réel sur
`windows-2022`**, puis committés. Le job .NET effectue un restore strict
`--locked-mode`, audit NuGet, build avec warnings-as-errors et un TRX
avec au moins un test exécuté. La [CI #39904964788](https://github.com/TobbyCy/RomothequeManager/actions/runs/39904964788)
sur le commit `36d6b8be34092cbadae1430ab27ea94a272ee1fc`
a démontré **8 tests PASS, 0 FAIL et 0 SKIPPED** et le lancement
réel d'une fenêtre Avalonia qui instancie les **trois vues**.
Ce n'est pas une preuve transférable à d'autres commits.

Packaging **réellement testé sur runner Windows avec SDK**, sur le SHA
cité ci-dessus : restore explicite verrouillé `-r win-x64
-p:SelfContained=true`, puis `dotnet publish -c Release -r win-x64
--self-contained true --no-restore -p:PublishSingleFile=false
-p:PublishTrimmed=false`. Validation de `RomothequeManager.V2.exe`,
`coreclr.dll`, `hostfxr.dll` et de la fenêtre native. Archive de dossier
complet avec `LICENSE` et avertissement synthétique, **sans ROM**.

Preuve [Actions #39904964788](https://github.com/TobbyCy/RomothequeManager/actions/runs/39904964788) :
`POC_WINDOW_OPENED_PASS views=Dashboard,Collection,GameDetail`,
`POC_ZIP_CANDIDATE_PASS`, **75 238 381 octets**, SHA-256 du
fichier ZIP `e4ded8331c7d5783029884e3283e2312b267ded0ea4857e19dd6925023ee5a97`.
Artefact éphémère GitHub Actions `v2-01a-win-x64-candidate`,
rétention **7 jours**, incluant `.zip` et `.zip.sha256`.
Le SHA-256 de l'archive interne est différent du hash de transport de
l'artefact GitHub (une seconde compression). **Aucune Release.**

**Limites** : essai hors ligne sur PC externe sans runtime/SDK installé :
`NOT_TESTED`. Test USB réel, lettre changeante, droits en lecture seule,
Windows ARM, Linux/SteamOS, ergonomie et accessibilité humaine :
`NOT_TESTED`. La recette Windows CI possède déjà .NET, et ne démontre
donc pas à elle seule l'absence de prérequis système sur ordinateur tiers.
L'inventaire et les licences des dépendances NuGet/natives doivent faire
l'objet d'une revue juridique distincte avant distribution publique.
Aucune adoption définitive de la stack.


## Réserves de revue — SDK et notices (23.10.2026)

Le SDK est **exactement** `10.0.401`, `global.json` à la racine avec
`rollForward=disable` et `allowPrerelease=false` : le contrôle Python
`verify_sdk_pin` et la commande `dotnet --version` dans le job Windows
échouent en cas d'écart. La règle de chemins CI-0 et des canaris négatifs
autorisent exclusivement `global.json` à cette casse, sans suffixe.

**Audit de l'artefact existant** [GitHub Actions #39905309039](https://github.com/TobbyCy/RomothequeManager/actions/runs/39905309039),
artefact ID `12481030630` : archive externe contenant ZIP (SHA-256
`71c81b07cd1a4a98631a0075b505a709876514e6b9f3f4b2c68b6c9122293cac`).
Inspection du ZIP **effectuée réellement** : 225 entrées incluant DLL
Avalonia, `libSkiaSharp.dll`, `libHarfBuzzSharp.dll`, les DLL du runtime .NET
et `av_libglesv2.dll`. L'archive ne contient pour notices que `LICENSE`
(licence du produit) et `POC-NOTICE.txt` (générique). **Aucun texte de
licence tiers ni inventaire d'attribution vérifié embarqué**.

Composants concernés : Avalonia `12.1.4` (MIT déclaré par NuGet), SkiaSharp
`3.119.4`, HarfBuzzSharp `8.3.1.3`, ANGLE
`Avalonia.Angle.Windows.Natives 2.1.27548.20260419`, MicroCom, Tmds.DBus
et pack runtime Microsoft .NET `10.0.12`. Ces mentions ne valent **pas**
vérification du texte des licences ni de toutes les obligations redistributives.

**Décision de sécurité** : les nouvelles archives sont toujours construites et
SHA-256 calculé en CI mais demeurent **quarantainées dans RUNNER_TEMP et ne
sont plus téléchargables**. L'action `upload-artifact` est supprimée et
interdite par `verify_public_ci_guard` avec canaris négatifs. L'artefact
**historique** reste potentiellement téléchargeable jusqu'au **30.10.2026
05:23 UTC** : **suppression manuelle prioritaire par le Contrôleur**
(GitHub Actions, run #39905309039, artefact
`v2-01a-win-x64-candidate`; vérifier aussi les runs précédents).
L'absence de nouvel upload ne retire pas rétroactivement ce fichier.

Avant toute nouvelle autorisation de téléchargement : inventaire complet des
composants binaires, versions, textes intégrals officiels de licences,
obligations d'attribution éventuelle (Skia, HarfBuzz, ANGLE, runtime .NET),
intégration des notices et contre-audit du ZIP **réel**, validation explicite
du propriétaire et nouvelle PR de restauration du gate d'artefact.

Ne pas utiliser ce code sur une bibliothèque réelle ou en production.
