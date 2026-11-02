# NEXT-05 / #149 Lot B — candidat ZIP IDEAL, validation interne

**Statut : DRAFT / CI Windows NOT_VERIFIED / revue DEV 2 obligatoire.** Ce document décrit un gate technique non réceptionné ; il ne constitue pas une recette humaine ni une Release.

## Périmètre

- Gate d'entrée : main 7aa62c8fe643ab56d58829903036d89031b11170, CI push/main #41465566095 (10/10 SUCCESS), GO propriétaire B.
- CI requise dans ci-dotnet Windows 2022, après tests .NET et ZIP POC V2C **inchangé et quarantainé**.
- scripts/Validate-BetaIdealZip.ps1 : deux builds séparés (clean, locked restore, publish .NET 10.0.401 / win-x64 / self-contained), licences exactes, manifeste par fichier et SHA-256 ZIP. Archives ordonnées et datées de façon figée : mêmes empreintes ZIP, chemins, tailles et fichiers obligatoires ; toute dérive est bloquante.
- BETA-IDEAL-MANIFEST.json : version v0.5.0-beta.1, phase validate, runtime .NET 10.0.12, aucun jeu ni fixture inclus, approbations juridique et publication en HOLD.
- tests/ideal_beta_zip_gate.py : refuse fichiers manquants, extra, altérés, liens/reparse, traversées, collisions Unicode/casse et ROMs/BIOS/sauvegardes ; tests négatifs sur fixtures fictives et copie de l'extraction.
- tests/sqlite_legal_gate.py : vérifie sur chaque vraie publication Windows six NUPKG, empreintes séparées des contenus et archives signées, cinq binaires, notices/licences et provenance fail-closed. Le runtime .NET a sa propre licence et ses SHA.
- Aucun ZIP ni exécutable téléchargé en artefact de CI, publié ou tagué.

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

Dans la CI #41474371101 au HEAD 186a5ee1, le runner et le worker non-admin partagent la session WTSActive 2 (WinSta0/Default visible), mais leurs niveaux d'intégrité diffèrent (élevé vs moyen). Le probe SQLite natif du ZIP est PASS sous non-admin ; Avalonia reste TIMEOUT 90 secondes. Le fait de voir WinSta0/Default n'établit pas encore les droits effectifs d'ouverture du bureau : l'ajout des trois oracles Win32 vise précisément cette hypothèse. Leurs résultats sont NOT_VERIFIED tant que la CI au nouveau HEAD n'est pas terminée. 

Ces indicateurs décrivent les limitations de session/bureau observables. Si l'exécutable graphique échoue malgré un bureau apparemment interactif, la cause OS/applicative reste NOT_VERIFIED et le gate échoue. Un signal WTS seul ne constitue pas une fenêtre Avalonia ouverte : seul le smoke GUI exécuté avec succès et le parcours ES-DE natif le prouvent.

### Si le runner GitHub hébergé ne fournit pas de bureau interactif

**OWNER_DECISION_REQUIRED / NO_AUTOMERGE / NO_RELEASE.** Option soumise au propriétaire : poste ou runner Windows dédié, sous session console/RDP interactive réellement ouverte avec compte standard non-administrateur. Utiliser l'archive ZIP exacte et son SHA-256 attestés par CI ; extraire dans un répertoire temporaire neuf, conserver des données strictement fictives et un blocage réseau, sans installer un SDK/runtime .NET global pour dissimuler une dépendance. Vérifier l'ouverture Avalonia, les parcours IDEAL ES-DE, médias et diagnostics, SQLite et restauration, Offline/Stale, Cancel/Dispose, consentement et refus hors racines, avec preuves par oracle et opérateur. Détruire les fixtures ensuite. Une recette humaine reconnue ou un runner dédié constitue une preuve complémentaire, mais **ne remplace pas automatiquement une CI required rouge**, une revue indépendante ni un GO de fusion ou Release.

## Conditions de réception

1. CI complète au HEAD exact : policy, Documents, .NET Windows (incluant Lot B), DocFX >=90 %, 100 % nouvelles API et ci-policy-gate.
2. Revue indépendante DEV 2 sur ce même HEAD, notamment les limites de la preuve et l'absence d'upload. Aucun verdict favorable présumé.
3. Fusion uniquement par Contrôleur avec GO et CI push/main post-fusion.
4. Recette humaine #95, accès réels, autorisation juridique, phase publish et Release restent soumis à décisions distinctes. Tous les REAL_*_HOLD et NEXT05_RELEASE_HOLD restent actifs.

Le ZIP POC V2C conserve sa quarantaine et son workflow inchangés.
