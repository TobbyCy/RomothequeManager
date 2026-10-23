# LIC-ZIP-01 — inventaire juridique ciblé du ZIP Windows

Source figée : `src/RomothequeManager.V2/packages.lock.json` du `main` après PR #53 (commit `31579350033c22cd3c55e156d29a4da7f18ad822`). Cible : `net10.0 / win-x64 / self-contained`. Mandat : Issue #50.

## Portée et méthode

La restauration NuGet ne prouve **pas** que les fichiers sont publiés. Les 25 paquets ci-dessous sont les dépendances résolues de `net10.0`; une entrée du bloc RID `net10.0/win-x64` n'est **pas** non plus une preuve de contenu ZIP. Le manifeste **du publish candidat** fourni par DEV 1 sur [PR #55 / Adaptive CI #174](https://github.com/TobbyCy/RomothequeManager/actions/runs/39958695887) contient **223 fichiers** (`PUBLISH_FILE_JSON`, merge SHA `053100390487f7bc0dee653f594bc5cb3cff9442`) et permet la confrontation de la colonne finale. Ce n'est **pas** encore le manifeste du ZIP final : toute modification de dépendances, suppression ou ajout impose un nouveau rapprochement. Les composants natifs statiquement intégrés demandent aussi des avis spécifiques.

| Paquet NuGet | Version verrouillée | Licence / famille | Notice source candidate pour le ZIP | Présence prouvée dans le publish candidat de #55 |
|---|---|---|---|---|
| `Avalonia` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.dll`, `Avalonia.Base.dll`, `Avalonia.Controls.dll` (entre autres) |
| `Avalonia.Angle.Windows.Natives` | `2.1.27548.20260419` | ANGLE BSD-style | licenses/ANGLE-LICENSE.txt | `PRESENT_CANDIDAT` : `av_libglesv2.dll` |
| `Avalonia.BuildServices` | `11.3.2` | À relever du paquet; build uniquement attendu | — si absent | `ABSENT_CANDIDAT` : aucun binaire `Avalonia.BuildServices` |
| `Avalonia.Desktop` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.Desktop.dll` |
| `Avalonia.FreeDesktop` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.FreeDesktop.dll` |
| `Avalonia.FreeDesktop.AtSpi` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.FreeDesktop.AtSpi.dll` |
| `Avalonia.HarfBuzz` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.HarfBuzz.dll` |
| `Avalonia.Native` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.Native.dll` |
| `Avalonia.Remote.Protocol` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.Remote.Protocol.dll` |
| `Avalonia.Skia` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.Skia.dll` |
| `Avalonia.Themes.Fluent` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.Themes.Fluent.dll` |
| `Avalonia.Win32` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.Win32.dll` |
| `Avalonia.X11` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | `PRESENT_CANDIDAT` : `Avalonia.X11.dll` (managé même sur Windows) |
| `HarfBuzzSharp` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz si natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | `PRESENT_CANDIDAT` : `HarfBuzzSharp.dll` |
| `HarfBuzzSharp.NativeAssets.Linux` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | `NATIF_AUTRE_RID_ABSENT_CANDIDAT` : aucun `.so`, `.dylib`, `.wasm` |
| `HarfBuzzSharp.NativeAssets.macOS` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | `NATIF_AUTRE_RID_ABSENT_CANDIDAT` : aucun `.so`, `.dylib`, `.wasm` |
| `HarfBuzzSharp.NativeAssets.WebAssembly` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | `NATIF_AUTRE_RID_ABSENT_CANDIDAT` : aucun `.so`, `.dylib`, `.wasm` |
| `HarfBuzzSharp.NativeAssets.Win32` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | `PRESENT_CANDIDAT` : `libHarfBuzzSharp.dll` |
| `MicroCom.Runtime` | `0.11.6` | MIT | licenses/MicroCom-LICENSE.txt | `PRESENT_CANDIDAT` : `MicroCom.Runtime.dll` |
| `SkiaSharp` | `3.119.4` | MIT wrapper + BSD Skia si natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | `PRESENT_CANDIDAT` : `SkiaSharp.dll` |
| `SkiaSharp.NativeAssets.Linux` | `3.119.4` | MIT wrapper + BSD Skia natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | `NATIF_AUTRE_RID_ABSENT_CANDIDAT` : aucun `.so`, `.dylib`, `.wasm` |
| `SkiaSharp.NativeAssets.macOS` | `3.119.4` | MIT wrapper + BSD Skia natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | `NATIF_AUTRE_RID_ABSENT_CANDIDAT` : aucun `.so`, `.dylib`, `.wasm` |
| `SkiaSharp.NativeAssets.WebAssembly` | `3.119.4` | MIT wrapper + BSD Skia natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | `NATIF_AUTRE_RID_ABSENT_CANDIDAT` : aucun `.so`, `.dylib`, `.wasm` |
| `SkiaSharp.NativeAssets.Win32` | `3.119.4` | MIT wrapper + BSD Skia natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | `PRESENT_CANDIDAT` : `libSkiaSharp.dll` |
| `Tmds.DBus.Protocol` | `0.94.1` | MIT | licenses/Tmds.DBus-COPYING.txt | `PRESENT_CANDIDAT` : `Tmds.DBus.Protocol.dll` (managé même sur Windows) |

## Preuve fichier-level du candidat de DEV 1

Source : [PR #55, job .NET de la CI #174](https://github.com/TobbyCy/RomothequeManager/actions/runs/39958695887/job/120431047432), bloc `PUBLISH_FILE_MANIFEST_BEGIN/END`, **223 fichiers** avant ajout des fichiers juridiques. Les 25 paquets NuGet ont été confrontés aux noms observés. **22 assemblies Avalonia** y sont visibles, y compris les modules managés FreeDesktop/X11 : ne pas les exclure au motif qu'ils visent Linux. Les variantes natives Linux/macOS/WebAssembly ne sont pas présentes (0 extension `.so`, `.dylib`, `.wasm`).

| Binaire observé | SHA-256 du candidat | Constat |
|---|---|---|
| `av_libglesv2.dll` | `53191a77fe783cd757ca7767077c2a64a662e7043777a5b4ab74980d4a0b73e3` | ANGLE natif Windows présent |
| `libSkiaSharp.dll` | `7dec3ba900ab353491e6446f0083739924c6f8dd668832e2f09d38ebffdbbe1c` | Skia natif présent |
| `libHarfBuzzSharp.dll` | `145ade963eba427027d5e1381db5d51a0ef9e98e9fef9efbfa6074ca20da246f` | HarfBuzz natif présent |
| `hostfxr.dll` | `838591885c5396fb0588a3aedc2f3e841d3afc67570868b1bce615264005a44c` | Version **.NET 10.0.12**, commit runtime `95017c71…` |
| `System.Private.CoreLib.dll` | `1125acc8106c43fc8bad2d203c4c4485df6182d292846c2fff415c1040c54678` | Version **10.0.12-servicing.26422.108**, même commit |

Le manifeste observe également `MicroCom.Runtime.dll`, `Tmds.DBus.Protocol.dll`, `HarfBuzzSharp.dll` et `SkiaSharp.dll`. Les noms de fichiers constituent une preuve de présence, mais la **provenance exacte de la licence binaire Microsoft** doit être rapprochée du runtime pack NuGet **10.0.12** ; les notices de la branche source [dotnet/runtime v10.0.12](https://github.com/dotnet/runtime/tree/v10.0.12) ne suffisent pas à prouver le contenu du pack Windows.

## Sources éditeurs et textes complets conservés

- Avalonia 12.1.4 : [licence MIT](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/licence.md) et [NOTICE.md](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/NOTICE.md) ; l'avis éditeur est plus étendu que la seule licence MIT.
- ANGLE Windows Natives 2.1.27548.20260419 : [licence exacte publiée par NuGet](https://www.nuget.org/packages/Avalonia.Angle.Windows.Natives/2.1.27548.20260419/License), comparée au [dépôt ANGLE Avalonia](https://github.com/AvaloniaUI/angle/blob/avalonia-fork-master/LICENSE). Licence de type BSD : copyright, conditions, non-endorsement et exclusion de garantie à reproduire en binaire.
- SkiaSharp 3.119.4 et HarfBuzzSharp wrapper : [MIT mono/SkiaSharp](https://github.com/mono/SkiaSharp/blob/v3.119.4/LICENSE.md). Le binaire natif Skia relève en outre de la [licence BSD Skia](https://github.com/google/skia/blob/main/LICENSE).
- HarfBuzz natif 8.3.1 : [COPYING éditeur](https://github.com/harfbuzz/harfbuzz/blob/8.3.1/COPYING), licence Old MIT et avis des contributeurs.
- MicroCom.Runtime 0.11.6 : [MIT MicroCom](https://github.com/kekekeks/MicroCom/blob/master/LICENSE).
- Tmds.DBus.Protocol 0.94.1 : [MIT/COPYING](https://github.com/tmds/Tmds.DBus/blob/main/COPYING).
- Runtime .NET 10 Windows self-contained : [règles Microsoft de redistribution](https://github.com/dotnet/runtime/blob/main/docs/project/licensing-assets.md). Copier **les fichiers exacts `LICENSE.TXT` et `THIRD-PARTY-NOTICES.TXT` du runtime pack `Microsoft.NETCore.App.Runtime.win-x64` réellement embarqué**, avec sa version et ses hashes. Ne pas remplacer la licence binaire Windows par une supposition « .NET = MIT ».

Les textes complets identifiés sont dans `packaging/licenses/` ; `packaging/THIRD-PARTY-NOTICES.txt` indexe ces textes. Leur présence en dépôt ne signifie pas encore qu'ils sont présents dans le ZIP.

## Complément natif — paquet éditeur exact (23.10.2026)

- **Preuve du producteur, SkiaSharp v3.119.4 :** [NativeAssets.Build.targets](https://github.com/mono/SkiaSharp/blob/v3.119.4/binding/NativeAssets.Build.targets) place `External-Dependency-Info.txt` dans les paquets natifs sous le nom `THIRD-PARTY-NOTICES.txt`. Les deux projets [SkiaSharp.NativeAssets.Win32](https://github.com/mono/SkiaSharp/blob/v3.119.4/binding/SkiaSharp.NativeAssets.Win32/SkiaSharp.NativeAssets.Win32.csproj) et [HarfBuzzSharp.NativeAssets.Win32](https://github.com/mono/SkiaSharp/blob/v3.119.4/binding/HarfBuzzSharp.NativeAssets.Win32/HarfBuzzSharp.NativeAssets.Win32.csproj) partagent cette règle.
- **Copie byte-identique du texte éditeur :** [External-Dependency-Info.txt](https://github.com/mono/SkiaSharp/blob/v3.119.4/External-Dependency-Info.txt) → `packaging/licenses/SkiaSharp-THIRD-PARTY-NOTICES.txt`. Git blob SHA-1 des deux fichiers : `02f9d113ec2763c89c4c33af3e9c55d522fbef06`. Ce document officiel comporte 22 rubriques, notamment Skia, HarfBuzz, ANGLE, FreeType, libpng, libjpeg-turbo, ICU, Adobe DNG SDK, gif, libmicrohttpd et SPIR-V ; ne pas le tronquer ni le reconstituer.
- **Pourquoi c'était bloquant :** le [validate Windows #39974178891](https://github.com/TobbyCy/RomothequeManager/actions/runs/39974178891) était techniquement réussi (241 fichiers, 75 298 304 octets, SHA-256 `f0213026001566ebc58e8c49616768c4c37489d75c6177eddbd714171060b0fc`), mais ne contenait pas cette notice du producteur. Il ne peut donc pas être distribué tel quel. Le script est modifié pour exiger et copier ce neuvième document source, avec échec si absent.
- **Qualification nécessaire :** la notice du producteur énumère aussi des composants à choix de licence ou copyleft (gif MPL/GPL/LGPL, FreeType FTL/GPL, libmicrohttpd LGPL/eCos). Cette liste peut être plus large que ce qui est effectivement lié dans les DLL Windows. Il faut documenter les composants réellement incorporés et les choix/obligations applicables, spécialement pour libmicrohttpd, avant d'affirmer une conformité complète. La présence de la notice ne prouve pas à elle seule le lien statique de chacun des 22 composants.
- **ANGLE Avalonia :** `av_libglesv2.dll` provient du paquet distinct `Avalonia.Angle.Windows.Natives 2.1.27548.20260419`, dont la [licence NuGet](https://www.nuget.org/packages/Avalonia.Angle.Windows.Natives/2.1.27548.20260419/License) est déjà copiée. Les sous-composants éventuellement incorporés par le fork Avalonia/ANGLE doivent encore être confrontés au paquet binaire exact. La notice du paquet SkiaSharp ne vaut pas preuve pour ce DLL distinct.

**Statut :** `NATIVE_PUBLISHER_NOTICES_ADDED_TO_SOURCE`, `ZIP_REVALIDATION_REQUIRED`, `NATIVE_LINKAGE_REVIEW_OPEN`, `RELEASE_HOLD`. **Aucun marqueur d'approbation juridique** tant que ces points ne sont pas clos.

## Contrôles de clôture nécessaires — pas de GO distribution à ce stade

1. **PUBLISH-MAP** : le manifeste du candidat #55 (223 fichiers) est rapproché ci-dessus ; DEV 1 doit le régénérer et le confronter au **ZIP final exact** sur HEAD figé. Confirmer que le contenu final correspond aux fichiers réellement distribués.
2. **DOTNET-RUNTIME** : version observée **10.0.12** ; extraire les deux documents binaires Microsoft du runtime pack Windows exact, les copier dans le ZIP et prouver leur provenance/hash.
3. **NATIVE-THIRD-PARTY** : inspecter les éventuels sous-composants/avis provenant des binaires ANGLE/Skia/HarfBuzz ; conserver les notices de distribution des paquets exacts. Ne pas déduire du seul statut MIT NuGet que toutes les obligations natives sont couvertes.
4. **ZIP-CONTENT** : inclure l'index `THIRD-PARTY-NOTICES.txt`, les fichiers `licenses/` réellement applicables, la licence projet et les avis .NET ; gate bloquant si une notice requise est absente, si une exclusion n'est pas prouvée ou si le hash ne correspond pas au ZIP exact.
5. **REVUE** : CI sur PR de distribution, contrôle indépendant Agent sur ZIP extrait, décision du Contrôleur avant upload. UX externe reste `NOT_TESTED` et ne vaut pas approbation d'une Release.

**Verdict DEV 2 :** `SOURCE_INVENTORY_PREPARED` ; `CANDIDATE_PUBLISH_223_FILES_MAPPED` ; `FINAL_ZIP_NOT_YET_PROVEN` ; `RELEASE_HOLD`. Ce document ne lève pas la réserve `thirdPartyNotices=PENDING` de l'Issue #50.
