# NEXT-03 — Recette vérifiable DEV 1 / DEV 3

**État initial : `NOT_EXECUTED` pour tous les cas du nouveau parcours.** [Contrat NEXT-03](next-03-local-library-contract.md), [#76](https://github.com/TobbyCy/RomothequeManager/pull/76), [oracles DEMO-01](../../contracts/demo-library-01/oracles.json), [recettes moteur #75](https://github.com/TobbyCy/RomothequeManager/pull/75), [aperçu Avalonia #77](https://github.com/TobbyCy/RomothequeManager/pull/77). Préparer uniquement des **fixtures fictives locales temporaires, sans ROM commerciale**, et les vérifier avec le générateur DEMO-01. Les chiffres ci-dessous sont des **attendus d'oracle**, pas des résultats d'un test UI déjà passé.

**Verdicts :** `PASS` uniquement sur exécution prouvée de la **même build Windows et du même HEAD** ; `FAIL` si comportement contraire ; `NOT_IMPLEMENTED` si écran/adaptateur absent ; `NOT_EXECUTED` si non joué ; `BLOCKED` pour prérequis ou sécurité manquants. Un test .NET ou une capture de POC ne vaut pas automatiquement un PASS GUI.

| ID | Action synthétique | Attendu PASS — sinon FAIL |
|---|---|---|
| N03-01 | Ouvrir Bibliothèques puis annuler le sélecteur | Aucun scan, aucune mutation, sélection/dernier complet inchangés. |
| N03-02 | Choisir UNC/URI, racine absente, reparse point | Refus sûr avec diagnostic neutre ; jamais accès hors de la racine approuvée. |
| N03-03 | Choisir IDEAL, puis cliquer **Inventorier** | Début seulement sur action explicite, `ScanId`, source, portée, limites et annulation disponibles ; aucun double scan simultané. |
| N03-04 | IDEAL : scan `COMPLETE`, import ES-DE facultatif | **32 observations ROMS**, **30 références XML** seulement si import demandé, **13 plateformes déclarées** ; `ConfirmedGameCount=null`. |
| N03-05 | Inspecter Metal Deer Salad `demo01-011` | **Une** référence de jeu, une playlist et deux fichiers membres ; pas trois jeux reconnus. |
| N03-06 | CHAOS : scan `COMPLETE`, import XML facultatif | **33 observations ROMS**, **30 références ES-DE**, ROM `005` et `017` référencées introuvables ; aucune suppression inférée. |
| N03-07 | CHAOS : fichiers similaires `004/013/027` | Trois groupes **candidats** avec raison/provenance, aucun doublon exact ou action destructive. |
| N03-08 | Médias IDEAL/CHAOS | `image/thumbnail/marquee` déclarés ≠ effectivement décodés ; fallback lisible ; 14 PNG absents dans l'**oracle** CHAOS, non nécessairement 14 manques ES-DE exposables. |
| N03-09 | MYSTERY sans aucun XML | **32 observations**, aucun import ES-DE imposé, **Jeux identifiés : Inconnu**, aucun `demoId` d'oracle affiché comme découverte. |
| N03-10 | Annuler pendant lecture `.m3u` après dernier complet | `PARTIAL/CANCELLED`, `TotalFileCount=null`, `LastAttempt` partiel, `Current` complet inchangé et handles fermés. |
| N03-11 | Atteindre une limite / dossier refusé après début | `PARTIAL`, couverture incomplète visible, diagnostic, aucun compte exhaustif ni faux absent. |
| N03-12 | Retirer la racine avant premier fichier | `FAILED`, `TotalFileCount=null`, diagnostic, dernier complet conservé si présent ; pas `0`. |
| N03-13 | Simuler OFFLINE, index accessible puis inaccessible ; STALE | Historique **daté seulement si accessible** ; sinon `Inconnu` ; Offline/Partial/Stale distincts, pas de suppression ni scan automatique. |
| N03-14 | Basculer bibliothèque A→B dans Collection/Observations | Source, compteurs, filtres/fiche et diagnostic du bon contexte ; aucune identité A dans B. |
| N03-15 | Vérifier manifests fictifs avant/après et double clic Inventorier | Aucun fichier source modifié ; session single-flight ; aucun réseau, suppression, export ni copie. |
| N03-16 | Proposer test sur vraie ROMothèque | **BLOCKED** sans GO propriétaire sécurité distinct ; aucun contenu ni hash privé envoyé dans GitHub. |

**Preuve minimale par cas :** `ID | HEAD + SHA-256 ZIP de test | manifest SHA de fixture fictive | librarySessionId/scanId | source/mode | date/coverageProof | état Scan/Availability/Freshness | attendu/observé | verdict | lien CI + capture/log anonymisé`.

**Gate de réception future :** N03-01..15 `PASS` sur une même build et fixtures, tests/CI Windows verts, sécurité revue ; N03-16 ne devient admissible que sur **nouvelle autorisation distincte**. Les procédures USB réel, persistance inter-redémarrages et changement de lettre seront reçues séparément ; **aucun vrai scan, merge ni Release dans cette PR**.
