# Vue 05 — Préparation des exports (quatre pages distinctes)

**État : maquette de parcours validée, aucun moteur d'export réel autorisé.** Ne pas condenser les quatre étapes dans une page à miniatures.

## Navigation

Stepper permanent : **1 Sélection → 2 Configuration → 3 Simulation → 4 Validation et rapport**. Retour arrière sans perte des choix, valeurs et dépendances persistantes seulement dans un futur brouillon explicitement enregistré.

- **Sélection** : plateformes, filtres, jeux, variantes, dépendances observables ; choix utilisateur explicites et ambiguïtés visibles.
- **Configuration** : profils de destination réutilisables, exceptions ponctuelles sans modifier le profil enregistré implicitement ; contraintes de format et d'espace connues vs estimées.
- **Simulation** : liste prévisionnelle des éléments, dépendances multidisques, refus, inconnus, couverture, estimation d'espace **qualifiée** et source datée.
- **Validation / rapport** : résumé des décisions et refus ; distinction stricte entre plan proposé, validation technique et opérations effectuées. Une simulation n'est pas une copie.

## Sécurité

En présence de source Stale, partielle, mauvaise génération ou identité USB incertaine, une prévisualisation historique peut exister mais aucun plan d'opération physique ne doit être considéré opérable. Aucun CommitGate, replay, E6, copie/conversion ou scrape n'est autorisé par cette UI. Le ZIP POC de lecture synthétique n'inclut pas d'export réel.

## Réception future

EXP-01 quatre pages distinctes ; EXP-02 retour sans perte ; EXP-03 simulation explique exclusions ; EXP-04 refus source Stale ; EXP-05 aucune écriture réelle ; EXP-06 rapports datés avec provenance. **NOT_EXECUTED.**
