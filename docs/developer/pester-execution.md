# Pester 4.10.1 — découverte, exécution et canaris

**Spécification des garanties héritées ; preuve de nouvelle exécution attendue dans ce dépôt.**

Les suites métier historiques comportent **42 cas distincts** au dernier référentiel validé avant migration. Elles doivent être découvertes **et** exécutées dans Windows PowerShell 5.1 Desktop avec **Pester 4.10.1** identifié et vérification de provenance/signature selon la politique de CI. Une découverte qui réussit sans exécuter de test n'est pas PASS.

## Contrôles à conserver

1. Runner épinglé et identifié ; script local et manifestes des tests sous version.
2. Canaris d'omission : désactiver fictivement une suite/une étape dans un clone synthétique doit faire **échouer** le gate ; restaurer ensuite une copie propre, ne jamais committer le canari négatif.
3. Nombre réel de cas et résultats PASS/FAIL/SKIPPED, sans double comptage de deux processus de mesure.
4. Aucune dépendance à des ROM/BIOS/StateRoot ni à un SSD physique ; comparer hashes des sources critiques.
5. Rapport final lié au HEAD/base/checkout réellement testés ; un job Windows qui n'a pas démarré n'a rien validé.

Ce document ne donne pas une commande d'exécution sur disque réel. Voir [couverture](pester-coverage.md) et [CI adaptative](ci-adaptative.md).
