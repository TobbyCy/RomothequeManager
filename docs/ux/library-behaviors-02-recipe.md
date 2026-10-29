# UX-CONTRACT-02 — recette fonctionnelle commune DEV 1 / DEV 3

**Référence normative de présentation :** [comportements des bibliothèques](library-behaviors-02.md), [oracles DEMO-01](../../contracts/demo-library-01/oracles.json), Issues [#3](https://github.com/TobbyCy/RomothequeManager/issues/3), [#8](https://github.com/TobbyCy/RomothequeManager/issues/8), [#11](https://github.com/TobbyCy/RomothequeManager/issues/11), [#65](https://github.com/TobbyCy/RomothequeManager/issues/65). **État initial de toutes les lignes : NOT_EXECUTED** pour la réception UX-CONTRACT-02 ; la CI antérieure du générateur/Produit ne valide pas ces scénarios d'interface.

## Protocole PASS/FAIL

Chaque ligne exige : **fixture fictive exacte** (ou état simulé depuis un index), `libraryId`, provenance (DEMO / ES-DE / INVENTORY), `scanId/generationId` si disponible, date, preuve de portée et screenshot/trace/UI accessible lorsque le parcours existe. DEV 1 fournit/valide les entrées et oracles de chaque scénario ; DEV 3 vérifie l'écran/navigation si la fonctionnalité est raccordée.

- **PASS** : comportement effectivement exécuté sur la version/SHA exacts et conforme **au libellé et aux données** ; preuve GitHub liée.
- **FAIL** : un effet observé viole une assertion obligatoire (ex. `0 jeux` à la place de `Inconnu`, `Supprimé`, jeu créé depuis un fichier, recherche change de bibliothèque, inventaire lancé automatiquement).
- **NOT_IMPLEMENTED** : parcours non disponible dans l'UI ou absence d'adaptateur autorisé. Ce n'est **pas** un échec du moteur ni un PASS UX ; créer une dépendance ciblée si un mandat ultérieur l'autorise.
- **NOT_EXECUTED** : test non joué, même si le code existe ; **BLOCKED** si fixture/index/recette hors ligne manque ou si un prérequis de sécurité interdit l'essai. Ne jamais transformer `SKIPPED` en `PASS`.
- Les **oracles de catalogue** restent indépendants des **observations du scanner**. Aucun test ne nécessite des fichiers personnels, un disque USB réel ou une ROM commerciale.

### Matrice commune (tous les attendus sont normatifs pour une future implémentation, pas déclarés PASS ici)

| ID | Précondition / action synthétique | Assertion observable PASS | Condition FAIL | Responsable de preuve |
|---|---|---|---|---|
| LB02-01 | Démarrage sur registre fictif, aucune sélection réelle | L'accueil explique DEMO/lecture seule, bibliothèques navigables sans scan, aucune opération disque externe | scan lancé ou source réelle touchée sans GO | D1 fixture / D3 UI |
| LB02-02 | Choisir `IDEAL` parmi trois bibliothèques | `libraryId`/nom et source/génération affichés ; sélection explicite visible | compteurs d'une autre bibliothèque | D3 |
| LB02-03 | `IDEAL` : charger XML puis observations ROMS `COMPLETE` séparément | `30 références ES-DE`, `32 fichiers observés`, `jeux identifiés par NEXT-02 : Inconnu` ; sources distinctes | `32 jeux`, fusion automatique, somme des compteurs | D1 oracle / D3 |
| LB02-04 | `IDEAL` : sélectionner Metal Deer Salad ID de fixture `011` | Une entrée ES-DE de jeu pour playlist, 3 fichiers ROMS (1 `.m3u` + 2 membres), pas 3 jeux | disque devient jeu automatiquement | D1 / D3 |
| LB02-05 | `IDEAL` : 13 slugs plateformes déclarés | Plateformes affichées avec source et portée ; `Hello!` référencé Xbox, `Jaxson & Dex` PS2, aucune console déduite de `.bin` | limite artificielle de huit ou mauvaise plateforme | D1 / D3 |
| LB02-06 | `IDEAL` : images chargées via ES-DE | Les 3 rôles ES-DE importables par jeu sont distingués des autres PNG de manifest ; aucun faux média « vérifié » | 180 médias « importés par ES-DE » sans preuve | D1 / D3 |
| LB02-07 | `CHAOS` : charger XML valide et observation `COMPLETE` | `30 références ES-DE`, `33 fichiers ROMS` ; 2 ROMs référencées indisponibles (`005`, `017`) avec diagnostic, jamais 28 jeux | éléments cachés, supprimés ou faussement confirmés | D1 / D3 |
| LB02-08 | `CHAOS` : 3 fichiers candidats à duplication (`004,013,027`) | Groupes libellés `Doublon potentiel`, motifs/provenances ; éditions/régions séparées | « doublon exact » sur nom/taille seuls, action supprimer | D1 / D3 |
| LB02-09 | `CHAOS` : 14 PNG absents du jeu de fixture | Fallback + diagnostic `Média indisponible` uniquement si attente déclarée et vérification faite ; conserver jeu/référence | écran vide assimilé à jeu absent, nombre de médias inventé | D1 / D3 |
| LB02-10 | `MYSTERY` : aucune XML, `32` fichiers ROMS | `Aucune métadonnée ES-DE` et `32 fichiers observés` ; jeux identifiés `Inconnu`, titres inconnus | `0 jeux`, `30 jeux détectés` ou import ES-DE forcé | D1 / D3 |
| LB02-11 | `MYSTERY` : `0` média physiquement présent dans la fixture | Différencier `0 fichier média dans l'oracle` et `médias attendus/inventoriés par l'app : Inconnu/non renseigné` | 180 médias « manquants » inférés sans XML | D1 / D3 |
| LB02-12 | `COMPLETE` sur `ROMS` uniquement, couverture des médias inconnue | Seuls les compteurs ROMS et plateformes couvertes sont conclus ; média/complet global non prouvé | badge « collection 100 % complète » | D1 / D3 |
| LB02-13 | Limite de fichiers -> `PARTIAL` après dernier `COMPLETE` | Dernière tentative et dernier complet séparés, observations partielles datées, total inconnu | 0/valeur exhaustive, effacement `Current` | D1 / D3 |
| LB02-14 | Annulation pendant `.m3u` | `PARTIAL/CANCELLED`, état antérieur conservé, pas de graphique de suppression | erreur non interceptée, nombre d'absents inféré | D1 / D3 |
| LB02-15 | Racine absente/non accessible, `FAILED` | Diagnostic, dernier complet consultable s'il existe, nouveau total inconnu | zéro, perte de l'historique, suppression | D1 / D3 |
| LB02-16 | `OFFLINE` + index historique **accessible** | Bibliothèque, valeurs historiques **datées** et provenance conservées, mention support absent | remise à zéro ou scan silencieux | D1 état / D3 |
| LB02-17 | `OFFLINE` + index **inaccessible** | Indisponibilité affichée, compteurs inconnus, bibliothèque logique non effacée | `0 jeux` ou catalogue supprimé | D1 état / D3 |
| LB02-18 | `STALE` sur snapshot dernier complet consultable | Données historiques explicitement datées ; pas de « vérifié maintenant » | fraîcheur assimilée à disponibilité ou scan automatique | D1 / D3 |
| LB02-19 | Disponibilité `UNKNOWN`, aucun nouveau probe | `Non vérifié` ; aucune prétention ONLINE/OFFLINE | OFFLINE affiché arbitrairement | D1 / D3 |
| LB02-20 | Deux snapshots, B partiel/hors partition A | Comparaison non comparable ou limitée aux zones prouvées ; `NOT_OBSERVED_IN_B_COVERED_SCOPE` uniquement avec preuve de couverture | `Supprimé` ou « manquant » hors zone couverte | D1 / D3 |
| LB02-21 | Sélection A/B de bibliothèques différentes | Refus ou réinitialisation de sélection, aucun diff créé | comparaison croisée, fuite de snapshot | D3 |
| LB02-22 | Changer bibliothèque avec fiche/filtre/compare ouverts | Sélection/compare de l'ancienne bibliothèque invalidée ; état visible de la nouvelle | ancienne fiche présentée comme nouvelle | D3 |
| LB02-23 | Reconnecter SSD fictif sous autre lettre, identité incertaine | Réassociation refusée ou demande de vérification/confirmation ; `libraryId` logique préservé | association automatique par lettre/nom | D1 état / D3 |
| LB02-24 | Erreur XML ou PNG corrompu de **fixture négative isolée** | Diagnostic utile, préservation du dernier import valide, fallback non trompeur | écrasement du catalogue IDEAL nominal, affichage silencieux | D1 / D3 |
| LB02-25 | Ouvrir depuis Dashboard un compteur/diagnostic | Navigation vers bibliothèque/plateforme pertinente avec source, couverture et date ; aucun scan lancé | lien inerte annoncé comme actif, dénominateur inventé | D3 |
| LB02-26 | Bibliothèque avec plateforme hors couverture et index non fiable | `Inconnu / non évalué`, jamais « absent » ni `0` pour cette partition | fausse absence de fichiers/jeux sur plateforme | D1 / D3 |
| LB02-27 | Écran étroit, clavier et lecteur d'écran | Sélection/filtres/états lisibles, focus visible, texte non limité à la couleur, pas de faux bouton actif | état inaccessible ou action annoncée mais inactive | D3 |
| LB02-28 | Contrôle d'absence d'effet | Aucune écriture/copie, suppression, scraping, réseau, lecture de vraie ROM ; le statut de suivi humain ne change pas la preuve | toute opération non autorisée | D1 / D3 |

### Verdict de recette par livrable

- **DEV 1 / fixtures :** PASS seulement si les manifestes et tests synthétiques exacts attestent les données, les états et les diagnostics exigés ; les lignes GUI peuvent rester `NOT_IMPLEMENTED`/`NOT_EXECUTED`. Les SHA-256 du générateur concernent exclusivement les fichiers de démonstration créés, **pas une collection utilisateur**.
- **DEV 3 / UI :** PASS seulement pour les lignes effectivement navigables et vérifiées, sur une version/commit Windows précis, sans inférence sémantique ajoutée. Un POC visuel et sa CI verte ne remplacent pas la recette des bibliothèques.
- **Revue commune :** état global `PASS` uniquement si toutes les lignes requises **dans un périmètre de release explicitement autorisé** sont exécutées et PASS. Sinon `PARTIAL_RECIPE / BLOCKED / NOT_IMPLEMENTED` et les manques exacts. Ce contrat documentaire **ne définit pas une nouvelle release**.

### Format minimal de preuve pour chaque cas

`ID | HEAD DEV 1/DEV 3 | fixture/manifest SHA | libraryId | sourceMode | scanId/generationId | coverageProof | état disponibilité/fraîcheur | attendu | obtenu | verdict PASS/FAIL/NOT_EXECUTED/NOT_IMPLEMENTED/BLOCKED | lien CI/capture/log`.

**Interdiction absolue de marquer PASS** à partir d'une simple égalité des comptes oracles : l'UI doit respecter les qualificatifs `référencé`, `observé`, `identifié`, `potentiel`, `inconnu` et la frontière `OFFLINE/STALE/PARTIAL`. Aucune opération sur collection réelle, merge ou Release n'est autorisée par cette recette.
