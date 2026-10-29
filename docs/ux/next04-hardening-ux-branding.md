# NEXT-04 — UX / branding hardening (DEV 3, PR #93)

**Périmètre :** ajustements Avalonia en lecture seule sur la branche #93 existante. Aucune Release, nouvelle branche, ROM personnelle, copie d'illustrations privées, scanner parallèle, scraper ni export activé.

## Sources de conception et limite de comparaison

Le lot UX-V2-2026-10-28 comprend **dix maquettes JPEG privées** numérotées UX-V2-01..10, conservées par le Pilotage. Les métadonnées, dimensions et descriptions sont vérifiées dans le manifeste privé ; **aucun original n'est incorporé ou lié depuis le Produit**. Les outils de lecture accessibles dans cette session ne permettent pas de rendre directement leurs binaires : comparaison **de la structure et des spécifications documentées**, non certification pixel-à-pixel des dix images. Les maquettes comportent des visuels commerciaux sans droit de redistribution établi.

| Référence | Intention utile | Réalisation autorisée / limite |
| --- | --- | --- |
| UX-V2-01 | Dashboard : hiérarchie, tuiles, navigation | Quatre indicateurs qualifiés, panneaux collection/support/attention ; aucune couverture physique inventée |
| UX-V2-02 | Collection : grille, recherche, filtres | Cartes responsives, filtre par systèmes du XML, pages 50 et retour de défilement ; panneau latéral futur non revendiqué |
| UX-V2-03 | Fiche : jaquette, données, médias | Composition image/métadonnées adaptative ; jaquette/miniature/logo exclusivement référencés, vidéo non lue |
| UX-V2-04 | Bibliothèques : sources et états | Import sur choix manuel + confirmation, réimport, durée de session explicite, simulation NEXT-03 séparée |
| UX-V2-05 | Diagnostics : synthèse, anomalies | Section lisible par code/plateforme, message sans déduction de suppression ; inspecteur avancé hors lot |
| UX-V2-06 | Export 1/4, sélection | Référence de cohérence des états seulement, non implémenté |
| UX-V2-07 | Export 2/4, configuration | Non implémenté |
| UX-V2-08 | Export 3/4, simulation | Non implémenté |
| UX-V2-09 | Export 4/4, validation | Non implémenté |
| UX-V2-10 | Export variante | Non implémenté |

## Diagnostic de branding

- Source officielle : `RM_V2_Horizontal_Fond_Sombre.png`, **1960 × 576**, proportions ~3,403:1. La barre latérale antérieure utilisait 220 DIP et un `Image` 188 × 58 DIP ; l'image apparaissait petite et les marges pouvaient dominer. Le rééchantillonnage haute qualité était déjà activé.
- Correction : barre de navigation 264 DIP, image **228 × 67 DIP** au ratio naturel dans un conteneur centré. Aucune modification du PNG, SVG ou ICO approuvé ; pas de ressource privée liée. Largeur principale limitée à 1580 DIP pour éviter les lignes de 5000 px sur écrans ultralarges.
- Icône Windows : `ApplicationIcon` officiel du csproj et `WindowIcon` du fichier embarqué préservés. Le contrôle Windows vérifie l'ICO à **7 tailles**, l'extraction de l'icône associée à l'EXE, la copie byte-for-byte du PNG et de l'ICO dans le publish.
- DPI : captures CI Avalonia à échelle de rendu 100, 125 et 200 % (DIP logique identique, bitmap physique agrandi) ; ce n'est **pas** une inspection humaine du DPI de Windows, du moniteur réel ou de la netteté perceptive.

## API/présentation changées (interne à Avalonia)

- `MainWindow.Hero` : titre, surtitre et contexte, sans modification de navigation.
- `MainWindow.Notice` : informations et avertissements, sans changer les diagnostics du moteur.
- `MainWindow.PrimaryButton` : action principale visuelle et focus natif.
- `VisualDashboard.Metrics` : quatre compteurs de provenance qualifiée, distincts d'un inventaire physique ; pas de nouvelle API de données.
- `MainWindow.ImportedDiagnostics` : projection des codes `EsDeDiagnosticPresentation`, limitée en densité et accessible depuis Bibliothèques ; pas de nouveau parser et aucune lecture de chemin supplémentaire.
- `ImportedDetail` : mise en page adaptative sans perdre `Genre/Developer/ReleaseDate`, `CoverFor`, le fallback ni les identités.
- `Next04Capture` et workflow de preuve : 13 captures (11 vues fonctionnelles historiques + 2 rendus DPI simulés), contrôles logo/ICO/EXE, SHA-256 ZIP standard, sans `NEXT04_CI` dans le ZIP.

Les contrats publics `EsDeGame`, `EsDeImportSession`, `ImportedCatalogueUiState`, `InventoryApplicationService` et `InventoryReader` **ne sont pas modifiés**. Le backend NEXT-05 reste hors mandat. La nouvelle version backend de DEV 1, si elle survient, devra être intégrée sous nouveau GO puis testée sur le SHA combiné.

## Recette et limites

Sur **HEAD final de #93**, exigence : Adaptive CI y compris `ci-policy-gate`, tests .NET, Windows natif, IDEAL (30/13), CHAOS (30), MYSTERY refusé et dernier valide conservé, liste 155/page 2/offset non nul, métadonnées ES-DE présentes/absentes, réimports, simulation NEXT-03, 13 PNG et ZIP avec notices. Des runs antérieurs ne prouvent pas un nouveau HEAD. Aucun média privé, ROM, BIOS ou maquette en artefact.

**Toujours NOT_EXECUTED :** comparaison pixel-à-pixel avec les dix maquettes, validation humaine DPI 125/200 %, clavier Tab/Shift+Tab, souris, écran physique 5120 × 1440, compatibilité de la bibliothèque personnelle. La capture CI montre une interface rendue, pas l'approbation de son esthétique ni son accessibilité complète.
