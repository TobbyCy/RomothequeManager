# DEMO-LIBRARY-01 — contrat de catalogue fictif (CANDIDAT)

**DEV 2 / 28.10.2026 / `FORMAT_ACK DEV 1` EN ATTENTE / PR Draft exclusivement.** Coordination ouverte : [Issue #70](https://github.com/TobbyCy/RomothequeManager/issues/70).

La présente PR n'embarque **aucun fichier ROM, XML ES-DE, image ni média**. Les JSON décrivent des **fixtures à fabriquer ultérieurement** et des résultats **prévisionnels non exécutés** ; ces données ne sont pas le DTO catalogue historique, un `EsDeCatalogueSnapshot` ni un `InventorySnapshot` réel.

## Référentiel

- [`catalogue.json`](catalogue.json) : 30 `demoId` stables, titre humoristique, plateforme, genre, année fictive, description, éditeur fictif, édition, région, chemin de ROM, disques et médias à créer.
- [`scenarios.json`](scenarios.json) : 30 entrées avec les **mêmes identifiants** dans chacun des scénarios `IDEAL`, `CHAOS`, `MYSTERY`, chemins physiques **prévus**, références XML et disponibilités des médias.
- [`oracles.json`](oracles.json) : assertions de structure et résultats attendus, sans prétendre avoir exécuté un scanner ou généré des fixtures.
- [`media-brief.md`](media-brief.md) : consignes artistiques strictement originales pour des créations futures.

**Identités :** `demoId` identifie un jeu fictif dans le référentiel, alors que NEXT-01-A génère un `EsDeGame.GameId` fondé sur plateforme/chemin, susceptible de varier après renommage, et NEXT-02 génère des `FileObservation.ObservationId` liés à un scan. Aucune conversion automatique `fichier = jeu`, aucune déduction d'un ID depuis le titre.

## Compatibilité candidate avec DEV 1

Racine temporaire consentie : `ROMS/<platform>/...` et `MEDIAS/<platform>/...`. En `IDEAL` / `CHAOS`, conserver **un seul** XML par plateforme à `MEDIAS/<platform>/metadata/es-de-gamelist.xml` ; `<game><path>` relatif à `ROMS/<platform>`, médias relatifs au dossier du XML. Exemple pour `demo01-001` : `<path>./demo01-001_sacmon-titane-and-damas.gba</path>`, `<image>../games/demo01-001/front.png</image>`. Ces exemples sont à **générer seulement dans une future fixture** : aucun XML de ce type n'est présent ici. Les caractères `&`/accents doivent être échappés en XML et encodés UTF-8/NFC.

Schéma ES-DE effectivement lu dans l'adaptateur actuel : `<image>`, `<thumbnail>`, `<marquee>`, `<video>`. Mapping candidat : `front → image`, `thumbnail → thumbnail`, `logo → marquee`. Les champs **back, screenshot, background** restent dans le **manifeste externe**, car l'importateur ne les lit pas ; ne jamais détourner `video` vers une capture PNG. `MYSTERY` n'a **aucun XML** mais doit pouvoir être observé par NEXT-02, qui fonctionne sans ES-DE.

## Trois variantes et oracles arithmétiques (NOT_EXECUTED)

| Variante | IDs de référence | ROMS : fichiers physiques prévus | Entrées ES-DE | Médias présents prévus |
|---|---:|---:|---:|---:|
| `IDEAL` | 30 | 32 | 30 | 166 |
| `CHAOS` | 30 | 30 | 24 | 160 |
| `MYSTERY` | 30 | 32 | 0 | 12 |

`IDEAL` : rangement régulier, 30 entrées XML, toutes les ROMs fictives et les 166 médias planifiés présents **après matérialisation future**. `CHAOS` : huit points d'entrée renommés/déplacés, 24 références XML, ROM principale `demo01-027` référencée mais absente, second disque de `demo01-016` absent, six médias manquants. `MYSTERY` : 30 IDs identiques dans la vérité terrain, **0 XML**, ROMs en dossiers `vrac`, douze couvertures isolées sans association automatiquement prouvée.

**Points d'épreuve :** homonymes `Night Shift` IDs 023 et 024 (plateformes et jeux différents), `Clockwork Snooze` ID 016 (playlist + deux disques = **un jeu de référence, trois fichiers**), éditions/régions distinctes, accents et ponctuation, long synopsis et médias manquants. Les 30 noms sont des parodies **originales** ; aucune illustration, marque, logo ou personnage commercial n'est reproduit.

Les comptes `RomsPhysicalFiles` concernent **les fichiers sous ROMS**, pas le nombre de jeux identifiés par l'inventaire. Un `Complete` couvre la portée énumérée même si une référence XML ou un membre de playlist est indisponible ; aucune « suppression » n'en découle. `ConfirmedGameCount=null` reste la vérité pour NEXT-02 avant association fiable. L'absence de XML en `MYSTERY` peut faire échouer l'importateur ES-DE, **pas l'inventaire autonome**.

## Recette documentaire / dépendances

L'oracle exige 30 IDs uniques identiques dans toutes les variantes, des chemins locaux relatifs distincts, les comptes ci-dessus, une distinction des fichiers/jeux et aucune fausse attribution de médias. Les vérifications de cohérence des manifestes **ne prouvent pas l'exécution de ROMs/PNG/chargement XML**, qui reste `NOT_EXECUTED`.

**DEV 1 :** valider dans [#70](https://github.com/TobbyCy/RomothequeManager/issues/70) les conventions XML et médias, les aliases plateformes, les comptes et la séparation IDs (`FORMAT_ACK` ou `CHANGES_REQUIRED`). **Ne pas figer les contrats avant cette réponse.** Si besoin, corriger seulement les documents de cette PR. Aucun développement UI, scanner, export, téléchargement de ROM, merge ou Release dans le lot DEV 2.

**État : `DEMO01_CATALOG_CANDIDATE / DEV1_FORMAT_ACK_PENDING`.** L'objectif `DEMO01_CATALOG_READY` est conditionné à l'accord de format et à la validation du Contrôleur.
