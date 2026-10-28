# DEMO-LIBRARY-01 — générateur de ROMothèque fictive

**Dépendance contractuelle : DEV 2 / [PR Draft #72](https://github.com/TobbyCy/RomothequeManager/pull/72).** Les fichiers de vérité terrain sont `contracts/demo-library-01/catalogue.json`, `scenarios.json` et `oracles.json`, repris à l'identique dans cette branche. Les identités `demoId` restent distinctes des identités ES-DE et des observations NEXT-02. Aucun fichier commercial, BIOS, scraper ni contenu tiers.

## Exécution explicite

Environnement : **Node.js 20+**, aucun paquet npm ou dépendance Internet. Depuis la racine du dépôt, sous PowerShell ou terminal :

```powershell
node tools/demo-library/generate.cjs --output "C:\temp\demo01-nouveau-dossier"
node tools/demo-library/generate.cjs --verify "C:\temp\demo01-nouveau-dossier"
```

Le dossier ciblé doit être **absolu, local, inexistant**, avec un parent déjà existant. Interdiction des chemins UNC/URL, destinations dans le dépôt, liens symboliques de la chaîne parente et overwrites. Le chemin fourni autorise exclusivement la création de cette sortie ; aucun scan de ROMothèque personnelle. L'option `--catalog /chemin/absolu/contracts/demo-library-01/catalogue.json` sélectionne explicitement un autre exemplaire **accompagné des deux contrats `scenarios.json` et `oracles.json`** du même dossier.

## Résultats contractuels DEV 2

| Variante | Identifiants de référence | Fichiers sous ROMS | Entrées ES-DE | PNG locaux |
|---|---:|---:|---:|---:|
| IDEAL | 30 | 32 | 30 | 166 |
| CHAOS | 30 | 30 | 24 | 160 |
| MYSTERY | 30 | 32 | 0 | 12 |

- **IDEAL** : arborescence `ROMS/<platform>` et `MEDIAS/<platform>/metadata/es-de-gamelist.xml`, vraies jaquettes/logo/vignettes/captures/arrière-plans PNG, faux contenus ROM texte, un jeu multidisque (`demo01-016` et deux faux disques) ; 8 plateformes.
- **CHAOS** : noms variables, sous-dossiers et accents, six entrées sans XML, une ROM de jeu référencée mais absente (`demo01-027`), un second disque déclaré mais absent (`demo01-016`), six médias absents et deux faux fichiers de contenus volontairement identiques sans déduction d'identité automatique. XML demeure valide pour tester les diagnostics.
- **MYSTERY** : 30 fichiers de référence sans XML ES-DE et douze couvertures isolées non automatiquement reliées à des jeux ; inventaire autonome NEXT-02 uniquement.

Les médias `front→image`, `thumbnail→thumbnail`, `logo→marquee` sont référencés dans ES-DE ; les autres restent dans le manifest (l'importeur ne les connaît pas). Les images sont **de vrais PNG originaux**, dessinés hors ligne par code et lettrage bitmap local, sans marque/personnage commercial. Les pseudo-ROMs et faux disques sont du texte avec l'en-tête `DEMO01_SIMULATED_NON_EXECUTABLE_NO_ROM_OR_BIOS_CONTENT` ; une playlist `.m3u` est un simple texte, pas une ROM exécutable.

## Manifests et intégrité

Un `manifest.json` par variante et un manifest racine rassemblent les chemins relatifs, identifiants stables, disponibilités, résultats attendus, tailles et SHA-256 des **seules données fictives créées**. Un run sur même catalogue/Node/zlib est déterministe, sans timestamp ni chemin absolu incorporé au contenu. `--verify` contrôle fichiers déclarés, empreintes et signatures PNG ; ne modifie rien.

La sortie préexistante est refusée, sans nettoyage ni overwrite. Un arrêt peut laisser une nouvelle sortie partielle ; sans manifest racine final valide, elle est **incomplète**. Relancer sur une nouvelle sortie choisie. Protection atomique contre toutes les mutations concurrentes de chemins non prouvée : **aucun GO sur collection réelle**.

## Contrôle et gouvernance

Tests MSTest dans `tests/dotnet/RomothequeManager.V2.Tests/DemoLibraryGeneratorTests.cs` : 30 IDs identiques, oracles de la PR #72, import ES-DE IDEAL/CHAOS, anomalies connues, inventaire MYSTERY, images PNG locales, même sortie binaire à deux destinations, refus de sortie existante, catalogue invalide, contrôles SHA. Les fixtures historiques #64 sont strictement inchangées.

**Chaîne de PR : #72 (contrat DEV 2) → #73 (générateur DEV 1).** Cette branche contient le contrat de #72 uniquement pour valider son exécution ; aucun merge vers `main` ni Release ne sont autorisés par cela. `FORMAT_ACK` et contre-revue restent requis avant le gel du contrat.

**CI / PR superposées :** la PR #73 cible temporairement `main` parce que le workflow Adaptive CI ne déclenche pas les PR dont la base est la branche DEV 2 #72. La branche #73 contient les contrats #72 comme dépendance parent Git, sans les modifier ; **fusionner d'abord #72** sous GO distinct, puis resynchroniser #73 avec `main` et recontrôler son diff avant une éventuelle fusion (non autorisée par ce mandat).
