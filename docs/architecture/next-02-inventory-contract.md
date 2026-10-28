# NEXT-02-CONTRACT — Inventaire local avec ou sans métadonnées ES-DE

**Statut : CANDIDAT DOCUMENTAIRE / DEV 2 / 28.10.2026 — NON IMPLÉMENTÉ — AUCUN GO DE SCAN RÉEL.**
Références : [NEXT-02 #65](https://github.com/TobbyCy/RomothequeManager/issues/65), [NEXT-01 #61](https://github.com/TobbyCy/RomothequeManager/issues/61), [NEXT-01-A PR #62](https://github.com/TobbyCy/RomothequeManager/pull/62), [META-01 #51](https://github.com/TobbyCy/RomothequeManager/issues/51), [NEXT-01-UX PR #64](https://github.com/TobbyCy/RomothequeManager/pull/64).
Ce document spécifie un contrat et des oracles **à implémenter et tester ultérieurement** ; aucune ligne ne constitue une preuve de test passé. Évolution additive : aucune réécriture rétroactive des contrats/archives T1–T4, des comparateurs PowerShell ni des classes ES-DE existantes.

## 1. Objectif, frontière et décision d'architecture

Produire une **observation de fichiers locale, bornée, non destructive** sur une ou plusieurs racines autorisées. Le fonctionnement **ne dépend jamais de la présence d'un XML ES-DE**. Les métadonnées ES-DE, si présentes et choisies explicitement, constituent une source **optionnelle d'enrichissement avec provenance**, jamais l'identité obligatoire d'un fichier ou d'un jeu.

**Décision proposée à DEV 1 :** introduire un `InventorySnapshot` autonome (`FILESYSTEM_OBSERVATION`), puis un adaptateur explicite vers une future projection commune `CatalogueReadModel` **versionnée**. Ne pas transformer `EsDeCatalogueSnapshot` en modèle normatif ni appeler `EsDeCatalogueImporter` pour énumérer les fichiers : son entrée exige un XML. Conserver ce dernier comme lecteur de métadonnées ES-DE et réutiliser, par composition et seulement après revue, ses garde-fous de chemins, d'ouverture de médias et de staging. Ne pas changer l'API de la PR #64 dans NEXT-02.

Chaîne cible (séparation explicite) :

`Sélection utilisateur → InventoryScope / validation → InventoryEnumerator → FileObservation[] + Coverage/Diagnostics → InventorySnapshot`
`[optionnel] ES-DE XML → EsDeCatalogueSnapshot → MetadataEvidence[]`
`InventorySnapshot + MetadataEvidence[] → rapprochements candidats vérifiables → CatalogueReadModel (fichiers, groupes de jeu candidats, médias, diagnostics) → UI`

L'UI n'effectue jamais directement de parcours du disque, de parsing XML, de calcul d'identité ou de résolution de chemins sensibles. Le résultat doit être compréhensible **sans ES-DE**, sans moteur de scraping, sans SQLite obligatoire, sans opérations d'écriture.

## 2. Entités et types de données candidats

Tous les noms ci-dessous sont **conceptuels**, pas des déclarations C# déjà intégrées. Version candidate : `next-02-inventory/0.1-candidate` ; toute adoption modifiant l'interface doit être négociée avec DEV 1/DEV 3 et testée par un consommateur indépendant.

| Entité | Champs obligatoires / sémantique |
| --- | --- |
| `InventoryScope` | `librarySessionId`, racines/domaines explicitement autorisés, `platformFolderPolicyVersion`, `extensionRegistryVersion`, profondeur/volume/nombre/temps max et annulation ; **aucune racine implicite** |
| `FileObservation` | `observationId` local au scan, `locationKey` (bibliothèque logique + domaine + chemin relatif normalisé), `platformKey` ou `UNKNOWN`, `relativePath`, `kind`, `availability`, `sizeBytes?`, `modifiedUtc?`, `observedAtUtc`, `source=FILESYSTEM_OBSERVATION`, `diagnosticRefs[]` |
| `InventorySnapshot` | `schemaVersion`, `librarySessionId`, `scanId`, `capturedAtUtc`, `scanStatus`, `availability`, `freshness`, `coverageProof[]`, `observations[]`, `diagnostics[]`, `contentFingerprint` ; `observedFileCount` peut être entier ; **`totalFileCount` nullable si couverture insuffisante** |
| `MetadataEvidence` | `sourceKind=ES_DE_IMPORT` (ou autre adaptateur autorisé ultérieurement), `sourceRef` opaque, `field`, `value`, `associatedLocationKey?`, `confidence/reason` ; jamais de valeur sans provenance |
| `GameCandidate` | `candidateId` dans un espace de noms distinct, `platformKey?`, `displayTitle?`, `fileLocationKeys[]`, `variantCandidates[]`, `evidenceRefs[]`, `resolution=CONFIRMED_BY_SOURCE|CANDIDATE|AMBIGUOUS` ; **une observation n'est pas automatiquement un jeu** |
| `MediaReference` | `kind`, `relativePath` et `domain` autorisés, `sourceKind`, `associationStatus`, `availability` et `diagnosticRefs[]` ; aucune URL distante ni chargement automatique |
| `Diagnostic` | `code` stable, `severity=INFO|WARNING|ERROR`, `scopeRef` opaque, `reason` neutre, `sourceKind` et `observedAtUtc` ; pas de chemin complet/secret dans les logs publics |

**Distinctions impératives :** `FileObservation` est une ligne de constat physique daté ; `romUnitId` désigne une unité logique ROM selon le contrat métier historique et n'est pas un alias de `FileId` ; `GameId` désigne un jeu logique confirmé par une source/règle explicite, `VariantId` une variante, `MediaReference` un fichier associé. Un fichier peut être un disque d'un ensemble multidisque, un sidecar, une playlist, une archive potentielle ou un média — pas forcément un jeu. Plusieurs fichiers peuvent constituer un jeu ; un jeu peut avoir plusieurs variantes. Les comptes « fichiers observés », « jeux confirmés » et « groupes candidats » restent séparés. Aucune double comptabilisation d'une playlist et de ses membres en jeux par défaut.

### Identité, stabilité et portabilité

- `librarySessionId` est stable **pendant la session** ; l'identité durable entre redémarrages requiert une future association vérifiée, pas une déduction par nom de lecteur/lettre USB.
- `observationId` peut être propre à une exécution et ne doit **jamais** être utilisé comme clé persistante inter-scans. Ne pas contredire l'invariant historique « fileId stable au plus dans un scan ».
- `locationKey` est une clé de rapprochement *candidate*, dérivée d'un identifiant logique de bibliothèque *déjà établi*, du domaine et du chemin relatif. Normalisation documentée (séparateurs `/`, préfixes de longueur ou encodage sans collision, tri ordinal ; comparaison des chemins selon les règles Windows du volume et détection explicite de collisions de casse). Le déplacement validé de racine conserve la clé **si le contenu/la portée sont vérifiés** ; un renommage ou un déplacement interne ne prouve **aucune** identité de fichier conservée.
- Les `GameId` / `VariantId` existants ne sont ni recréés automatiquement à partir des titres ni fusionnés silencieusement entre sources. Les `candidateId` sont namespacés, provisoires et non interchangeables avec les identités émises par ES-DE.
- `contentFingerprint` est l'empreinte **d'une projection d'observations et de sa portée**, pas un SHA-256 du contenu des ROMs. Elle peut changer pour taille, disponibilité, métadonnées ou couverture ; elle ne démontre ni octets identiques, ni déplacement valide à elle seule.

## 3. Plateformes, types et ensembles

`PlatformResolver` repose d'abord sur une **convention de dossiers configurée et versionnée** : par défaut proposé `ROMS/<platform>/...` ; les alias (`gb`, `gba`, `psx`, etc.) ne sont adoptés qu'après décision documentée, jamais extrapolés silencieusement. Si le dossier ou l'alias n'est pas reconnu : `UNKNOWN_PLATFORM`, sans prétendre identifier une console via la seule extension. Pas de fusion de deux plateformes parce que leurs noms se ressemblent.

`FileClassifier` utilise une table d'extensions versionnée, sans exécuter les fichiers : `POTENTIAL_ROM`, `PLAYLIST`, `ARCHIVE_CANDIDATE`, `SIDECAR`, `MEDIA`, `UNKNOWN`. Casse des extensions sans importance ; `.bin`, `.iso` et `.zip` restent ambigus. Pas d'inspection/décompression d'archives ni de confiance accordée au nom, à l'extension ou au titre comme preuve de jeu. Les formats multi-disques (notamment `.m3u`) sont des **relations candidates** entre unités existantes ; lecture du texte bornée, résolution sûre de références, refus d'URL/chemins absolus/sorties de racine ; si membres absents, conserver le constat et un diagnostic. Ne pas compter une playlist + deux disques comme trois jeux.

## 4. XML et médias : options, jamais prérequis

**Sans ES-DE :** inventaire autorisé → plateforme, fichiers, tailles/horodatages disponibles, classement prudent, diagnostics ; titre peut rester `null` ou un **libellé d'affichage issu du nom de fichier** clairement étiqueté `FILENAME_FALLBACK`, jamais une métadonnée vérifiée. Aucune jaquette « supposée ». `GameCandidate` peut rester non résolu ; ne pas afficher un faux catalogue de jeux confirmés.

**Avec ES-DE :** lecture des XML via l'adaptateur existant, sans écriture ; associer les métadonnées **seulement** sur plateforme + domaine + chemin relatif canonique dont l'équivalence est validée. Conserver séparément le titre observé du fichier, la description importée, la provenance ES-DE et les conflits. Une entrée XML sans fichier disponible donne `REFERENCED_NOT_OBSERVED`, **jamais `DELETED`** ; un fichier sans entrée XML reste une observation, **jamais une erreur effaçante**. Deux XML concurrents ou deux correspondances plausibles → `AMBIGUOUS_METADATA`, pas choix automatique.

**Médias :** références ES-DE importées si disponibles ; en mode sans XML, découverte de médias **hors lot par défaut**, sauf convention `MEDIAS` explicitement activée et bornée dans le contrat d'implémentation suivant. Associer uniquement par correspondance prouvée/choisie, jamais par nom seul en prétendant à la certitude. Référence absente, inaccessible, illisible ou format non pris en charge → placeholder + diagnostic, sans réseau ni suppression. Pour ouvrir un média, vérifier à nouveau sa provenance, la racine, les reparse points, la validité du chemin et l'accessibilité **au moment de l'ouverture** ; fournir au consommateur un flux en lecture seule, à fermer par le consommateur. Ne pas loguer les chemins sensibles.

## 5. Sécurité et confidentialité non négociables

- **Opt-in explicite** : aucune énumération au démarrage, aucun lecteur/dossier auto-détecté ; sélection d'un répertoire local autorisé, racines ROMS/MEDIAS distinctes si nécessaire et validées. Ne pas interpréter une racine racourcie/absente comme bibliothèque vide.
- **Read-only** sur source : aucun mkdir, fichier d'index, modification de timestamps volontaire, XML/ROM/média écrit, copie/export, nettoyage, renommage, suppression ou hachage binaire systématique.
- **Confinement** : canoniser avant ouverture et revalider les chemins après résolution ; refuser traversée `..`, chemins absolus, URI, UNC/réseau, URL, jonctions/symlinks/reparse points dans les racines et sous-chemins ; refuser race évidente de substitution à l'ouverture plutôt que suivre un lien.
- **Bornes** : profondeur, nombre de fichiers, taille des textes de playlists/XML, temps, taille des répertoires traités, mémoire et annulation explicites ; atteinte de limite = `PARTIAL` / `LIMIT_REACHED`, jamais `COMPLETE`. Aucun contenu binaire de ROM ouvert pour analyse ; les métadonnées de `FileInfo` ne sont pas un hash de contenu.
- **Erreurs contrôlées** : fichiers/dossiers illisibles, exceptions, stockage éjecté, accès refusé, collision de casse, chemin invalide, fichier disparu pendant l'énumération → diagnostics et état partiel/échec ; ne pas masquer la défaillance comme « zéro ».
- **Confidentialité** : pas d'Internet/télémétrie/upload, aucun hash/nom/chemin de ROM réelle, BIOS ou média tiers dans GitHub, CI, captures, logs publics ou issues. Les tests créent uniquement des noms, octets et XML **entièrement fictifs** dans des dossiers temporaires. Les diagnostics publics emploient des identifiants anonymisés.

## 6. Statuts, couverture, absence et mise à jour

Trois dimensions indépendantes : `scanStatus=COMPLETE|PARTIAL|FAILED`, `availability=ONLINE|OFFLINE|INACCESSIBLE|UNKNOWN` et `freshness=FRESH|STALE|UNKNOWN`. Un état `COMPLETE` signifie uniquement que **la portée déclarée et toutes ses partitions attendues** ont été énumérées sans omission connue ; il **ne prouve pas** que tous les jeux existants ont été identifiés. `coverageProof[]` porte les partitions (`platformKey`, `sourceKey`, `rootKey`, `domain`) et `status=COMPLETE|PARTIAL|UNOBSERVED` ; absence de partition attendue interdit COMPLETE. Racine initiale absente/inaccessible = FAILED, pas COMPLETE vide ; dossier accessible et réellement vide peut être COMPLETE avec zéro observation.

- `observedFileCount` décrit le nombre **effectivement énuméré** ; `totalFileCount` ou `confirmedGameCount` = `null` lorsque l'exhaustivité ou l'association logique n'est pas démontrée. `0` n'est pas synonyme d'inconnu.
- Une observation datée d'un fichier introuvable ou d'un XML sans chemin résolu est `UNAVAILABLE` / `NOT_OBSERVED` ; **aucun état `DELETED` ni action de suppression**. Même deux scans complets ne constituent qu'une absence observée dans des zones prouvées, jamais preuve d'effacement physique.
- Préparer le nouveau snapshot **avant** publication. Un scan `FAILED`, `PARTIAL`, annulé ou devenu inaccessible **ne remplace pas silencieusement** le dernier snapshot complet utile. Conserver sa date/portée et marquer son éventuelle obsolescence ; permettre l'affichage d'observations partielles uniquement avec étiquetage explicite, et jamais des compteurs exhaustifs.
- Réimport d'une portée identique et accessible : ordre déterministe, mêmes observations/provenances → résultat équivalent ; variation du fichier, de la source ou de la couverture explicitée. Déplacement d'un SSD : racine nouvellement choisie et correspondance vérifiée, pas simple égalité de lettre ou chemin.

## 7. Doublons potentiels — aucune conclusion automatique

- **N0, signal faible** : ressemblance de nom ou de titre, avec taille éventuellement égale, **ne prouve rien** ; conserver séparément régions, éditions, disques et plateformes.
- **N1, candidate réversible** : plusieurs observations correspondant à des critères configurés (par exemple même plateforme + taille connue + nom rapproché / extension) produisent un `POTENTIAL_DUPLICATE` avec liste de preuves, niveau de confiance limité et motifs ; les disques d'une playlist et les homonymes interplateformes ne sont jamais fusionnés.
- **N2, preuve d'égalité binaire** : hors du lot initial. Hachage exact uniquement après mandat explicite, budget I/O, consentement local et définition des exclusions ; même hash binaire ne prouve pas qu'il s'agit du même *jeu logique*.
- **Interdits** : déduplication, suppression, fusion d'identités, édition de métadonnées ou choix d'une ROM « meilleure » automatiques.

## 8. Interface DEV 1 ↔ catalogue ↔ UX et décisions à geler

DEV 1 peut implémenter indépendamment les contrats `InventoryScope`, `InventorySnapshot`, `FileObservation`, `InventoryDiagnostics` et fournir une **méthode de lecture en mémoire** / cancellation + fixtures. DEV 2 valide la forme de l'adaptateur `InventorySnapshot → CatalogueReadModel` en distinguant `observations`, `gameCandidates`, `confirmedGames`, `metadataEvidence`, `mediaReferences`, `coverage`, `diagnostics` et `countsKnown`. DEV 3 consomme la projection mais **aucun changement de #64** ne découle automatiquement du présent contrat.

Questions explicites à résoudre en revue DEV 1, avant gel de l'API :
1. Choix d'un nom/namespace et d'une **version de schéma**, sans collision avec `EsDeGame`, `EsDeCatalogueSnapshot` ou le DTO historique `0.2`.
2. Sémantique exacte de `FileId` : **observation localisée et datée**, non identité persistante ; `locationKey` séparée et limitée à des comparaisons prouvées.
3. Déclaration/versionnement des plateformes/aliases/extensions et résultat `UNKNOWN` si non configuré.
4. Enveloppe des résultats `COMPLETE/PARTIAL/FAILED`, couverture par partition, compteurs inconnus et codes de diagnostics **testables**.
5. Cas `.m3u`/archives et limites de ressources : les bornes numériques sont une **décision d'implémentation** soumise aux canaris, pas une promesse issue du présent document.

La **compatibilité de lecture avec le POC synthétique** doit être maintenue. Le mode importé NEXT-01-A peut rester un adaptateur ; l'UI ne doit pas assimiler une observation FS sans métadonnées à un jeu complet, ni afficher une comparaison A/B synthétique comme réalité de bibliothèque.

## 9. Critères d'acceptation proposés (tests À ÉCRIRE — NOT_EXECUTED)

| ID | Fixture entièrement fictive / oracle de réception |
| --- | --- |
| T01 | `ROMS/gb/one.gb` sans XML → une observation, diagnostic/jeux candidats honnêtes ; aucun appel obligatoire à ES-DE |
| T02 | `gba`/`psx` + sous-dossiers + alias configurés → plateforme versionnée, `UNKNOWN` si inconnu, pas de fusion |
| T03 | `.GBA`, `.bin`, `.ISO`, `.zip`, `.m3u`, `.txt` → classification candidate, archives non ouvertes |
| T04 | Deux éditions homonymes et deux plateformes → deux observations ; aucun `GameId` confirmé inventé |
| T05 | Noms/tailles proches → `POTENTIAL_DUPLICATE` seulement, preuves et reversibilité, pas d'écriture |
| T06 | Playlist `.m3u` + deux fichiers disque → 3 objets fichiers observables mais **pas 3 jeux** ; membre manquant diagnostiqué |
| T07 | Racine accessible vide vs racine absente → `COMPLETE + 0` dans le premier cas, `FAILED + null` dans le second |
| T08 | Lecture partielle, accès refusé, SSD débranché → `PARTIAL/FAILED`, `totalFileCount=null`, dernier complet conservé ; aucune suppression |
| T09 | Junction/symlink/reparse point (racine, enfant, média) → refus contrôlé, pas de suivi hors racine |
| T10 | Traversée, chemin absolu/UNC/URI, collision de casse → refus et diagnostics, aucune ouverture distante |
| T11 | Deux scans identiques → résultats stables à portée égale ; une observation modifiée → différence datée sans attribution de suppression |
| T12 | Annulation, limites de taille/profondeur/temps/fichiers → `PARTIAL` signalé, pas de remplacement silencieux |
| T13 | 2 000 fichiers fictifs temporaires → rapport durée/mémoire seulement, **aucun PASS performance anticipé** |
| T14 | Absence totale de `gamelist.xml` → catalogue d'observations utilisable et aucune exception due au XML manquant |
| T15 | XML synthétique présent + fichier non référencé + entrée XML absente du disque → provenances conservées, `REFERENCED_NOT_OBSERVED`, pas de perte d'observation |
| T16 | Média correct, absent, illisible et référence forgée → flux local read-only ou placeholder, confinement revalidé |
| T17 | Réassociation vérifiée du même SSD / changement de lettre et réimport après échec → identité de session préservée si preuve, dernier résultat utile intact |
| T18 | Tests de non-régression ES-DE NEXT-01-A, mode démo/UX #64 et invariants de couverture historique ; aucun test nouveau déclaré PASS sans run exact |

**Preuves exigées pour une future PR de code :** HEAD exact, CI applicable, PASS/FAIL et SKIP réels, fixtures synthétiques contrôlées, mesures si performance revendiquée, captures purement fictives si UX, aucune donnée privée ni téléchargement de ROM, revue indépendante et décision de fusion par le Contrôleur.

## 10. Limites et non-objectifs de NEXT-02-CONTRACT

Ce contrat **ne livre ni scanner utilisable, ni catalogue persistant, ni mapping complet des consoles, ni détection binaire des doublons, ni scraper, ni collection personnelle, ni export/copie/suppression, ni upload, ni Release**. Il ne décide pas SQLite, stockage entre redémarrages, politique complète des médias sans XML, recherche exhaustive d'archives ni valeur définitive des bornes. L'inventaire sur une ROMothèque réelle ne sera envisagé **qu'après validation des fixtures et GO propriétaire explicite**.

**Livrable DEV 2 :** contrat proposé pour revue et alignement DEV 1 ; aucune modification de code et aucun remplacement automatique des contrats normatifs existants.
