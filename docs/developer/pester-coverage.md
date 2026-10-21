# Couverture du noyau PowerShell — lecture de la mesure

Les travaux historiques ont établi une **méthode de couverture reproductible** sur fixtures synthétiques. Un indicateur ancien rapportait 289 points couverts parmi 5 797 points instrumentables ; **ce chiffre est un repère historique, pas une couverture recalculée ni une preuve de la nouvelle CI**.

## Méthode attendue

Séparer comptage et mesure ; conserver une règle claire de périmètre (sources éligibles, exclusions justifiées, chemins et instrumentations). Démarrer deux processus PowerShell propres lorsque le protocole l'exige afin de détecter les dépendances cachées et les variations de résultat. Préserver les canaris négatifs d'omission et la cohérence des sommes.

## Présenter les résultats

Consigner commit exact, version PowerShell/Pester, périmètre de fichiers, points total/couverts/non couverts, cas réellement exécutés, différences entre passages, limites et objectifs de progression. **La couverture n'est pas un indicateur de sûreté physique**, et son pourcentage n'est pas à additionner aux scénarios de validation UX seulement documentés.

Voir [référence de protocole](coverage-reference.md) et [Pester](pester-execution.md).
