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

## Licence et droits des contributions

Le code et la documentation **originaux** dont les auteurs ou titulaires ont accordé les droits correspondants sont distribués sous [licence MIT](LICENSE) : **Copyright (c) 2026 Cyril Tobler** pour les créations originales du projet indiquées comme telles.

En proposant une contribution, confirmez que vous êtes habilité à en autoriser la reproduction, la modification et la redistribution conformément à la licence MIT applicable aux contributions acceptées. **Ne soumettez aucun élément tiers** (code, image, jaquette, logo, texte, ROM, BIOS ou donnée d'utilisateur) si vous ne disposez pas des droits et autorisations requis ; signalez clairement toute dépendance ou attribution à conserver. Une PR ne transfère pas automatiquement la propriété d'une œuvre tierce au mainteneur.

La licence du logiciel et de sa documentation est distincte des droits éventuels sur le nom et les éléments graphiques officiels ; consulter [BRANDING.md](BRANDING.md). Pour les droits relatifs aux ROM et aux contenus de jeux, voir [LEGAL.md](LEGAL.md).

## Revue

Une PR ne fusionne qu'après les contrôles applicables et la revue du mainteneur autorisé. Les changements de code doivent mettre à jour [statut produit](docs/product-status.md) et [changelog](CHANGELOG.md) **dans leur même diff lorsque le gate le demande**. Les PR de forks ne doivent pas bénéficier de secrets ou privilèges CI élevés.

Pour les vulnérabilités, utilisez la [politique de sécurité](SECURITY.md), pas une Issue ordinaire. Ne joignez jamais logos, images ou code tiers sans avoir établi les droits de redistribution ; la licence MIT du logiciel n'autorise pas implicitement la redistribution de ces contenus.
