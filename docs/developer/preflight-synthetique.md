# E1 — préflight de sécurité sur fixtures synthétiques

Le préflight E1 est **distinct** d'un inventaire complet et d'un export physique. Il vérifie les préconditions, les chemins, les identités et les refus attendus à partir de chemins/fixtures inventés.

## Étendue du protocole

Le référentiel historique compte **18 scénarios préflight synthétiques**. Leur succès à l'intérieur du nouveau dépôt doit être **retesté**, notamment sur les fonctions liées aux symlinks/junctions et comportements spécifiques à Windows ; un environnement qui ne peut pas créer le lien de test doit reporter **PARTIAL/BLOCKED**, pas 18/18 PASS.

Les vérifications doivent confirmer l'absence d'écriture de données personnelles, l'intégrité des fichiers JSON et des chemins autorisés, le refus d'un chemin externe ou de métadonnées incohérentes et des logs expurgés. Les essais sont jetables et ne touchent jamais ROM/SSD/StateRoot réels.

## Rapports

Chaque scénario indique PASS/FAIL/BLOCKED/NOT_EXECUTED avec son oracle. Le résultat global précise le nombre de réussites, échecs, refus et limites. Une CI verte sur le nouveau HEAD ne donne pas d'autorisation de lancer un préflight sur une bibliothèque physique.

Guide technique : [CI adaptative](ci-adaptative.md) ; sécurité : [ADR-0001](../decisions/0001-isolation-des-donnees-reelles.md).
