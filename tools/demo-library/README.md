# DEMO-LIBRARY-01 — générateur fictif (DEV 1)

**Catalogue canonique : [PR #72, HEAD 968d1f61420a5d0b70eb5160e9d57d505560f2b7](https://github.com/TobbyCy/RomothequeManager/pull/72)**. Les fichiers `contracts/demo-library-01/catalogue.json`, `scenarios.json`, `oracles.json` contiennent les **30 titres exacts**, 13 plateformes, identifiants, métadonnées et scénarios validés pour l'alignement. Aucun catalogue parallèle n'est maintenu.

## Exécution volontaire

Environnement : **Node.js 20+**, aucune dépendance npm ni accès Internet.

```powershell
node tools/demo-library/generate.cjs --output "C:\temp\demo01-nouveau-dossier"
node tools/demo-library/generate.cjs --verify "C:\temp\demo01-nouveau-dossier"
```

Il faut spécifier un chemin absolu local vers un dossier final **inexistant**, sous un parent déjà existant. Refus des chemins UNC, des sorties dans le dépôt de l'application, des liens dans la chaîne de dossiers existante, et des destinations déjà présentes. Le programme n'explore ni n'altère une ROMothèque réelle. `--catalog` accepte un autre exemplaire **complet** du contrat (catalogue, scénarios, oracles) dans un même dossier. Le générateur ne modifie jamais ces contrats.

## Oracles de génération (ROMS physiques / entrées XML ES-DE / vrais PNG)

| Variante | ROMS | ES-DE | PNG |
|---|---:|---:|---:|
| IDEAL | **32** | **30** | **180** |
| CHAOS | **33** | **30** | **166** |
| MYSTERY | **32** | **0** | **0** |

13 plateformes normatives : `dos`, `gamecube`, `gba`, `megadrive`, `n64`, `nes`, `pc`, `ps2`, `ps3`, `ps4`, `psx`, `snes`, `xbox`. **Hello! sur Xbox**, **Jaxson & Dex sur PS2**, **Dad of Chores sur PS4**. Les extensions `.bin` sont des marqueurs fictifs, non exécutables et non adaptés à un véritable émulateur.

- **IDEAL :** 30 jeux fictifs complets, métadonnées XML ES-DE par plateforme, vrais PNG 100 % originaux, et la playlist multidisque de *Metal Deer Salad* (`demo01-011`) avec deux faux disques. L'importateur ES-DE ne lit que image/thumbnail/marquee ; les 3 autres catégories restent dans les manifests.
- **CHAOS :** les mêmes 30 jeux dans des chemins complexes, deux ROMs principales référencées et manquantes (`demo01-005`, `demo01-017`), trois fichiers supplémentaires doublons candidats (`demo01-004`, `demo01-013`, `demo01-027` *Street Fritter II*), et 14 médias volontairement absents. Les 30 entrées XML restent nominalement **valides**.
- **MYSTERY :** 30 jeux de référence représentant 32 fichiers ROMS (dont un jeu multidisque), **sans XML ni média local**, à observer avec NEXT-02 sans présumer l'identité des jeux.

Les fichiers factices ressemblant à des ROMs/disques sont du texte lisible avec marqueur `DEMO01_SIMULATED_NON_EXECUTABLE_NO_ROM_OR_BIOS_CONTENT`. Les PNG sont de vrais fichiers raster générés hors ligne par une routine graphique originale avec lettrage bitmap original ; aucun artwork, personnage ou logo commercial. Les cas négatifs *Paper Plumber XML* et *Final Tax Return PNG* sont déclarés dans le contrat mais **ne sont pas injectés dans les scénarios normaux**. Les variantes secondaires et captures additionnelles uniquement déclarées ne sont pas générées.

## Manifestes, contrôles et précautions

Un manifeste JSON racine et un manifeste par scénario contiennent les identifiants de référence, les chemins et états, les résultats attendus et les SHA-256 des **fichiers synthétiques nouvellement générés**. `--verify` compare tailles, SHA et signature PNG. La sortie est reproductible à runtime Node/zlib identique et ne contient aucun timestamp ni chemin de destination.

Les tests .NET/MSTest sur répertoires temporaires contrôlent les 30 IDs/titres/plateformes exacts, import ES-DE, 13 plateformes, oracles arithmétiques, diagnostic ROM/média manquant, vrais PNG ouvrables, les doublons candidats, l'inventaire MYSTERY, la reproductibilité et le refus d'écraser une destination.

Une interruption peut laisser un **nouveau** dossier partiel ; il n'est pas considéré comme livraison tant que le manifeste final n'est pas vérifié. Ne pas réutiliser ce dossier : choisir une autre destination. Aucune garantie atomique face aux remplacements concurrents de chemins, aucun GO de scan réel. Les fixtures historiques de la PR #64 restent intactes.

**Gouvernance :** PR #73 Draft, contrat canonique #72 indépendant. Aucun merge ni Release autonome, validation indépendante Agent avant décision d'intégration.
