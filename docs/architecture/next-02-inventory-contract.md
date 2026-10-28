# NEXT-02 — Contrat minimal P1 : observation de fichiers sans ES-DE

**DEV 2 / Contrôleur — 28.10.2026 — DOCUMENT CONTRACT / DRAFT / pas de scan réel.**
Références : [mandat #65](https://github.com/TobbyCy/RomothequeManager/issues/65), [PR DEV 1 #68](https://github.com/TobbyCy/RomothequeManager/pull/68), [contrat NEXT-01-A #62](https://github.com/TobbyCy/RomothequeManager/pull/62), [UX #64](https://github.com/TobbyCy/RomothequeManager/pull/64).
**Périmètre volontairement réduit** après réserve du Superviseur sur la complexité. La proposition d'architecture et de parcours étendus du HEAD documentaire précédent `41f39b6650a3ba53f2473efc087303aaf1398a8e` reste consultable dans l'historique Git, **mais n'est pas un contrat normatif pour NEXT-02 P1**.

## 1. Résultat obligatoire et frontières

Sur demande explicite d'un appelant, lire **une racine locale sélectionnée contenant `ROMS/`**, même sans aucun `gamelist.xml`, et retourner des **observations de fichiers** sans toucher au disque source. Un fichier n'est ni un jeu logique ni un média ES-DE confirmé. N'introduire aucun `GameId`, `VariantId` ou `romUnitId` à partir d'un nom, d'une extension ou d'un chemin.

**Aucun appel à `EsDeCatalogueImporter`**, aucune connexion réseau, lecture d'une bibliothèque réelle, scan automatique, modification de ROM/XML/média, extraction d'archive, hash binaire de ROM, déduplication ou suppression. La PR DEV 1 reste indépendante de #64 et du Dashboard VISUAL-POC-01 #67.

## 2. Interfaces minimales à valider entre DEV 1 et DEV 2

Les noms ci-dessous correspondent à la **candidate implémentée dans PR #68 HEAD `1771fd2a80b5b43f0500721be31a22a0483ea04b`** ; signature finale soumise au `PEER_ACK` explicite de DEV 1 et à la revue technique.

| Contrat | Champs et sémantique minimaux |
| --- | --- |
| `InventoryScope` | `LibraryRoot` **explicitement sélectionné**, local, contenant `ROMS/` ; alias plateformes éventuels explicitement fournis ; budget de profondeur, fichiers, répertoires, taille/lignes des playlists et durée. Aucun dossier réseau/UNC, URI ni lecture hors racine. |
| `InventorySession.Scan(CancellationToken)` | Déclenche **le seul** parcours de fichiers ; l'annulation doit produire un résultat contrôlé, y compris à l'intérieur des playlists. `Current` ne change que sur `Complete` ; `LastAttempt` rend visible le dernier essai `Complete/Partial/Failed`. Aucun état durable inter-session imposé. |
| `InventorySnapshot` | `SchemaVersion, LibrarySessionId, ScanId, CapturedAtUtc, ScanStatus, Availability, CoverageProof, Observations, Diagnostics, ObservedFileCount, TotalFileCount?`. Autres champs candidats non requis pour la réception P1 (jeu confirmé, empreinte de projection, fraîcheur, playlists, doublons). |
| `FileObservation` | `ObservationId` propre à **ce scan**, `LocationKey` candidate de rapprochement **dans la session seulement**, `PlatformKey` ou `UNKNOWN`, `RelativePath` sous `ROMS/`, type prudent, extension, taille/date si observées, provenance `FILESYSTEM_OBSERVATION`. Aucune identité de jeu. |
| `InventoryDiagnostic` | Code stable, sévérité, référence de portée, raison neutre, provenance et date ; pas de nom complet privé/secret dans les preuves publiques. |
| `CoverageProof` | Au minimum portée `ROMS` + plateformes effectivement énumérées ; ne jamais déclarer une portion `Complete` si inaccessible, sautée, interrompue ou non explorée. Ne pas confondre `Complete` sur `ROMS/` et collection personnelle exhaustive/jeux identifiés. |

### États et compteurs (normatifs)

- `Complete` : toute la **portée autorisée déclarée** parcourue sans omission connue, y compris un dossier accessible vraiment vide ; `TotalFileCount=ObservedFileCount`, éventuellement `0`.
- `Partial` : limite, annulation, répertoire/fichier sauté ou parcours incomplet ; conserver les observations obtenues, `TotalFileCount=null`. Un échec après un parcours commencé est au minimum `Partial` et diagnostiqué, jamais `Complete`.
- `Failed` : racine/`ROMS/` indisponible ou vérification initiale impossible ; `TotalFileCount=null` et diagnostic ; ne jamais produire un faux « zéro fichier/jeu » complet.
- `Availability` est **indépendant** de `ScanStatus` : support OFFLINE/INACCESSIBLE ne signifie pas fichier supprimé. **Absence dans un nouveau scan ≠ suppression**, même après deux scans.
- `Current` conserve le dernier `Complete` si un nouveau `Partial`, `Failed` ou scan annulé survient ; `LastAttempt` expose le résultat prudent pour diagnostic, sans effacer l'historique utile. Aucune tentative interrompue ne doit s'échapper sans résultat contrôlé alors que les ressources sont ouvertes.
- `ObservedFileCount` = observations effectivement collectées ; `TotalFileCount` = `null` sauf couverture complète. `ConfirmedGameCount`, s'il est présent dans le modèle candidat, **reste `null`** : NEXT-02 n'identifie pas des jeux.

### Sécurité P1

Lecture seule des métadonnées de fichiers et, pour `.m3u` seulement, du texte borné ; aucune ouverture de contenu ROM pour hachage/comparaison. Refus des liens symboliques/jonctions/reparse points à l'énumération, racines UNC/URI et chemins de playlists hors portée. Contrôle des limites et de l'annulation **pendant** l'énumération et les lectures auxiliaires, sans charger un fichier texte devenu énorme après le contrôle de taille. Les courses du système de fichiers doivent être converties en diagnostics/`Partial` plutôt qu'en plantage non traité ; la protection complète face à une substitution concurrente de reparse point reste une **réserve avant toute utilisation sur SSD réel**, pas un motif pour inventer une sécurité démontrée.

## 3. Acceptation de la première PR moteur (fixtures synthétiques uniquement)

| Oracle P1 | Attendu |
| --- | --- |
| P1-01 — Aucune gamelist | `ROMS/gb/one.gb` sans XML : une observation, provenance FS, pas de `GameId`, aucun import ES-DE. |
| P1-02 — Vide vs inaccessible | Répertoire `ROMS` accessible vide = `Complete` + `0` ; absent = `Failed` + `null`. |
| P1-03 — Fichier et plateforme | Identité `ObservationId` liée à un scan ; `LocationKey` session-scoped ; plateforme non configurée = `UNKNOWN` ; extension ambiguë ≠ jeu confirmé. |
| P1-04 — Partiel/erreur | Limite fichiers, accès refusé, support perdu : `Partial/Failed`, diagnostic, `TotalFileCount=null`, `Current` précédent intact. |
| P1-05 — Annulation réelle | Annulation **en cours de parcours et pendant une playlist `.m3u`** : pas d'exception non maîtrisée, handles fermés, état `Partial`/`LastAttempt` conservé. Le seul test d'un token pré-annulé ne suffit pas. |
| P1-06 — Chemins et liens | UNC/URI, sortie de portée, symlink/junction : refus contrôlé ; test de lien explicitement `NOT_TESTED` si l'hôte ne permet pas de créer le lien, **pas un PASS implicite**. |
| P1-07 — Source intacte | Aucun fichier ROM/XML/média modifié ; toutes fixtures, noms et octets sont fictifs ; aucun upload, screenshot ou hash ROM privé. |
| P1-08 — Régression | CI liée au **HEAD exact** : succès des tests NEXT-02 + tests existants ; limites et branches non exécutées listées honnêtement. |

**Preuves déjà disponibles pour la candidate #68** : [CI #40674053849](https://github.com/TobbyCy/RomothequeManager/actions/runs/40674053849) SUCCESS, **64 MSTest PASS dont 18 NEXT-02**. **Limites actuelles à traiter avant avis technique final :** l'annulation au milieu du parsing de playlist lève actuellement `OperationCanceledException` ; `File.ReadAllLines` après précontrôle de taille n'offre pas de borne si le fichier grossit entre-temps. Le canari de lien peut se terminer sans tester le refus. Une CI verte ne démontre pas ces cas.

## 4. Retenues hors du P1 / à réexaminer séparément

**Reporter sans bloquer la première PR moteur** : `InventoryProgress` / `IProgress`, `InventoryRunResult` séparé, modèle `InventoryPresentation`, adaptateur `CatalogueReadModel`/T3/T4, jointure ES-DE ↔ fichiers, identité jeu/variante/double disque, scan multi-root, découverte/enrichissement de médias, classement avancé, SQLite/persistance inter-redémarrages, vrai comparateur A/B, télémétrie, design d'un parcours Avalonia, indicateurs Dashboard, règles globales de duplication, validation cryptographique de relocalisation et optimisation produit sans mesure. Les playlists et doublons **candidats** déjà inclus par DEV 1 restent acceptables s'ils ne fragilisent pas les invariants P1 ; aucun périmètre additionnel n'est exigé.

**Parcours UX futur, non implémenté :** choisir localement → afficher périmètre et limites → cliquer volontairement « Inventorier en lecture seule » → voir un état d'exécution sans pourcentage trompeur → annuler → consulter les fichiers observés, diagnostics et `Complete/Partial/Failed`. Le moteur ne dépend d'aucune fenêtre Avalonia. La future projection distinguera **fichiers observés / jeux logiques / métadonnées ES-DE** et ne fera jamais passer des fichiers inconnus pour des jeux. Toute interaction UX nouvelle, notamment VISUAL-POC-01, exige un mandat distinct de DEV 3.

## 5. Validation par pairs et règle de gel

**DEV 1 :** répondre expressément `PEER_ACK` **sur le HEAD de cette PR #66** si les cinq noyaux (scope, observations, statuts, diagnostics, annulation) ci-dessus sont acceptés, ou `CHANGES_REQUIRED` avec écarts limités à P1. DEV 2 publiera sa revue technique de #68 avec le HEAD exact et les éventuelles corrections nécessaires. **PEER_ACK demandé ≠ reçu** ; aucune signature gelée avant accord écrit et revue de la PR d'implémentation.

**Contrôleur :** seul décide du merge et d'un éventuel GO d'essai réel. Pas de scan réel, pas d'UI, pas de fusion ni Release autonome.
