# PERSIST-01 — Stockage SQLite synthétique (NEXT-05 / #51)

**Phase : backend fixtures uniquement.** Aucun catalogue personnel, scan ROMS/MEDIAS, import ES-DE, aperçu média réel, fusion ni Release. `ICatalogStore` n'est pas branché à l'interface et le constructeur public `SqliteCatalogStore()` refuse l'accès (`REAL_CATALOG_STORAGE_HOLD`). Seul le constructeur `internal` utilisé par le test peut créer `%TEMP%/rm-persist01-fixture-*/ROMOTHEQUE_MANAGER/data/catalog.sqlite`.

## Contrats et tables

`CatalogStore.cs` contient `Library` (UUID), `StorageRoot` (UUID, rôle ROMS/MEDIAS, binding relatif, pointeurs CurrentComplete/LastAttempt), `ScanSnapshot` (UUID durable + scan ID éphémère attribué et manifest JSON versionné), `MetadataClaim` (valeur/provenance/source, idempotence source+clé externe+champ) et `UserDecision` (UUID manuel, choix, supersession/révocation conservés). Aucune table `Game` n'est créée automatiquement ; `FOUND_UNLINKED` et `AMBIGUOUS` restent des candidats, non des décisions.

`SaveAttempt` enregistre observations, couverture et `LastAttemptSnapshotId` en **une transaction** et avance `CurrentCompleteSnapshotId` uniquement si la couverture est entièrement Complete, Online, Fresh et le total cohérent. Si Partial, Failed, Offline ou Stale : sauvegarde de la tentative sans remplacer Current, sans supprimer de décision. Une exception entre insertion et bascule provoque le rollback de l'ensemble. Manifest JSON v1 limité à **16 Mio et 30 000 observations**, sans fichiers binaires ni chemins absolus. Le scan n'est jamais appelé par le stockage.

`LoadLastKnown` lit SQLite exclusivement et renvoie l'état conservé avec `DisplayAvailability=Offline` et `DisplayFreshness=Stale`, sans revalidation de volume, accès ROMS/MEDIAS, import XML, image ni réseau. Les `StorageRootId` et décisions sont des UUID durables distincts des IDs d'observation de scan. Un SSD déplacé ne peut pas être rattaché automatiquement par sa lettre ni par le nom de la bibliothèque.

## Journal / backup / migration / portable

- Choix initial **`journal_mode=DELETE`**, **`synchronous=FULL`**, FK activées et connexions SQLite fermées après chaque transaction ; **choix provisoire sur fixtures** à réévaluer après essais de retrait d'un **support de test**. Aucune garantie contre panne du contrôleur USB ou perte d'alimentation.
- Version SQLite `user_version=2` après migration transactionnelle v1→v2 (champ `MetadataClaim.RuleVersion`). Une base v1 reçoit **d'abord un backup cohérent via l'API SQLite**, puis la migration ; versions supérieures refusées. Le manifest JSON a **sa propre version**.
- `BackupVerified` utilise `SqliteConnection.BackupDatabase` dans un fichier temporaire, exécute `PRAGMA integrity_check`, puis publication atomique sous `ROMOTHEQUE_MANAGER/backups`. `RestoreVerified` refuse un chemin hors de cette fixture, vérifie la sauvegarde, conserve une copie de sécurité, vérifie la restauration temporaire avant remplacement et relance la validation du schéma.
- Distribution : **Microsoft.Data.Sqlite 10.0.14** + SQLitePCLRaw 2.1.13, versions/empreintes en `packages.lock.json` app et tests (résolution Windows réelle). Vérifier les licences du provider MIT et les avis tiers/native SQLite dans le gate Windows ; **aucun contenu de collection dans GitHub/artefacts publics**.
- Emplacement cible futur : local application data hors ROMS/MEDIAS ; mode portable `ROMOTHEQUE_MANAGER/data` **uniquement sous choix explicite et gate de sécurité**. `PortableAutoActivation=false`; aucun accès simultané réseau/cloud/multi-PC.

## Matrice PERS-01…08

| ID | Preuve synthétique / portée | Limite |
| --- | --- | --- |
| PERS-01 | Sauvegarde ROMS/MEDIAS Complete + 2 claims/1 décision, fermeture puis **second processus vstest** sans racines, réouverture Offline/Stale | vrai SSD NON EXÉCUTÉ |
| PERS-02 | Partial/Failed/Offline conservent Current et décisions | essais sans scan réel |
| PERS-03 | exception injectée avant commit : rollback snapshot/pointeurs | crash électrique NON EXÉCUTÉ |
| PERS-04 | claims ambigus n'impliquent aucun UserDecision ; choix explicite, révocation et historique au redémarrage | UI non raccordée |
| PERS-05 | rejects chemins absolus/rebind implicite, storage default local candidat, option portable désactivée | changement de lettre physique NON EXÉCUTÉ |
| PERS-06 | online backup, corruption d'une copie, restore, migration v1→2 et integrity_check | pas de corruption vraie DB |
| PERS-07 | simulation erreur disque avant commit, état préservé | débranchement réel NON EXÉCUTÉ |
| PERS-08 | provider natif Windows fonctionne, journal DELETE, gardes public stockage/preview ; pipeline de publication/empreinte ZIP | SHA ZIP uniquement après CI Windows |

**Gates pour intégration :** tests PERS-01…08 + anciens tests en CI locked, licence/transitifs vérifiés, logs Windows/manifest ZIP avec `e_sqlite3`, revue DEV 2 indépendante HEAD exact, puis seul GO Contrôleur pour fusion. La **recette sur SSD personnel** exige un mandat supplémentaire et n'est pas permise par cette PR.
