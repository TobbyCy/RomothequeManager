# PUB-01A — Préparation technique de la CI pour un dépôt public

**Statut : CHANGES_REQUIRED avant passage PUBLIC.** Travail proposé sur la branche isolée `audit/pub01a-public-ci-hardening` issue du HEAD de #29 `c8e47b2452b07b127a50de763db5928caa7edcd7` (22.10.2026). Ne pas confondre ce dossier de préparation avec la CI effective de `main` ; aucune modification de visibilité ou de protection GitHub réalisée. Les preuves ci-dessous sont des inspections des sources GitHub, **PAS** des résultats de workflow exécuté.

## 1. Périmètre contrôlé et garde-fous

| Surface | État de la branche candidate | Preuve et limite |
|---|---|---|
| Événements | `pull_request` ciblant `main` (opened/reopened/synchronize/ready_for_review), `push` sur `main`, `workflow_dispatch` réservé à `main` par le classificateur | `.github/workflows/ci.yml` + `tests/ci_policy.py: event_range`; aucune utilisation de `pull_request_target`, `workflow_run` ou `issue_comment` |
| Privilèges | `permissions: contents: read` ; aucune permission write, secret injecté, OIDC id-token write ou checkout à credentials persistants | Policy `verify_public_ci_guard` refuse les modifications dangereuses ; réglages GitHub effectifs à vérifier séparément |
| Actions externes | `actions/checkout` avec SHA complet `fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09` ; aucune action non épinglée permise | Tests négatifs rejettent les autres `uses:` et les aliases non SHA ; PowerShell Gallery/Pester 4.10.1 reste une dépendance externe avec contrôle de signature, sa disponibilité runtime est INCONNUE |
| Sources non fiables | Fork PR avec code synthétique traité comme **non fiable**, token lecture seule, aucun `pull_request_target` | Les scripts du fork peuvent modifier le workflow PR, d'où l'obligation des revues CODEOWNERS depuis `main` et de ne pas fournir de secrets ou runner privilégié |
| Validation politique | `policy` Python + `ci-docs` + CI-0 PS5.1 + autres jobs selon type de diff | `tests/ci_policy.py: final_gate` recalcule le plan et exige **exactement** les statuts `success` des jobs obligatoires ; `skipped`, `cancelled` et `failure` ne valent pas succès |
| Tests PS5.1 | Comparaison snapshots, bibliothèques, JSON UI, catalogue T1, DTO canonique T3, Pester, E1, préflight, négatifs | Raccordés aux jobs Windows sur branche candidate, ainsi que génération réelle de JSON synthétique PS5.1 consommée par Node |
| Tests UI après intégration | Si `prototype/ui11` absent, `UI_NOT_APPLICABLE_NO_PROTOTYPE_IN_TREE`. Si présent, découverte fermée : trois smokes #28 ; si modèle T2 présent, deux tests #30 supplémentaires | Un seul fichier `*.cjs` non prévu, un smoke échoué ou un script annoncé absent **fait échouer CI-0** ; ne pas traiter les tests encore absents de #29 comme exécutés |
| Gate final | Job `ci-policy-gate` avec `always()`, `needs` exact, lecture de `toJSON(needs)`, recalcul sur sources GitHub | Gate final fail-closed **si effectivement lancé et rendu required check** dans les protections GitHub ; il ne protège pas `main` tant que ces paramètres ne sont pas actifs |
| Propriétaires du code | `.github/CODEOWNERS` candidat : propriétaire identifié du dépôt pour tous les fichiers, workflow, politique, règles elles-mêmes | CODEOWNERS ne déclenche aucune revue bloquante sans activation GitHub du contrôle « Require review from Code Owners » et présence d'un approbateur indépendant |

### Chemins et informations internes

Le scan ciblé des sources et documents Produit n'a pas relevé de lien direct vers un dépôt privé externe dans les **fichiers contrôlés**. Il ne vaut ni audit exhaustif de tout l'historique Git, ni attestation d'absence de secrets. Des identifiants de documents internes utilisés dans les canaris de chemins ont été remplacés par des exemples neutres.

**MÉTADONNÉES HISTORIQUES À TRAITER AVANT PUBLIC :** au moins trois commentaires sur des PR Produit anciennes comportent des liens vers un dépôt privé distinct ; un corps de PR Produit contenait également un tel lien. Les fichiers sont indépendants de ces métadonnées : rendre le dépôt public peut rendre visibles ces commentaires, y compris sur une PR fermée. Une suppression de lien dans le contenu courant **ne garantit pas** que les versions/notifications passées deviennent inaccessibles. Revue par le propriétaire et audit PUB-01B obligatoires. Ne pas insérer le lien privé dans les fichiers de ce dépôt.

## 2. Protection de `main` proposée au propriétaire — à appliquer après publication

Dans **Settings → Rules → Rulesets** (ou **Branches → Branch protection rule** selon le compte), cibler uniquement `main` ; vérifier explicitement que la règle est **Active / Enforced** :

1. Exiger une **pull request avant fusion**, au moins une approbation humaine indépendante ; interdire la fusion d'une PR Draft, exiger les conversations résolues et rejeter les approbations obsolètes après nouveau commit.
2. Exiger la revue **Code Owners** sur changements `.github/`, scripts CI, modules et politiques. Le propriétaire ne peut pas approuver sa propre PR : si aucun autre réviseur autorisé n'est disponible, **HOLD** jusqu'à résolution organisationnelle, sans désactiver la protection.
3. Rendre obligatoires les statuts **`ci-policy-gate`** sur `pull_request` et tester un vrai run ; de préférence exiger les branches à jour avec `main`. N'ajouter des checks individuels que si leur comportement est toujours applicable ; le gate adaptatif est l'agrégateur prévu.
4. Bloquer les force-pushs et la suppression de `main`, interdire le bypass pour administrateurs et apps non autorisées. Vérifier les rôles de fusion, les méthodes merge/squash et la limitation des commits directs.
5. Dans **Settings → Actions → General**, conserver les tokens workflow avec permissions minimales (lecture par défaut) ; ne jamais envoyer de secrets ni token write aux PR de forks. Activer **l'approbation pour tous les contributeurs externes** ; ne pas autoriser de runner auto-hébergé avec secrets sur les PR externes.
6. Interdire les actions non approuvées / non épinglées et vérifier les mises à jour de SHAs ; consulter les journaux Actions, quotas, durée des jobs, coûts et la disponibilité réelle des runners. Le job Pester télécharge un paquet versionné puis vérifie la signature **avant import** ; si la provenance externe échoue, CI doit rester rouge.

**État de configuration au 22.10.2026 :** API GitHub de protection `main` : `403 Resource not accessible by integration` ; endpoint rulesets en privé : `403 Upgrade to GitHub Pro or make this repository public`. Aucun réglage activé ou vérifié par ce mandat. Une fenêtre sans protection peut exister entre changement de visibilité et activation : coordonner ces actions sous contrôle propriétaire, sans déclarer le dépôt prêt plus tôt.

## 3. Recette publique conditionnelle (aucune étape exécutée sur GitHub Actions ici)

Après audit du contenu, décision de licence et publication **autorisée par le propriétaire seulement** :

1. Contrôler le SHA du `main` publié, `.github/CODEOWNERS`, `ci.yml`, `tests/ci_policy.py`, `tests/Invoke-CiStatic.ps1` et les fichiers synthétiques ; `main` doit contenir effectivement les correctifs #26 → #27 → #29 et #28 → #30.
2. Configurer et **relire** la protection `main` avant d'accepter un changement public. Exiger `ci-policy-gate` ; vérifier qu'il ne s'agit pas d'un état ancien ou artificiellement vert.
3. Ouvrir une **PR fictive d'un fork contrôlé** sur `main`. Vérifier approbation workflow externe, token lecture seule, absence de secrets, événements exacts, tous les tests requis et le gate sur SHA/base/merge SHA réels.
4. Sur copies/branches isolées synthétiques, démontrer qu'un test PS échoué, Node échoué, Python échoué, job manquant, job `skipped` pourtant obligatoire et une action non épinglée sont bloquants. Aucun canari de défaillance ne doit être poussé vers `main`.
5. Exécuter des changements DOCS_ONLY, PS_ONLY, UI/prototype et SHARED/UNKNOWN en PR d'essai. Confirmer le routage et les jobs non applicables sans `PASS` fictif. Tester UTF-8 BOM PS5.1 → JSON → Node → recherche Unicode.
6. Après **fusion autorisée**, observer `push/main` et son nouveau SHA. Comparer l'ensemble des checks réellement exécutés à la matrice d'applicabilité ; archiver noms des jobs, conclusions, horodatage, URL des runs et hash des exports synthétiques.
7. Si Actions est bloqué avant toute étape (facturation/runner/service), constater **BLOCKED / NOT_EXECUTED** ; aucune réussite d'un workflow local ou d'une ancienne branche ne remplace le gate sur `main`.

## 4. Interdictions et reliquats

- **Aucun PASS runtime** déclaré : cette branche contient des modifications de scripts et des assertions négatives, pas un run Windows, Python/Node ou une validation Actions.
- L'historique des PR/issues, les assets et les licences relèvent aussi de l'audit de publication distinct PUB-01B ; ses conclusions peuvent bloquer la publication même si la CI est correcte.
- `CODEOWNERS` + gate dans un workflow **modifiables par une PR** ne constituent pas à eux seuls une frontière de confiance ; la protection du `main` et la revue indépendante sont nécessaires. Ne pas considérer le code d'un fork comme sûr.
- Ne pas créer de Release, ne pas augmenter les permissions, ne pas charger de ROM/SSD/StateRoot ni utiliser les secrets du propriétaire dans les jobs.

**Verdict proposé : `PUB01A_TECHNICAL_READY_CANDIDATE / CHANGES_REQUIRED_FOR_PUBLICATION`** jusqu'au merge autorisé, à l'activation effective des règles de `main`, à la recette CI externe, au nettoyage des métadonnées et à la réception de PUB-01B.
