# NEXT-05 / MI-A — Scanner MEDIAS à handles Windows (fixtures)

**Statut : CANDIDAT BACKEND / PR DRAFT / aucune autorisation réelle.** Contrat [Produit #115](https://github.com/TobbyCy/RomothequeManager/issues/115) ; avis de conception DEV 2 `MI_A_CONTRACT_APPROVABLE`. Code RS-A #114 et UI-RSA #116 inchangés. Cette contribution ne modifie ni `MediaIndexApplicationService` public ni `MediaPreviewService`.

## Frontières et implémentation

- `MiAFixtureMediaSession` et `MiAReadGrant` **internal**, sans constructeur public, uniquement pour racines synthétiques `%TEMP%/rm-mia-fixture-{GUID}/MEDIAS` sur Windows. Identité native du handle racine (`VolumeSerial + FILE_ID_128`) revérifiée, version/génération, durée <= 5 minutes, révocation inter-thread ; **grant ROMS non convertible**.
- Réutilisation **mécanique** de `RsAAnchoredReader` pour le parcours des répertoires, sans en modifier le code ni les permissions. `NtCreateFile` relatif au handle parent pour les fichiers avec `FILE_READ_DATA` et attributs, sans chaîne `FileStream(path)`; lecture `RandomAccess.Read(handle)` et métadonnées obtenues sur le **même handle**.
- Refus des reparse/jonctions, chemins device/UNC/traversal/ADS, noms 8.3 suspects (`~`), normalisations NFD, trailing dots/spaces et composants réservés ; contrôle de `FILE_STANDARD_INFO.NumberOfLinks == 1` pour refuser conservativement les **hardlinks**. Les systèmes de fichiers/volumes ne garantissant pas les identités/attributs nécessaires sont **NO-GO**. Les restrictions sur les hardlinks peuvent refuser des fichiers pourtant légitimes.
- Caps **non élargissables**, même via injection interne : profondeur 8 ; 5000 fichiers rencontrés ; 10000 répertoires ; 30 secondes coopératives ; 262144 octets d'en-tête par fichier ; 8388608 cumulés lus ; taille annoncée <= 67108864 ; côté maximal 8192 ; pixels <= 32 millions ; un seul scan actif par racine. Dépassement ou refus = `Partial/Failed`, **jamais Complete artificiel**.
- PNG/JPEG classés à partir des en-têtes uniquement, WebP reconnu mais `UNSUPPORTED_FORMAT` sans dimensions ni décodage. Pas de Skia, thumbnail, hash complet, lecture ROM, réseau, scraping, écriture MEDIAS ni lien automatique à un jeu.
- `Current` complet historique conservé si `LastAttempt` échoue ; aucun stockage utilisateur ou grant persisté. Une lecture kernel bloquée n'est **pas** préemptée par `CancellationToken` ; USB physique, compte non-admin, ACL et exFAT demandent des tests ultérieurs.

## Canaris et preuve attendue

| Canari | Couverture implémentée | Réserve |
| --- | --- | --- |
| MI-A01 | Refus sans consentement, grant éphémère, expiration/révocation, version, capacité séparée | Redémarrage réel processus non encore couvert ; modèle nouveau contexte vérifié |
| MI-A02 | Chemins invalides, symlink, hardlink natif, parent swap tenté, final swap et 0 octet d'en-tête après substitution, rejet alias | 8.3 réel/privilege de création de liens selon runner ; races non exhaustives |
| MI-A03 | PNG valide, JPEG invalide, WebP non décodé, caps fichiers/cumul bytes/depth | Mesures réelles dans CI Windows requises |
| MI-A04 | Annulation en lecture via callback, historique conservé | I/O noyau bloquante non préemptée |
| MI-A05 | Remplacement de racine à chemin identique, offline simulé | Aucun débranchement physique |
| MI-A06 | Application/cache exclus, racine ROMS non sélectionnable, aucune association | Même SSD physique non testé |
| MI-A07 | **NOT_EXECUTED** | Non-admin, ACL NTFS/exFAT, matrices FS réelles |
| MI-A08 | Constructeurs/entrées production ROMS et MEDIAS fail-closed | ZIP natif à vérifier après CI |
| MI-A09 | **NOT_EXECUTED** | Retrait / reconnexion USB physique, équipement et GO distincts |

**Critère de revue** : CI Adaptive `ci-policy-gate`, véritables tests sur runner Windows, TRX/logs détaillés, description des races **tentées / réalisées**, diff disjoint de #116, ZIP Windows candidat extrait et SHA-256 calculé sur le HEAD de PR (merge virtuel précisé). Aucun PASS revendiqué avant exécution réelle. Les résultats doivent être notés `PASS/FAIL/NOT_EXECUTED` sans déduire de GO production. Revue DEV 2 non-auteur avant toute décision Contrôleur.

**HOLD** : `REAL_LIBRARY_SCAN_HOLD`, `REAL_MEDIA_SCAN_HOLD`, `REAL_MEDIA_PREVIEW_HOLD`, `REAL_CATALOG_STORAGE_HOLD`, `NEXT05_RELEASE_HOLD`. Ni support personnel, ni édition de sources, ni Release.

### P1-MI-A-ROOT-01 — provenance et invalidation inter-racines (post-#116)

Une instance MI-A n'est pas une identité de support durable. Chaque `SelectFixture` invalide atomiquement le `MiAReadGrant` précédent, attribue un **nouveau** `MediaRootRef` opaque et une nouvelle version/génération, puis vide `Current`, `LastAttempt` et les compteurs pour empêcher l'attribution de données de A à B. Aucun scan, accès de fichier ou autorisation n'est déclenché par cette sélection. La réouverture éventuelle de A exige un **nouveau consentement** ; un ancien `Current(A)` n'est pas automatiquement réutilisé sous la nouvelle sélection.

Les opérations en cours capturent `root/rootId/MediaRootRef/SelectionVersion/generation`. Les vérifications intermédiaires et la publication finale contrôlent à nouveau cet ensemble sous verrou : une opération d'A ayant perdu la course avec `SelectFixture(B)` peut retourner un constat local, mais ne **publie jamais** ce résultat dans le `Current` ou `LastAttempt` de B, même si la substitution survient après sa dernière lecture. Dans une **même sélection** inchangée, une tentative Partial/Failed/Cancel conserve bien `CurrentComplete`. `MediaRootRef` n'est ni chemin ni authentification d'un disque externe ; le contrôle d'identité native du grant demeure inchangé.

**Canaris supplémentaires MI-A10/11** : A Complete → Select(B) → B Ready sans résultats/droits A → refus/annulation B sans pollution ; B Complete ensuite B Cancel conserve uniquement B ; resélection A exige reconsentement ; changement A→B pendant un scan A interdit toute publication tardive. CI/canaris Windows/ZIP du nouveau HEAD obligatoires, `MI_A07` et `MI_A09` restent NOT_EXECUTED.

