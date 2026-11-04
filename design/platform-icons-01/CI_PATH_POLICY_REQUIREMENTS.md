# GRAPHIC164 — Information pour résolution CI séparée

CI constatée sur c28004a4ba9bbb379cb9e012e1de9fb6949c3225 : Adaptive #41860507555 FAILURE, premier refus TRACKED_PATH_NOT_ALLOWED:design/platform-icons-01/CONSERVATION_90.md, puis ci-policy-gate / CLASSIFIER_OUTPUT_MISMATCH. Le premier chemin n'est pas une correction suffisante. DEV 2 prouve également une tentative de lecture UTF-8 de deux ZIP binaires par tests/ci_policy.py::audit_entries.

Pour une PR infrastructure séparée SOUS GO : autoriser exclusivement les chemins connus des 75 fichiers design de #164 ; restreindre les extensions et familles (docs md/json/txt, 24 études SVG, 4 SVG fallback, templates SVG, 2 sheets SVG, 2 aperçus historiques PNG, 2 ZIP NOMMÉS EXACTEMENT). Refuser tout design/.* ou *.zip générique. Ne pas déplacer les assets pour tromper le classificateur.

Pour les deux ZIP, brancher un audit binaire fail-closed à chemins fixes : CRC, doublons, tailles, chemins relatifs, traversal, symlinks, PNG dimensions/alpha, 84 entrées croisées au manifeste, SVG scripts/externes, refus de TTF/ROM/code. Considérer le verrouillage du ZIP historique par son blob SHA connu. Maintenir des canaris positifs ET négatifs.

Cette note n'autorise ni ne modifie une règle. Aucun GO sécurité ou CI verte implicite. Le HEAD documentaire suivant doit être revérifié par DEV 2.

P1_CI_POLICY_BLOCK / INFRA_SEPARATE_OWNER_GO_REQUIRED.

## Inventaire exact et archive réconciliée

Le fichier `CI_REQUIREMENTS_FINAL.md` contient la liste exhaustive des chemins suivis sur #164 et les garde-fous binaires. Le nouveau ZIP candidat contient **119 entrées** (84 PNG, 24 SVG sujet, 2 planches, 4 SVG fallback, 5 documents) : aucun regex générique `design/.*` ni acceptation globale de `*.zip` ne doit remplacer l'énumération stricte. Aucun changement CI apporté par cette PR.
