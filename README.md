# ROMothèque Manager

<p align="center"><img src="assets/branding/RM_V2_Horizontal_Fond_Sombre.png" alt="Logo officiel ROMothèque Manager" width="640"></p>

**Explorer ses collections rétro et leurs métadonnées, sans modifier les fichiers d'origine.** Logiciel Windows en développement : le ZIP public et les nouveautés testées dans GitHub ne sont pas identiques. Voir [STATUS.md](STATUS.md).

## Télécharger et essayer

**Dernière version publiée : [v0.3.0-alpha.1 Windows x64 portable](https://github.com/TobbyCy/RomothequeManager/releases/tag/v0.3.0-alpha.1)** (NEXT-03, **pas NEXT-04**).

1. Télécharger le [ZIP Windows](https://github.com/TobbyCy/RomothequeManager/releases/download/v0.3.0-alpha.1/RomothequeManager-v0.3.0-alpha.1-win-x64.zip) et le [SHA-256](https://github.com/TobbyCy/RomothequeManager/releases/download/v0.3.0-alpha.1/RomothequeManager-v0.3.0-alpha.1-win-x64.zip.sha256).
2. Vérifier avec `Get-FileHash -Algorithm SHA256 .\RomothequeManager-v0.3.0-alpha.1-win-x64.zip` ; empreinte attendue : `07989c5f87b6fff605412cc54d43232f0f8b23632fa301341615e587e4a6e228`.
3. Extraire entièrement l'archive et lancer `RomothequeManager.V2.exe`. Aucun SDK .NET ni Node.js nécessaire. Application non signée : ne pas désactiver les protections Windows.
4. Explorer les démonstrations **fictives** et les fonctions Alpha. Ne pas employer une ROMothèque personnelle pour la recette de développement.

## Disponible, en cours, absent

| Fonction | ZIP public | Code NEXT-04 dans `main` |
| --- | --- | --- |
| Dashboard, bibliothèques, collection, fiches | Oui, Alpha et démonstrations | Interface améliorée [#93 fusionnée](https://github.com/TobbyCy/RomothequeManager/pull/93) dans `main`, **non publiée en ZIP** |
| Import ES-DE historique | Expérimental, en lecture seule | Trois conventions, diagnostics, métadonnées et médias renforcés |
| Inventaire NEXT-03 | Aperçu/simulation ; **scan personnel refusé** | Tests du service sur fixtures fictives |
| Catalogue sauvegardé, export/copie de ROMs | **Non implémentés** | Non livrés |

L'import est volontaire, ne modifie pas les fichiers sources et conserve le catalogue **en mémoire seulement**. Les dispositions ES-DE personnelles ne sont pas toutes garanties ; aucun enrichissement réseau ni scraper. Une CI verte et des captures Windows ne constituent pas une recette humaine. Aucun jeu, ROM, BIOS ni jaquette commerciale n'est fourni.

## Documentation et contribution

[État daté du produit](STATUS.md) · [Précautions](docs/user/securite-et-limites.md) · [Historique technique](docs/product-status.md) · [Roadmap](docs/roadmap.md) · [Signaler un problème](https://github.com/TobbyCy/RomothequeManager/issues). Pour signaler un défaut, utiliser uniquement des exemples fictifs, jamais de fichiers, XML ou chemins privés.

Le code original concerné est sous [MIT](LICENSE). **Le bandeau affiché ci-dessus et les icônes officiels sont inclus dans le dépôt, mais ne sont pas sous licence MIT** : [notice graphique](assets/branding/README.md) et [BRANDING.md](BRANDING.md). Les droits des éléments tiers restent distincts.
