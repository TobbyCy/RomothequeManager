# Vue 01 — Dashboard (conception validée, non implémentée)

Le logiciel devra ouvrir sur un **tableau de bord centré sur la collection**, plutôt que sur la santé technique des processus.

## Hiérarchie

1. **Collection** : jeux connus, plateformes, jaquettes disponibles en cache, raccourcis favoris et recherche.
2. **Bibliothèques / supports** : visibilité physique des racines, état de leurs inventaires et date des snapshots.
3. **Préparations d'export** : simulations historiques ou plans sans les assimiler à des copies réalisées.
4. **Anomalies** : résumé non alarmiste et accès au centre de diagnostics, avec provenance.

## Règles de fiabilité

Une bibliothèque OFFLINE peut présenter une collection historique à condition que l'index reste accessible ; jamais « zéro jeu » automatiquement. Un nombre n'apparaît comme actuel qu'avec source, date et couverture ; une donnée inconnue se présente comme inconnue. Un bouton d'actualisation de l'UI ne relance pas implicitement un scan de ROM. Les cartes restent navigables en mode dégradé et expliquent les données indisponibles.

## Réception future

DASH-01 accueil direct ; DASH-02 ancien catalogue + source OFFLINE ; DASH-03 index absent ≠ collection vide ; DASH-04 source NON OBSERVÉE ; DASH-05 aucun scan au démarrage ; DASH-06 liens clavier/souris vers Collection/Bibliothèques/Diagnostics. **Tous ces tests sont NOT_EXECUTED.**
