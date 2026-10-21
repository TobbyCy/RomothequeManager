# Vue 06 — Diagnostics et centre de contrôle

**État : conception générale validée sous réserves de corrections graphiques, non implémentée.** Centre de contrôle hybride : cartes de synthèse au-dessus, liste d'anomalies au centre, inspecteur de contexte/preuves à droite.

## Informations et priorités

Distinguer erreurs documentées, avertissements, état de couverture et incidents de lecture ; filtrer par bibliothèque, statut, date, impact et type. Le suivi humain (examiné, ignoré, à vérifier) est différent de l'état objectif de la donnée. Marquer « traité » dans l'interface ne transforme pas un fichier en ROM valide ni une observation historique en vérification physique.

## Async et qualité

Un rafraîchissement visuel depuis un index connu n'est pas un scan ROM. Les opérations de lecture potentiellement longues doivent rester asynchrones et bornées, avec possibilité d'annulation future selon vrais contrats. Pas de scan automatique en arrière-plan ni téléchargement réseau implicite. Le centre évite de considérer OFFLINE seul comme une erreur critique et n'éteint pas une anomalie faute de réanalyse.

## Traçabilité

Chaque anomalie est reliée à sa preuve, portée, date et limite (notamment partial/unknown). Les regroupements ont une raison explicite ; les compteurs et notifications ne doivent pas inventer des taux de résolution. Les libellés et illustrations de maquette peuvent être corrigés avant intégration sans changer l'intention générale.

## Réception future

DIAG-01 liste/inspecteur ; DIAG-02 traçabilité ; DIAG-03 suivi humain distinct ; DIAG-04 Offline non critique par défaut ; DIAG-05 refresh non scan ; DIAG-06 anomalies non résolues par silence. **NOT_EXECUTED.**
