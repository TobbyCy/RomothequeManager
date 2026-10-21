# Vue 04 — Bibliothèques et snapshots (conception validée, non implémentée)

L'interface prévue est **hybride** : cartes de bibliothèques, liste et inspecteur rapide à droite, puis une page détaillée accessible. Des onglets structurent **Disques, Snapshots, Statistiques, Diagnostics**.

## États orthogonaux

**Disponibilité physique** : ONLINE, OFFLINE, INACCESSIBLE ou état non observé (ne pas transformer silence en OFFLINE). **Inventaire** : pas de résultat disponible, scan en cours, complet, partiel, échec ; fraîcheur indépendante (STALE/unknown selon règle future). Un historique complet peut rester consultable quand le support est déconnecté, **si la copie de projection/index est accessible**.

## Snapshots et comparaison

Mettre en avant le dernier snapshot cohérent avec source/date et couvrir les générations historiques. Une comparaison sélectionne **deux snapshots**, affiche un résumé, puis détails par plateformes/jeux/fichiers. En dehors de leur intersection de couverture prouvée, le résultat doit être « non comparable / non observé », pas « supprimé ». La dernière tentative partielle ou échouée ne détruit pas automatiquement le dernier inventaire complet vérifié.

## Supports mobiles

libraryId est une identité logique, pas une lettre Windows. La réassociation USB avec lettre différente exige une vérification suffisante et éventuellement confirmation explicite ; conflit d'identité => REFUS. La page ne lance pas de scan au démarrage.

## Réception future

LIB-01 doubles statuts ; LIB-02 ancien index Offline ; LIB-03 compare deux générations bornées ; LIB-04 conflit USB refusé ; LIB-05 source inconnue qualifiée ; LIB-06 pas de faux jeux supprimés. **NOT_EXECUTED.**
