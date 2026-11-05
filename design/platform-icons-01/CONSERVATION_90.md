# Conservation graphique — compatibilité avec #90

Référence publique : [#90, audit branches](https://github.com/TobbyCy/RomothequeManager/issues/90). Cette livraison n'ajoute **aucune branche**.

## Protection immédiate

- Réutiliser `graphiste/identite-v3-proposition`, par ajout d'un dossier indépendant `design/platform-icons-01/`.
- Parent de travail : `da8ffb80ff5e4d669c340d6d07e90cc4fe3a0a07`.
- Les **12 fichiers V3** sous `design/identity-v3-proposal/` restent inchangés ; leurs blobs Git sont listés dans `v3-preservation-manifest.json`. Le SHA de la nouvelle tête sera donné dans le retour de livraison.
- Aucun changement dans le code, les workflows, les assets officiels, la documentation racine, le catalogue de plateformes ou `main`. Pas de rebase ni de fusion de main ancien dans main courant.

## Conservation hors branche avant le gate ONLY_MAIN

Une archive `Conservation_Graphique_V3_PI01.zip` rassemble les 12 fichiers V3 au parent exact et le lot de préproduction (sources, PNG, planches, documents et empreintes). Elle est remise au propriétaire avec le pack de prototypes ; chaque entrée possède une empreinte SHA-256 et les fichiers V3 sont également vérifiés contre leurs blobs Git. Ce ZIP est une archive de conception, pas une Release produit.

Pour permettre ultérieurement une seule branche `main`, le Contrôleur/propriétaire choisira soit une intégration ciblée des seuls éléments retenus avec revue applicable, soit la conservation de cette archive vérifiée et, si autorisé, un tag d'archive au SHA exact. Il ne faut pas fusionner la branche historique complète par commodité.

**Avant toute suppression :** le propriétaire doit accepter la forme de conservation, vérifier l'accès et la restauration du ZIP, puis donner un GO ciblé à #90. Tant que ce choix n'est pas reçu, classement **OWNER_DECISION / PRÉSERVER**. Aucune suppression, création de tag ou adoption d'identité n'est autorisée par ce document. La préproduction n'abaisse aucun gate NEXT-05.
