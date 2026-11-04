# NEXT-05 / #149 Lot B — candidat ZIP IDEAL, validation interne

**Statut : PR/Release split NEXT-05 proposé sous GO propriétaire #6612651570, en attente revue DEV 2 et CI au HEAD exact ; GUI non-admin/ES-DE GUI = RELEASE_ONLY, qualification #95 NOT_VERIFIED, publication HOLD.** Ce document décrit un gate technique non réceptionné ; il ne constitue pas une recette humaine ni une Release.

## Périmètre

- Gate d'entrée : main 7aa62c8fe643ab56d58829903036d89031b11170, CI push/main #41465566095 (10/10 SUCCESS), GO propriétaire B.
- CI requise dans ci-dotnet Windows 2022, après tests .NET et ZIP POC V2C **inchangé et quarantainé**.
- scripts/Validate-BetaIdealZip.ps1 : deux builds séparés (clean, locked restore, publish .NET 10.0.401 / win-x64 / self-contained), licences exactes, manifeste par fichier et SHA-256 ZIP. Archives ordonnées et datées de façon figée : mêmes empreintes ZIP, chemins, tailles et fichiers obligatoires ; toute dérive est bloquante.
- BETA-IDEAL-MANIFEST.json : version v0.5.0-beta.1, phase validate, runtime .NET 10.0.12, aucun jeu ni fixture inclus, approbations juridique et publication en HOLD.
- tests/ideal_beta_zip_gate.py : refuse fichiers manquants, extra, altérés, liens/reparse, traversées, collisions Unicode/casse et ROMs/BIOS/sauvegardes ; tests négatifs sur fixtures fictives et copie de l'extraction.
- tests/sqlite_legal_gate.py : vérifie sur chaque vraie publication Windows six NUPKG, empreintes séparées des contenus et archives signées, cinq binaires, notices/licences et provenance fail-closed. Le runtime .NET a sa propre licence et ses SHA.
- Aucun ZIP ni exécutable téléchargé en artefact de CI, publié ou tagué.

## Partition PR / Release — décision propriétaire #6612651570 (proposition non fusionnée)

- **Required PR** : build + tests .NET vrais, provenance SHA, double ZIP déterministe/manifeste/licences, canaris négatifs, vraie fenêtre extrait sous token **runner seulement**, probe SQLite natif sous compte Windows standard (SID exact, HKU chargé, handles valides), réseau sortant bloqué, ACL et cleanup vérifiés. Le switch `-PrOnly` arrête le worker **après SQLite**, avant le lancement GUI standard : aucune attestation GUI/ES-DE non-admin `PASS` en PR.
- **RELEASE_ONLY / NOT_EXECUTED_ON_PR** : vrai EXE du ZIP candidat exact lancé dans une session Windows graphique réellement ouverte sous compte standard (non-admin), cinq oracles `WINDOW_OPENED → DISPATCHER_EXECUTE → SMOKE_PASS → POC_WINDOW_OPENED_PASS → LIFETIME_RETURN` ordonnés, exit 0, GUI ES-DE synthétique, SQLite/offline/ACL, nettoyage et recette humaine #95. Le succès de PR n'autorise pas la livraison.
- **Publication protégée** : `.github/workflows/dist-zip.yml phase=publish` appelle avant tout tag/asset un vérificateur de reçu indépendant signé HMAC-SHA256 ; preuve liée au SHA source, SHA-256 du ZIP reconstruit, SHA-256 de son EXE, tag précis, fraîcheur <= 7 jours, session standard, oracles, ES-DE, sécurité et #95. L'absence de preuve signée ou d'hôte qualifiant ⇒ `NEXT05_RELEASE_HOLD`. `phase=validate` reste strictement sans upload. Les secrets/attestations réels doivent être préparés hors des PR, sous contrôle du propriétaire ; une preuve synthétique de canari ne peut devenir une preuve de qualification.
- **Limite de sécurité honnête** : une signature authentifie l'émetteur du reçu, pas la réalité physique de la recette ; la responsabilité du signataire indépendant et le contrôle humain #95 restent obligatoires. Les administrateurs possédant un accès manuel direct aux Releases/tags ne sont pas techniquement empêchés par ce workflow : protection GitHub/permissions et gouvernance hors workflow nécessaires. Parmi les onze workflows présents au HEAD de référence, seul `dist-zip.yml` contient une création de Release/tag.
- **Risque accepté** : régression GUI spécifique non-admin potentiellement intégrée dans `main` et détectée avant livraison. `REAL_*_HOLD`, `LEGAL_PUBLISH_HOLD` et `NEXT05_RELEASE_HOLD` maintenus, aucun ZIP de recette qualifié disponible.

## Preuve Windows sous compte non-administrateur

tests/Invoke-BetaIdealWindows.ps1 ré-extrait le ZIP et prépare une fixture inventée, crée un utilisateur local temporaire non-admin, accorde des ACL limitées sur les dossiers de fixture et ajoute une règle pare-feu bloquant les sorties réseau de l'EXE. Le travailleur exécute le binaire extrait avec DOTNET_ROOT volontairement invalide et un PATH sans installation globale de .NET.

Les entrées de preuve déjà présentes dans le produit sont lancées depuis l'EXE extrait : fenêtre Avalonia réelle (--smoke-window) et import ES-DE fictif à quatre entrées (--ci-ux-offline-fixture). Les oracles attendent les diagnostics média/ROM indisponibles, l'UI, le rejet de XML malformé et la conservation du dernier instantané. L'empreinte du XML fixture doit rester identique avant/après. Un probe natif charge e_sqlite3.dll depuis le ZIP et exécute CREATE/INSERT/CLOSE/REOPEN/SELECT.

**Limites importantes :** ce probe SQLite natif ne constitue pas une restauration complète du SqliteCatalogStore du produit depuis le ZIP. Les chemins CurrentComplete, Offline/Stale, Cancel/Dispose, consentement, décisions et isolation ROMS/MEDIAS restent vérifiés par les suites .NET sur fixtures dans cette CI, sans preuve humaine du parcours intégral extrait. Les assertions non exécutées doivent rester NOT_EXECUTED ; ne pas les transformer en PASS.


## P1 non-admin Windows — session/bureau et règle de décision

Le test GUI non-admin doit être exécuté dans une session Windows réellement interactive. Le simple emploi de Start-Process -Credential sur le runner ne démontre pas cet état. Le harnais distingue les preuves suivantes :

1. **Runner :** rôle, PID, SessionId, ActiveConsoleSessionId, IDs des sessions WTS actives (sans noms), état WTS, catégorie WinSta0/autre, bureau Default/autre, visibilité de la window station, RID d'intégrité, booléen administrateur et seules présences booléennes de TEMP/USERPROFILE. Aucun nom de compte, chemin, environnement complet ni secret dans les logs.
2. **Worker non-admin :** mêmes informations obtenues dans son processus et son jeton. L'identité administrateur reste interdite. Un compte temporaire ne prouve pas une session WTS interactive.
3. **Probe SQLite en premier :** sous le même worker non-admin, chargement de la DLL extraite e_sqlite3.dll, CREATE/INSERT/CLOSE/REOPEN/SELECT sur une base synthétique ; résultat séparé de la GUI. Un PASS SQLite seul ne change jamais le verdict ZIP.
4. **GUI ensuite seulement :** si le worker est en état WTSActive (0), WinSta0/Default, station visible WSF_VISIBLE **et** si OpenDesktop(Default), OpenInputDesktop et la création d'un HWND natif masqué réussissent réellement sous son jeton, lancer l'EXE extrait sous ce même compte et conserver PID/SessionId/démarrage/exit ou timeout. Les échecs Win32 sont consignés par code numérique (notamment ACCESS_DENIED = 5) ; aucun bureau n'est créé ni aucune ACL élargie. Sinon, refuser la GUI avec CI_NONADMIN_GUI_ENV_UNSUPPORTED / OWNER_DECISION_REQUIRED, sans faux PASS administrateur.

Dans la CI #41474371101 au HEAD 186a5ee1, le runner et le worker non-admin partagent la session WTSActive 2 (WinSta0/Default visible), mais leurs niveaux d'intégrité diffèrent (élevé vs moyen). Le probe SQLite natif du ZIP est PASS sous non-admin ; Avalonia reste TIMEOUT 90 secondes. Le fait de voir WinSta0/Default n'établit pas encore les droits effectifs d'ouverture du bureau : l'ajout des trois oracles Win32 vise précisément cette hypothèse. La CI #41475857221 au HEAD c277f909 a ensuite confirmé les trois accès Win32 PASS sous les deux comptes ; malgré cela Avalonia non-admin reste TIMEOUT 90 secondes. 

Ces indicateurs décrivent les limitations de session/bureau observables. Si l'exécutable graphique échoue malgré un bureau apparemment interactif, la cause OS/applicative reste NOT_VERIFIED et le gate échoue. Un signal WTS seul ne constitue pas une fenêtre Avalonia ouverte : seul le smoke GUI exécuté avec succès et le parcours ES-DE natif le prouvent.

### Si le runner GitHub hébergé ne fournit pas de bureau interactif

**OWNER_DECISION_REQUIRED / NO_AUTOMERGE / NO_RELEASE.** Option soumise au propriétaire : poste ou runner Windows dédié, sous session console/RDP interactive réellement ouverte avec compte standard non-administrateur. Utiliser l'archive ZIP exacte et son SHA-256 attestés par CI ; extraire dans un répertoire temporaire neuf, conserver des données strictement fictives et un blocage réseau, sans installer un SDK/runtime .NET global pour dissimuler une dépendance. Vérifier l'ouverture Avalonia, les parcours IDEAL ES-DE, médias et diagnostics, SQLite et restauration, Offline/Stale, Cancel/Dispose, consentement et refus hors racines, avec preuves par oracle et opérateur. Détruire les fixtures ensuite. Une recette humaine reconnue ou un runner dédié constitue une preuve complémentaire, mais **ne remplace pas automatiquement une CI required rouge**, une revue indépendante ni un GO de fusion ou Release.


## P1 #154 — dernière instrumentation Avalonia autorisée (pré-revue DEV 2 obtenue)

**GO indépendant :** DEV 2 DIAGNOSTIC_TRACE_GO (revue #5739614735) au HEAD c277f909 ; GO Contrôleur réservé à une seule itération. Ce diagnostic n'est ni une approbation de la PR ni un GO de fusion.

- **Confinement réel :** le runner Windows crée CommonDocuments/rm-beta-ci-<10hex> et la marque fixe BETA-TRACE-FIXTURE.marker après avoir créé les seuls chemins inventés fictional-ideal/ROMS/gba/A.fixture et MEDIAS/gba/metadata/es-de-gamelist.xml. Pour chaque EXE extrait, RUNNER_TEMP et ROMOTHEQUE_BETA_TRACE_ROOT sont explicitement définis à ce même Shared. L'entrée C# ne trace que pour un mode GUI fixture (--smoke-window/--ci-ux-offline-fixture), GITHUB_ACTIONS=true, ROMOTHEQUE_BETA_TRACE=1, la correspondance canonique exacte à CommonDocuments, l'emplacement du binaire dans unpacked non-reparse et la marque synthétique exacte. Une demande de trace hors ce périmètre échoue BETA_TRACE_SCOPE_DENIED, sans chemin dans les journaux. Démarrage normal : zéro breadcrumb.
- **Marqueurs uniques et constants**, flush immédiat : MAIN_ENTER, SETUP_BEGIN, SETUP_COMPLETE, MAINWINDOW_CTOR_BEGIN, MAINWINDOW_CTOR_END, LIFETIME_START_BEGIN, WINDOW_OPENED, DISPATCHER_POST, DISPATCHER_EXECUTE, SMOKE_PASS/SMOKE_FAIL, LIFETIME_RETURN. Ils encadrent les appels existants ; aucune deuxième boucle dispatcher, aucune nouvelle permission, aucune suppression d'un oracle. Le code temporaire n'active aucun accès à une ROM, XML personnel, image ou profil utilisateur.
- **Comparaison :** le harnais exécute le même ZIP réellement extrait sous l'identité runner pour une trace diagnostique uniquement, puis sous le compte non-admin obligatoire après les contrôles de token/Win32/SQLite. Un PASS du compte runner ne peut jamais remplacer le POC_WINDOW_OPENED_PASS non-admin.
- **Timeout inchangé à 90 s :** après arrêt du seul processus EXE lancé, attendre bornément les deux pipes, lire au maximum leurs derniers 16 KiB et extraire au plus 12 marqueurs strictement whitelistés. Ne jamais imprimer le contenu brut stdout/stderr, les messages/stack d'exception, identité, mots de passe, variables complètes, chemins ni média. Le processus et le job restent en échec même si WINDOW_OPENED figure dans la trace sans oracle métier. Le gate final demeure ci-dotnet puis ci-policy-gate.
- **Interprétation :** la dernière frontière atteinte localise le point de blocage, sans le qualifier automatiquement de deadlock, problème de profil, rendu ou droits. Sans cause reproduite/démontrée lors de cette seule itération, verdict AVALONIA_CAUSE_NOT_VERIFIED / OWNER_DECISION_REQUIRED, PR Draft et required checks rouges, pas de nouvelle tentative spéculative.

### #95 — procédure Windows dédiée, **à préparer uniquement / NE PAS EXÉCUTER**

Préconditions : GO propriétaire explicite pour cette recette et pour tout changement de gate ; machine Windows dédiée hors collection réelle, session console/RDP ouverte et non-admin standard confirmé par SID/integrity/Win32 sans exposer l'identité. Récupérer un ZIP candidat exact avec son SHA-256 et le SHA Git du checkout CI, contrôler manifeste et licences, extraire dans un dossier neuf vérifié non-reparse. Créer seulement les ROMS *.fixture, jaquettes fictives, XML ES-DE et base SQLite de test, garder réseau sortant bloqué et ne dépendre d'aucun .NET installé globalement. Tester séparément : lancement Avalonia et Window.Opened, UI ES-DE, inventaire et médias locaux synthétiques, SQLite create/reopen/backup/restore, OFFLINE/STALE, Cancel/Dispose, consentement/refus et racines A/B, et refus hors périmètre. Relever pour chaque preuve PASS/FAIL/NOT_EXECUTED, environnement anonymisé, SHA, heure, codes, puis détruire compte et fixtures. Aucun passage en production, vrai scan, publication, upload d'archive ou transformation d'une CI rouge en succès par ce protocole.

## Conditions de réception

1. CI complète au HEAD exact : policy, Documents, .NET Windows (incluant Lot B), DocFX >=90 %, 100 % nouvelles API et ci-policy-gate.
2. Revue indépendante DEV 2 sur ce même HEAD, notamment les limites de la preuve et l'absence d'upload. Aucun verdict favorable présumé.
3. Fusion uniquement par Contrôleur avec GO et CI push/main post-fusion.
4. Recette humaine #95, accès réels, autorisation juridique, phase publish et Release restent soumis à décisions distinctes. Tous les REAL_*_HOLD et NEXT05_RELEASE_HOLD restent actifs.

Le ZIP POC V2C conserve sa quarantaine et son workflow inchangés.
