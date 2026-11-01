# État de ROMothèque Manager — instantané historique et limites actuelles

> **Consolidation NEXT-05 / #146 en cours.** Les preuves ci-dessous décrivent le 30 octobre, pas le HEAD courant. Depuis cet instantané, SQLite minimal, inventaire et médias ont été intégrés sur fixtures dans `main`, sans recette humaine ni autorisation de données personnelles. Vérifier la [dernière Release publiée](https://github.com/TobbyCy/RomothequeManager/releases) indépendamment de `main`. Les restrictions `REAL_*_HOLD` et `NEXT05_RELEASE_HOLD` restent actives ; la recette humaine est **NOT_EXECUTED**. La synchronisation définitive avec DocFX #148 et MVVM #147 reste **PENDING**.

**Instantané historique vérifié le 30 octobre 2026 (superseded pour l'état actuel).** `main` : `f1bd75b15ab8dacbbdb4ec25e6bff32bed4cda5e`. Les fonctionnalités en PR ne sont pas distribuées automatiquement.

| État | Ce qui existe | Limite |
| --- | --- | --- |
| **PUBLIÉ** | [Alpha v0.3.0-alpha.1 Windows x64 portable](https://github.com/TobbyCy/RomothequeManager/releases/tag/v0.3.0-alpha.1) : démos et import ES-DE historique expérimental | **NEXT-03, pas NEXT-04** ; catalogue en mémoire, scan personnel refusé |
| **MAIN — fusionné** | Backend ES-DE #92/#96 **et UX #93 fusionnés** : trois conventions XML, diagnostics, limites de lecture, métadonnées Genre/Developer/ReleaseDate, conservation du dernier catalogue valide | Tests sur fixtures fictives ; **non présent automatiquement dans le ZIP publié** |
| **MAIN — UX intégrée** | [#93 fusionnée](https://github.com/TobbyCy/RomothequeManager/pull/93) depuis HEAD `bed9735919b1d9b572c87671746fa486ba396e93` : cinq écrans, navigation, recherche, pagination, logo/ICO, démonstration ES-DE fictive | Réception technique `APPROVABLE_INTEGRATED_SYNTHETIC` ; **pas dans la Release publique actuelle** |
| **RECETTE HUMAINE** | Procédure [#97](https://github.com/TobbyCy/RomothequeManager/issues/97), test prévu sur bibliothèque fictive | **NOT_EXECUTED** : picker réel, confirmation/annulation, clavier/focus, DPI physique, ultralarge ; bibliothèque personnelle non testée |
| **NON LIVRÉ dans cet instantané / ZIP** | Persistance durable pour l'utilisateur, scraper, SteamOS qualifié, installateur signé, export, copie et réorganisation de ROMs | Maquettes, contrats et boutons inactifs ne sont pas des fonctions |

**Preuves techniques :** les trois CI sur le HEAD combiné #93 sont vertes (Adaptive avec `ci-policy-gate`, moteur Windows natif et GUI Windows). Ce sont des preuves **synthétiques**, pas une validation d'usage personnel. Un ZIP temporaire de CI n'est pas un téléchargement public pérenne.

Les anciens ZIP V2-01C restent consultables dans les [Releases](https://github.com/TobbyCy/RomothequeManager/releases). Le [README](README.md) renvoie à la dernière Release effectivement publiée.

[Contrat NEXT-04 #91](https://github.com/TobbyCy/RomothequeManager/issues/91) · [Remédiation usage #97](https://github.com/TobbyCy/RomothequeManager/issues/97) · [DOC-TRUTH #98](https://github.com/TobbyCy/RomothequeManager/issues/98) · [Historique technique](docs/product-status.md). Les anciens HEAD et mentions « Draft » dans l'historique décrivent **leur époque**, pas l'état actuel.

**#93 fusionnée dans main ;** aucune nouvelle Release n'est implicite. La recette humaine demeure distincte.
