# DEMO-LIBRARY-01 — catalogue canonique (DEV 2)

**Statut : READY_FOR_AGENT_REVIEW, sous alignement du générateur DEV 1.** La [liste de 30 titres et corrections propriétaires](https://github.com/TobbyCy/RomothequeManager/issues/71) prime sur l'ancienne proposition de huit plateformes. [Générateur Draft #73](https://github.com/TobbyCy/RomothequeManager/pull/73), [coordination format #70](https://github.com/TobbyCy/RomothequeManager/issues/70).

**Fichiers** : [catalogue canonique JSON](../../contracts/demo-library-01/catalogue.json) ; [scénarios](../../contracts/demo-library-01/scenarios.json) ; [oracles](../../contracts/demo-library-01/oracles.json) ; [briefs médias](demo-library-01-media-brief.md). Ce sont des spécifications, **aucune ROM, aucun média PNG et aucun XML n'est livré dans cette PR**.

## Contrat de données

Le catalogue expose exactement 30 jeux et les champs déjà consommables par le générateur : `schemaVersion = demo-library-01/1` ; pour chaque entrée `id, title, platform, genre, year, description, publisher, region, edition, romFile`, et `discs` pour les playlists. Champs supplémentaires : `media`, `caseTags` et, uniquement en déclaration pour une phase suivante, `alternateReleases` et `additionalMedia`. Chaque ID `demo01-NNN` est canonique dans les trois scénarios, indépendant des `EsDeGame.GameId` dérivés des chemins et des `FileObservation.ObservationId` d'un scan.

**Plateformes primaires : 13**, sans restriction rétro : dos, gamecube, gba, megadrive, n64, nes, pc, ps2, ps3, ps4, psx, snes, xbox. `Hello!` = Xbox originale (`xbox`), `Jaxson & Dex` et `Need for Sleep` = PS2 (`ps2`), `Dad of Chores` = PS4 (`ps4`) ; PC, DOS, PS3 et GameCube figurent dans le catalogue. Les extensions `.bin`/`.chd` sont des marqueurs de fixtures non exécutables : elles ne signifient pas qu'un fichier est jouable ni identifié par la plateforme.

La disposition documentaire cible est `ROMS/<platform>/<romFile>` et `MEDIAS/<platform>/<covers|backcovers|logos|screenshots|thumbnails|backgrounds>/<id>.png`. Dans IDEAL/CHAOS, un seul XML par plateforme à `MEDIAS/<platform>/metadata/es-de-gamelist.xml` ; ses chemins de ROM sont relatifs à `ROMS/<platform>` et ses chemins médias au répertoire `metadata`. Seuls `image, thumbnail, marquee` sont consommés par l'importateur actuel ; autres médias sont documentés dans les manifestes.

## Résultats de référence NON EXÉCUTÉS sur le catalogue corrigé

| Scénario | Jeux canoniques | Fichiers sous ROMS | Entrées XML | PNG |
|---|---:|---:|---:|---:|
| IDEAL | 30 | 32 | 30 | 180 |
| CHAOS | 30 | 33 | 30 | 166 |
| MYSTERY | 30 | 32 | 0 | 0 |

- **IDEAL** : 30 points d'entrée physiques (dont une playlist Metal Deer Salad ID 011), deux disques, toutes les références ES-DE et six médias originaux par jeu.
- **CHAOS** : 2 ROMs principales absentes mais référencées (005 Little Theft Moto, 017 Minorcraft), 3 doublons candidats (004, 013, 027 Street Fritter II), 2 disques et 14 médias manquants. Ne jamais déduire une suppression d'un fichier indisponible. Les XML restent lisibles et contiennent 30 jeux.
- **MYSTERY** : 30 points d'entrée + 2 membres de playlist, ni XML ni médias ; la preuve de 30 jeux ne vient que des IDs de l'oracle, jamais du scanner. `ConfirmedGameCount=null`.

Les cas secondaires (autre édition/plateforme, deuxième capture, XML volontairement cassé ou image corrompue) sont **déclarés mais hors décompte nominal**. Les XML invalides de Paper Plumber et PNG corrompus de Final Tax Return doivent être matérialisés dans des fixtures négatives séparées, sans casser IDEAL/CHAOS.

## Coordination obligatoire avant gel du format

Le générateur DEV 1 #73 possède actuellement un catalogue candidat différent (anciens noms/plateformes), et ses tests exigent des homonymes `Average Anto` / `SacMon : Titane Damas` qui ne figurent plus dans les 30 titres corrigés. **DEV 1 doit remplacer la source candidate par le catalogue canonique (option `--catalog`), ajuster ces assertions, et aligner le doublon contrôlé de Street Fritter II (027).** Les versions alternatives de The Shrims (PS2), Pasta Kombat et Average Anto Bros., ainsi que la 2e capture de Jaxson & Dex, sont décrites sans revendiquer une génération effective. Les contrôles de structure des JSON ne sont pas des tests d'intégration.

La [PR Draft #72](https://github.com/TobbyCy/RomothequeManager/pull/72) sera revue indépendamment par l'Agent. DEV 1 doit répondre `FORMAT_ACK` ou `CHANGES_REQUIRED` sur la source de vérité et ses éventuels écarts ; aucune fusion ni Release autonome.

