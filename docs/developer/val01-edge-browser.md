# VAL-01 — recette Microsoft Edge réelle (Issue #39)

La preuve navigateur attendue est **indépendante** des suites Node/V8 à DOM simulé. `tests/edge-browser-smoke.cjs` ouvre `prototype/ui11/index.html` et `demo-integree.html` sous Microsoft Edge Chromium réel, en mode **headless**, via Chrome DevTools Protocol (CDP) et le client WebSocket natif Node. Le seul trafic réseau de pilotage autorisé est `127.0.0.1` ; les pages sont locales `file://`, les URL HTTP(S) sont bloquées et toute tentative détectée provoque un échec. Aucun paquet npm, service SaaS, ROM, BIOS ou dossier personnel n'est lu.

## Exécution reproductible

Sur **Windows 2022** disposant déjà de Microsoft Edge, de Node avec `globalThis.WebSocket` et des sources du dépôt :

```powershell
node --version
node .\tests\edge-browser-smoke.cjs
if ($LASTEXITCODE -ne 0) { throw "EDGE_BROWSER_SMOKE_FAILED" }
```

Le harnais cherche exclusivement `msedge.exe` dans les emplacements Program Files officiels. Il **échoue** si Edge est absent, si le protocole ne s'ouvre pas, si le produit n'est pas Edge, si une ressource n'est pas chargée, en cas d'exception JavaScript ou si une assertion échoue. Il affiche la version réelle depuis `Browser.getVersion`, celle de Node, le `GITHUB_SHA`, le nombre de contrôles et `BROWSER_REAL_PASS` seulement après la recette entière. Le profil Edge est temporaire et supprimé en `finally`. Aucun téléchargement/installation automatique du navigateur.

## Scénarios obligatoires

- Deux chargements complets HTML/CSS/JS locaux ; présence des scripts de projection et d'un rendu réellement stylé ; zéro erreur JS.
- `index.html` : Dashboard, navigation par entrée souris CDP, Bibliothèques, OFFLINE/STALE, index inaccessible (refus explicite), Comparaison, Collection et fiche ouverte. Aucun message de suppression physique inférée.
- `demo-integree.html` : Dashboard, Bibliothèques, sélection multi-états, comparaison précalculée avec couverture non prouvée et classe UNKNOWN, inspecteur d'une unité, refus d'index inaccessible, Collection et fiche.
- Contrôle fermé des URLs chargées, limitation aux ressources du dépôt et zéro requête Internet volontaire. Les canaris Python exigent le **chemin exact** du harnais et les commandes GitHub Actions sans `continue-on-error`.

## CI et limites

La recette est un pas **obligatoire** du job Windows `CI-0` ; le `ci-policy-gate` préexistant refuse CI-0 en erreur. La politique garde `permissions: contents: read`, aucun `pull_request_target` et aucun secret pour forks. Edge/Node non disponibles = **FAIL**, jamais PASS ou SKIPPED volontaire. Les sources originelles et les suites Node restent inchangées.

Un navigateur **headless réel** n'est ni une preuve de rendu graphique vérifié par une personne, ni une mesure d'accessibilité complète, ni une application Windows portable installable. Les fixtures de démonstration intégrée restent rédigées manuellement ; ce test ne les transforme pas en export réel PowerShell. Les résultats ne valent que pour l'édition/versions du navigateur, l'OS et le HEAD du run. Les captures et logos ne sont pas créés ni publiés par ce lot.
