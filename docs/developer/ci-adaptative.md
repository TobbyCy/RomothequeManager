# CI adaptative — politique et vérifications obligatoires

**Plan des vérifications à conserver ; disponibilité des workflows et des runners à confirmer sur le commit testé.** La documentation ne prouve aucun run GitHub distant ni build .NET.

## Routage

| Diff | Contrôles attendus |
| --- | --- |
| DOCS_ONLY | Classifieur, canaris de sécurité/chemins/liaisons docs, **ci-policy-gate** |
| PS_ONLY | Contrôles docs + CI-0 à CI-4 sous Windows PS5.1, Pester signé, canaris et préflights |
| DOTNET_ONLY | Docs + restauration verrouillée/build/analyse/tests .NET **non vides**, uniquement lorsqu'un vrai projet existe |
| SHARED/MIXED/UNKNOWN/WORKFLOW/SECURITY | FULL, tous les checks applicables ; refus des inconnus non analysables |
| RELEASE | Référence protégée + gate complet + build et artefact vérifiés, séparé des PR |

Le gate ne peut pas devenir SUCCESS si un check **obligatoire** est failed, cancelled, skipped ou absent. Un test .NET non applicable n'est **pas** un succès fictif : expliquer son absence.

## Vérifier l'initialisation et les chemins Git

Le premier push peut avoir un SHA antérieur entièrement nul ; son interprétation doit rester fail-closed. Les références Git figées, baselines/manifestes, chemins et règles de diff doivent correspondre au dépôt et au HEAD testés. Les changements de policy/permissions, de chemins inconnus ou de workflows exigent FULL, pas DOCS_ONLY.

## Sécurité forks et publication

Permissions contents:read par défaut, aucune écriture ou secret dans la CI de PR de fork, ne pas exécuter de code non fiable dans un contexte privilégié, checkout sans credentials persistés, actions épinglées. Publication Release uniquement via job distinct autorisé avec permissions minimales et tag/HEAD/ZIP/manifest/SHA-256 cohérents. **Aucune Release ni build .NET n'est autorisé ou revendiqué dans cette documentation.**

Une exécution refusée avant démarrage des runners ne démontre ni un échec du code testé ni la réussite du pipeline. Les preuves de chaque HEAD doivent venir de ses **propres runs**.
