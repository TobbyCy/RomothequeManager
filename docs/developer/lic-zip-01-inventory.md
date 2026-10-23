# LIC-ZIP-01 — inventaire juridique ciblé du ZIP Windows

Source figée : `src/RomothequeManager.V2/packages.lock.json` du `main` après PR #53 (commit `31579350033c22cd3c55e156d29a4da7f18ad822`). Cible : `net10.0 / win-x64 / self-contained`. Mandat : Issue #50.

## Portée et méthode

La restauration NuGet ne prouve **pas** que les fichiers sont publiés. Les 25 paquets ci-dessous sont les dépendances résolues de `net10.0`; une entrée du bloc RID `net10.0/win-x64` n'est **pas** non plus une preuve de contenu ZIP. L'inventaire de sources est établi ; la colonne de présence réelle reste à renseigner à partir du manifeste de `dotnet publish` fourni par DEV 1. Les composants natifs statiquement intégrés demandent aussi des avis spécifiques.

| Paquet NuGet | Version verrouillée | Licence / famille | Notice source candidate pour le ZIP | Statut de présence à valider |
|---|---|---|---|---|
| `Avalonia` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.Angle.Windows.Natives` | `2.1.27548.20260419` | ANGLE BSD-style | licenses/ANGLE-LICENSE.txt | Windows natif candidat; vérifier DLL exacte |
| `Avalonia.BuildServices` | `11.3.2` | À relever du paquet; build uniquement attendu | — si absent | Exclure du ZIP seulement après preuve de manifeste |
| `Avalonia.Desktop` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.FreeDesktop` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.FreeDesktop.AtSpi` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.HarfBuzz` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.Native` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.Remote.Protocol` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.Skia` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.Themes.Fluent` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.Win32` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `Avalonia.X11` | `12.1.4` | MIT + NOTICE éditeur | licenses/Avalonia-LICENSE.txt + Avalonia-NOTICE.txt | Assembly candidat; confronter au publish |
| `HarfBuzzSharp` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz si natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | Assembly managé candidat; prouver présence native |
| `HarfBuzzSharp.NativeAssets.Linux` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | RID non Windows; exclusion à prouver |
| `HarfBuzzSharp.NativeAssets.macOS` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | RID non Windows; exclusion à prouver |
| `HarfBuzzSharp.NativeAssets.WebAssembly` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | RID non Windows; exclusion à prouver |
| `HarfBuzzSharp.NativeAssets.Win32` | `8.3.1.3` | MIT wrapper + Old MIT HarfBuzz natif | licenses/SkiaSharp-LICENSE.txt + HarfBuzz-COPYING.txt | DLL native Win32 candidate; vérifier licence exacte |
| `MicroCom.Runtime` | `0.11.6` | MIT | licenses/MicroCom-LICENSE.txt | Assembly managé candidat |
| `SkiaSharp` | `3.119.4` | MIT wrapper + BSD Skia si natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | Assembly managé candidat; prouver présence native |
| `SkiaSharp.NativeAssets.Linux` | `3.119.4` | MIT wrapper + BSD Skia natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | RID non Windows; exclusion à prouver |
| `SkiaSharp.NativeAssets.macOS` | `3.119.4` | MIT wrapper + BSD Skia natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | RID non Windows; exclusion à prouver |
| `SkiaSharp.NativeAssets.WebAssembly` | `3.119.4` | MIT wrapper + BSD Skia natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | RID non Windows; exclusion à prouver |
| `SkiaSharp.NativeAssets.Win32` | `3.119.4` | MIT wrapper + BSD Skia natif | licenses/SkiaSharp-LICENSE.txt + Skia-LICENSE.txt | DLL native Win32 candidate; vérifier licence embarquée et avis statiques |
| `Tmds.DBus.Protocol` | `0.94.1` | MIT | licenses/Tmds.DBus-COPYING.txt | Assembly Linux/D-Bus potentiellement exclue, à prouver |

## Sources éditeurs et textes complets conservés

- Avalonia 12.1.4 : [licence MIT](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/licence.md) et [NOTICE.md](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/NOTICE.md) ; l'avis éditeur est plus étendu que la seule licence MIT.
- ANGLE Windows Natives 2.1.27548.20260419 : [licence exacte publiée par NuGet](https://www.nuget.org/packages/Avalonia.Angle.Windows.Natives/2.1.27548.20260419/License), comparée au [dépôt ANGLE Avalonia](https://github.com/AvaloniaUI/angle/blob/avalonia-fork-master/LICENSE). Licence de type BSD : copyright, conditions, non-endorsement et exclusion de garantie à reproduire en binaire.
- SkiaSharp 3.119.4 et HarfBuzzSharp wrapper : [MIT mono/SkiaSharp](https://github.com/mono/SkiaSharp/blob/v3.119.4/LICENSE.md). Le binaire natif Skia relève en outre de la [licence BSD Skia](https://github.com/google/skia/blob/main/LICENSE).
- HarfBuzz natif 8.3.1 : [COPYING éditeur](https://github.com/harfbuzz/harfbuzz/blob/8.3.1/COPYING), licence Old MIT et avis des contributeurs.
- MicroCom.Runtime 0.11.6 : [MIT MicroCom](https://github.com/kekekeks/MicroCom/blob/master/LICENSE).
- Tmds.DBus.Protocol 0.94.1 : [MIT/COPYING](https://github.com/tmds/Tmds.DBus/blob/main/COPYING).
- Runtime .NET 10 Windows self-contained : [règles Microsoft de redistribution](https://github.com/dotnet/runtime/blob/main/docs/project/licensing-assets.md). Copier **les fichiers exacts `LICENSE.TXT` et `THIRD-PARTY-NOTICES.TXT` du runtime pack `Microsoft.NETCore.App.Runtime.win-x64` réellement embarqué**, avec sa version et ses hashes. Ne pas remplacer la licence binaire Windows par une supposition « .NET = MIT ».

Les textes complets identifiés sont dans `packaging/licenses/` ; `packaging/THIRD-PARTY-NOTICES.txt` indexe ces textes. Leur présence en dépôt ne signifie pas encore qu'ils sont présents dans le ZIP.

## Contrôles de clôture nécessaires — pas de GO distribution à ce stade

1. **PUBLISH-MAP** : DEV 1 publie le manifeste exhaustif du `dotnet publish -r win-x64 --self-contained true` sur le HEAD de distribution, avec noms et SHA-256 ; rapprocher chaque `.dll`/natif des paquets de ce tableau. Confirmer par absence les paquets non-Windows ; les sections du lock ne suffisent pas.
2. **DOTNET-RUNTIME** : relever la version exacte du runtime pack et les deux documents binaires Microsoft ; les copier dans le ZIP et prouver leur provenance/hash.
3. **NATIVE-THIRD-PARTY** : inspecter les éventuels sous-composants/avis provenant des binaires ANGLE/Skia/HarfBuzz ; conserver les notices de distribution des paquets exacts. Ne pas déduire du seul statut MIT NuGet que toutes les obligations natives sont couvertes.
4. **ZIP-CONTENT** : inclure l'index `THIRD-PARTY-NOTICES.txt`, les fichiers `licenses/` réellement applicables, la licence projet et les avis .NET ; gate bloquant si une notice requise est absente, si une exclusion n'est pas prouvée ou si le hash ne correspond pas au ZIP exact.
5. **REVUE** : CI sur PR de distribution, contrôle indépendant Agent sur ZIP extrait, décision du Contrôleur avant upload. UX externe reste `NOT_TESTED` et ne vaut pas approbation d'une Release.

**Verdict DEV 2 :** `SOURCE_INVENTORY_PREPARED` ; `ACTUAL_SHIPPED_CONTENT_NOT_YET_PROVEN` ; `RELEASE_HOLD`. Ce document ne lève pas la réserve `thirdPartyNotices=PENDING` de l'Issue #50.
