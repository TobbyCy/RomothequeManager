# Contribuer à ROMothèque Manager

Ce projet accepte les propositions documentaires, les correctifs et les tests dans un périmètre clairement défini. **Utilisez exclusivement des données fictives**, jamais de ROM, BIOS, bibliothèques personnelles ou secrets.

## Préparer une contribution

1. Consultez le [README](README.md), le [statut réel](docs/product-status.md), la [roadmap](docs/roadmap.md) et les [ADR](docs/decisions/README.md).
2. Recherchez une Issue existante. Sinon, décrivez le besoin, son résultat attendu, ses critères et les cas où la solution doit refuser d'agir.
3. Récupérez l'URL de clonage depuis GitHub et créez une branche dédiée depuis une version de main vérifiée ; ne mélangez pas plusieurs sujets.
4. Examinez le diff avec Git : commandes utiles ci-dessous. Les fichiers PowerShell 5.1 peuvent exiger BOM, CRLF et octets strictement conservés.
5. Exécutez les tests **réellement présents et applicables** sur votre checkout et consignez version, résultat et limites. Voir le [guide développeur](docs/developer/README.md).
6. Ouvrez une PR Draft : Issue Produit, comportement, changements, tests positifs/négatifs, état PASS/FAIL/BLOCKED/NOT_EXECUTED et mise à jour des guides.

    git status --short
    git diff --check
    git diff

Ces commandes inspectent seulement Git ; elles ne remplacent ni Pester ni la CI.

## Revue

Une PR ne fusionne qu'après les contrôles applicables et la revue du mainteneur autorisé. Les changements de code doivent mettre à jour [statut produit](docs/product-status.md) et [changelog](CHANGELOG.md) **dans leur même diff lorsque le gate le demande**. Les PR de forks ne doivent pas bénéficier de secrets ou privilèges CI élevés.

Pour les vulnérabilités, utilisez la [politique de sécurité](SECURITY.md), pas une Issue ordinaire. Ne joignez jamais logos, images ou code tiers sans avoir établi les droits de redistribution ; la licence du projet reste à décider.
