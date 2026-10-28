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


## 11. NEXT-02-INTEGRATION : revue de compatibilité des interfaces (28.10.2026)

**Références vérifiées :** main au commit `2e828c7eaab0aa11385f98080183dada5455c982` ; HEAD #64 `e775cdd64522d45ee2fb848df232fa1f19023326` ; contrats T3/T4 du catalogue commun **candidats et non normatifs**. Les signatures de cette section sont **proposées, non implémentées** ; DEV 1 doit encore les confirmer avant gel. Aucune modification à #64, à VISUAL-POC-01 #67 ni aux contrats historiques.

| Interface existante | Ce qui est vérifié | Adaptation prévue / responsable |
| --- | --- | --- |
| `EsDeCatalogueImporter.Read` et `EsDeImportSession.Import` | Exigent un XML ES-DE pris en charge. Les données sont préparées avant substitution. | **DEV 1** : moteur `InventorySnapshot` autonome ; ES-DE reste une source de métadonnées optionnelle, jamais le scanner sans XML. |
| `EsDeCatalogueSnapshot` | `LibraryId, SourceKind, ContentFingerprint, Games, Diagnostics` ; `EsDeGame` : `GameId, VariantId, Platform, Title, RomRelativePath, RomExists, Description, Media, DiscPaths`. Aucun `FileObservation[]` ni couverture exhaustive. | **DEV 2** : adaptateur distinct `EsDeCatalogueSnapshot → MetadataEvidence[] + EsDeReferencedGame[]`. `Games.Count` compte les entrées ES-DE, pas les fichiers ni tous les jeux physiques. |
| `EsDeImportSession.TryOpenMedia` | Vérifie la provenance d'un `EsDeMedia` du snapshot et revalide le confinement à l'ouverture en lecture seule. | **DEV 1 + DEV 2** : conserver l'autorité du service source sur ses propres médias ; ne pas accepter arbitrairement une référence inventaire comme `EsDeMedia`. |
| `ImportedCatalogueUiState`, `MainWindow.Imported.cs` (#64) | Deux modes `Demonstration/Imported` ; `Snapshot` ES-DE, recherche/pages de 50, sélection `GameId` ; Dashboard/Collection/fiche/Bibliothèques lisent la même projection ES-DE ; A/B importé désactivé. | **DEV 2** : future façade neutre ; **DEV 3 ultérieurement** : adapter un mode explicite « Inventaire de fichiers » et de nouveaux états, sans changer #64 dans ce mandat. |
| DTO T3/T4 `0.2.0-ui-catalogue-candidate` et comparateur historique | Modèles conçus autour d'objets `games[]`, `romUnitId`, `snapshotId` / `generationId` et preuves de couverture ; version toujours candidate. | Ne pas convertir arbitrairement les fichiers inconnus en `games[]` ou `romUnitId` ; pas de substitution silencieuse au DTO 0.2 ou de provenance `SYNTHETIC_SNAPSHOT` sur données réelles. |

**Verdict de compatibilité :** `COMPATIBLE_BY_ADAPTER / NOT_TYPE_COMPATIBLE_AS_IS`. Un `InventorySnapshot` ne peut pas être affecté à la propriété ES-DE `Snapshot` de #64 ; il faut une façade nouvelle **versionnée**, consommable sans Avalonia, et des tests de parité séparés. Le lecteur ES-DE ne doit pas modifier ses invariants pour accueillir le moteur FS.

### 11.1 Interface proposée à DEV 1 (à valider, pas de code)

Signature candidate : `StartAsync(InventoryScope scope, IProgress<InventoryProgress>? progress, CancellationToken cancellationToken) → Task<InventoryRunResult>`.

- `InventoryScope` : racine et domaines validés après confirmation explicite ; stratégie/version plateformes et extensions, limites, choix de la portée. Aucun parcours avant confirmation.
- `InventoryProgress` : `scanId, phase, processedEntries, observedFiles, diagnosticCount, expectedEntries?, percent?, observedAtUtc`. Phases candidates `VALIDATING_SCOPE, ENUMERATING, CLASSIFYING, BUILDING_OBSERVATIONS, FINALIZING`. Compteurs monotones pour une même phase ; `percent=null` tant que le dénominateur exact n'est pas démontré ; pas de faux « 100 % ». Événements bornés/coalescés ; aucune dépendance Avalonia ou callback UI dans le moteur.
- `InventoryRunResult` : `runStatus=COMPLETED|PARTIAL|FAILED|CANCELED`, `stagedSnapshot?`, `diagnostics[]` et référence distincte du dernier snapshot utile. **`CANCELED` décrit le job**, pas un quatrième `scanStatus` ; une tentative annulée produit au plus des observations `PARTIAL` ou rien.
- `InventorySnapshot` : `scanStatus=COMPLETE|PARTIAL|FAILED`, état de support/fraîcheur et preuves de couverture ; un échec initial n'est jamais un snapshot vide complet. Publier un nouveau résultat complet seulement après validation de sa portée. Tout `PARTIAL` se consulte comme aperçu séparé, sans remplacer automatiquement le dernier `COMPLETE`.
- `CancellationToken` : arrêter au prochain point sûr, fermer les handles, conserver l'état historique ; **aucune promesse d'annulation instantanée**. Les valeurs numériques des limites, la cadence des événements et le schéma C# exact sont à fixer par DEV 1 avec oracles synthétiques.
- Une exception attendue d'accès/permission/éjection devient un diagnostic contextualisé et un `PARTIAL/FAILED` ; elle ne devient jamais un compteur zéro ou une suppression de fichier.

### 11.2 Façade de présentation indépendante d'Avalonia (cible candidate)

`InventoryPresentation(schemaVersion, sourceMode, librarySessionId, observedAtUtc, scanStatus, coverageProof, availability, freshness, observedFiles[], esDeReferencedGames[], gameCandidates[], mediaReferences[], diagnostics[], counts)`.

L'adaptateur ne lit **aucun** dossier et ne parse **aucun** XML : il convertit des résultats déjà qualifiés. `sourceMode=DEMONSTRATION|ES_DE_REFERENCES|FILESYSTEM_INVENTORY|COMBINED` est **nouveau et candidat**, pas un ajout immédiat à l'énumération `CatalogueMode` de #64.

| Champ de présentation | Règle vérifiable |
| --- | --- |
| `observedFilesCount` | Nombre **effectivement observé**, même pendant un résultat partiel ; toujours affiché « fichiers observés » et daté, pas « total connu de la ROMothèque ». |
| `totalFilesKnown / totalFileCount?` | `true` et entier uniquement si toutes les partitions de la portée sont `COMPLETE` ; sinon `false/null`. |
| `esDeReferencedGamesCount?` | Entrées `EsDeGame` **référencées par le XML** seulement si import ES-DE effectué ; `null` en l'absence de XML, jamais zéro implicite. |
| `gameCandidatesCount` | Groupes potentiels, distincts des fichiers et des jeux référencés. L'égalité entre ces compteurs n'est pas présumée. |
| `confirmedLogicalGameCount?` | `null` tant qu'une règle d'identité vérifiée ne permet pas de démontrer ce total logique ; ne jamais inventer un `GameId` à partir d'une extension ou d'un titre. |
| `mediaAvailableCount?` | Uniquement le périmètre de médias réellement observé ; zéro média ES-DE ne signifie pas zéro média sur le disque. |
| `lastAcceptedSnapshot / currentAttempt / currentAvailability` | Trois axes indépendants, chacun avec sa date et son périmètre ; support débranché ≠ suppression du catalogue historique. |

**Rapprochement ES-DE optionnel :** uniquement sous bibliothèque, plateforme, domaine et chemin relatif canonique vérifiés ; statut `MATCHED|REFERENCED_NOT_OBSERVED|OBSERVED_UNREFERENCED|AMBIGUOUS`, deux provenances conservées. Les titres, descriptions, variantes et médias ES-DE sont **des métadonnées importées**, pas des faits vérifiés par le scanner. Une ROM citée dans le XML et indisponible reste référencée ; un fichier non décrit par XML reste observé. Le mode sans XML conserve `FILESYSTEM_OBSERVATION` et aucune jaquette implicite. Toute collision reste un diagnostic.

## 12. Contrat fonctionnel du futur parcours utilisateur (aucune UI implémentée)

| Étape | Action explicite et état attendu | Erreur et protection |
| --- | --- | --- |
| UX-01 — Accueil | Démonstration ou dernier catalogue connu identifiés ; aucun scan au démarrage. États OFFLINE/STALE et historique datés. | Pas de « collection vide » si source absente. |
| UX-02 — Sélection | « Choisir une bibliothèque… » : sélecteur local ; racine logique ROMS, MEDIAS facultatif et domaines autorisés. | Annuler ne change rien ; UNC/URL, lien/jonction ou racine inaccessible = refus sûr. |
| UX-03 — Prévalidation | Afficher la portée, versions plateformes/extensions, lecture seule, limites et exclusions ; aucun scan. | Si périmètre invalide, « Démarrer » désactivé et explication claire. |
| UX-04 — Consentement | **« Inventorier en lecture seule »** : seul ce clic lance le scanner. L'import ES-DE, si choisi, est optionnel et identifié séparément. | Ni sélectionner un dossier, ni actualiser une carte, ni changer de lettre USB ne lance automatiquement un scan. |
| UX-05 — Progression | Phase courante, fichiers observés, diagnostics, bouton d'annulation et statut « en cours ». Progression **indéterminée** si total inconnu. | Pas de nombre définitif, faux pourcentage ou « jeux confirmés » pendant une lecture partielle. |
| UX-06 — Fin complète | `COMPLETE`, date et portée ; fichiers observés, plateformes, références ES-DE, candidats et médias **dans des rubriques distinctes**. | Dossier vide intégralement parcouru = « zéro fichier observé dans cette portée », pas « aucun jeu existant ». |
| UX-07 — Partiel/annulé | `PARTIAL/CANCELED`, zones non couvertes, causes, nombre observé non exhaustif ; « Réessayer » volontaire. | Ancien snapshot complet conservé et daté ; aperçu partiel séparé. |
| UX-08 — Échec | `FAILED` avec diagnostic actionnable (disque retiré, accès refusé, chemin dangereux, limite atteinte). | Aucun catalogue écrasé, aucun « zéro jeu », pas d'exception brute ni de chemin privé dans la preuve publique. |
| UX-09 — Consultation | Onglets « Fichiers observés », « Jeux référencés ES-DE » si import, « Groupes candidats », « Médias » si observés, « Diagnostics ». | Un fichier n'acquiert pas une fiche de jeu ni `GameId/VariantId` par simple présence. Une fiche fichier reste possible. |
| UX-10 — Réinventaire | « Réinventorier » et « Choisir un nouvel emplacement » restent deux actions confirmées ; réassociation vérifiée. | Relocalisation/échec ne provoquent ni scan caché ni inférence de suppression. |
| UX-11 — Médias | Ouvrir seulement après revalidation d'origine et confinement ; placeholder local pour média absent/illisible. | Aucun scraping, accès réseau ni liaison automatique sur nom proche. |
| UX-12 — Diagnostics | Codes stables, catégories `INFO|WARNING|ERROR`, résumé en français, contexte scope/plateforme, action sûre. | Éviter messages bruts, secrets, faux états « supprimé » et alarmisme. |

### 12.1 Frontière UI et dépendance DEV 3

- Réutiliser **ultérieurement** les pratiques de #64 : sélecteur explicite, mode démo isolé, recherche, pagination **50 éléments**, réimport conservateur, repli jaquettes et source unique entre vues. Ne **pas** faire passer une observation FS brute dans `EsDeCatalogueSnapshot` ni modifier `MainWindow` maintenant.
- Le futur Dashboard, Collection, Fiche, Bibliothèques et Diagnostics devront consommer **le même `InventoryPresentation` figé** ; les compteurs inconnus restent `null` et les jeux candidats n'apparaissent pas comme jeux confirmés. Les fichiers bruts ont une fiche fichier si besoin, pas une fausse fiche jeu.
- Progression et annulation représentent **un job transitoire**, distinct de la santé du support et des cartes du Dashboard. Ne pas intégrer de bouton ou de compteur à VISUAL-POC-01 #67 dans cette PR. Placement visuel, charte, logo/icônes et composition de la vue restent sous l'autorité de DEV 3 et du mandat #67.
- A/B réel reste désactivé tant que la correspondance `romUnitId` entre snapshots et la couverture intégrale comparée n'est pas démontrée. Les snapshots du mode Démonstration restent `SYNTHETIC_SNAPSHOT` uniquement.
- Pas d'exécution concurrente de deux scans du même scope ; boutons incompatibles désactivés, possibilité d'annuler, traitement explicite de la fermeture de la fenêtre. Focalisation clavier, information textuelle indépendante de la couleur, états accessibles au redimensionnement et diagnostics entièrement consultables sont des critères d'une **future** revue UX, pas des tests PASS aujourd'hui.

**Séquence des dépendances :** DEV 1 fige moteur/progression/couverture après revue → DEV 2 vérifie l'adaptateur et des fixtures de présentation → DEV 3 valide la consommation du contrat sans toucher au Dashboard #67 → Contrôleur autorise séparément, si opportun, l'implémentation UI. Aucune nouvelle branche UX ni changement à #64/#67 par DEV 2.

## 13. Scénarios de réception d'intégration, NON EXÉCUTÉS

| Cas | Résultat attendu sur fixtures 100 % inventées |
| --- | --- |
| INT-01 | Sans XML : fichiers visibles, `esDeReferencedGamesCount=null` et aucun `GameId` déduit ; aucune dépendance au parseur ES-DE. |
| INT-02 | Avec XML : références ≠ observations ; provenance de chaque valeur, fichier non référencé préservé et référence introuvable non supprimée. |
| INT-03 | Deux homonymes, playlist + deux disques : fichiers distincts, candidats explicites, pas 3 jeux imposés. |
| INT-04 | Modes démonstration, import ES-DE, inventaire FS et combiné isolés ; #64 et ses fixtures inchangées ; A/B fictif non présenté comme réel. |
| INT-05 | Dossier vide complet = 0 fichiers ; disque absent/partiel = total inconnu et non 0 ; dernier complet affichable. |
| INT-06 | Sélecteur annulé, racine invalide, retour arrière : aucun scan démarré ; consentement explicite obligatoire. |
| INT-07 | 2 000 fichiers inventés : phases, compteurs monotones, `percent=null` sans dénominateur prouvé ; mesures de cadence et mémoire seulement après run. |
| INT-08 | Annulation, éjection, limite et refus d'accès : handles libérés, résultat partiel/échec, diagnostics, dernière observation complète préservée. |
| INT-09 | Relocalisation simulée d'un SSD : validation avant réassociation ; pas de disparition de jeux ni d'auto-scan. |
| INT-10 | Média présent/illisible/absent/forgé : ouverture seulement via service source read-only, placeholder, pas de réseau. |
| INT-11 | Diagnostics en français, contextualisés, sans chemin privé, explication non alarmiste et `PARTIAL` honnête. |
| INT-12 | Non-régression des 15 cas ES-DE (#62), de la PR #64 (62 MSTest au HEAD de correctif lorsque démontrés) et des invariants historiques ; **les futurs PASS exigent un run sur le nouveau HEAD**. |
| INT-13 | Fichier sans jeu logique confirmé : fiche fichier possible, `confirmedLogicalGameCount=null`, aucun `romUnitId` fabriqué. |
| INT-14 | Recette Windows future : Tab/Shift+Tab, défilement, 760×480, 1120×720, DPI et annonces textuelles ; **NOT_TESTED**, mandat UI distinct requis. |

## 14. Revue croisée à obtenir avant gel

**À confirmer par DEV 1 sur #66/#65 :** signature moteur (`InventoryScope, InventoryProgress, InventoryRunResult`), états terminal/annulation, source du snapshot accepté/partiel, identité `observationId` vs `locationKey`, politique d'alias/extension et limites, rapprochements ES-DE sans création de `GameId`, 14 oracles INT. La confirmation vaut accord de contrat, **pas GO de scan réel**.

**À confirmer par DEV 3 (sans travaux sur VISUAL-POC-01) :** consommabilité future de `InventoryPresentation`, statuts de progression et d'erreur, présentation des compteurs séparés, pagination, source unique entre vues et absence de réutilisation d'A/B synthétique.

**Verdict DEV 2 à ce stade :** `INTEGRATION_CONTRACT_PROPOSED / PEER_ACK_PENDING`. Ce contrat documentaire ne fige aucune signature en désaccord, ne constitue pas un test du moteur et n'autorise ni changement UX, ni merge, ni Release, ni scan réel.
