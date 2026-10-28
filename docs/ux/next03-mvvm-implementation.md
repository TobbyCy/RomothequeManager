# NEXT-03 / MVVM IMPLEMENTATION — simulation d'interface, sans scan

Références : contrat fonctionnel #82, contrat service provisoire DEV 1 #83, fondation MVVM #84, intégration #75/#76/#77 encore en attente. Ce lot est une prévisualisation sur branche séparée, non une réception fonctionnelle de NEXT-03.

## Découpage

- View : InventoryPreviewWindow.axaml et code-behind, accessibles par un unique bouton dans Bibliothèques. Palette Darcula Retro et logo/ICO validés réutilisés. Bindings compilés locaux x:DataType / x:CompileBindings, sans changement global de la configuration Avalonia.
- ViewModel : InventoryViewModel et ses commandes Start/Cancel/SelectObservation/ShowCurrent/ShowLastAttempt. ConsentConfirmed (case de confirmation à usage unique) empêche un départ par simple sélection ; INPC et ICommands standards, sans logique scanner ni référence Avalonia.
- Application Service : InventoryPreviewService implémente les signatures de passage de DEV 1 #83 pour permettre une GUI interactive isolée. Il ne fait aucune lecture de fichiers, aucun Directory.Exists, aucun import XML, aucun InventorySession. Le chemin sélectionné par le picker natif n'est examiné que syntaxiquement. Le simulateur fabrique en mémoire des observations explicitement étiquetées NEXT03_UI_SIMULATION et expose Complete, Partial, Failed, Offline et Stale simulés. Il maintient un single-flight et distingue Current/LastAttempt ; annulation asynchrone contrôlée. Après stabilisation, remplacer le service et la définition provisoire de l'interface par le seul contrat Core final de DEV 1, sans créer de moteur parallèle.
- Domain : InventoryReader.cs et tous les modèles NEXT-02 restent intacts.

## Parcours de la prévisualisation

Ouvrir Bibliothèques et cliquer « NEXT-03 · Prévisualisation MVVM (SIMULATION) » ; sélectionner volontairement un dossier local. Le picker ne déclenche aucun scan. Choisir le scénario simulé (Complete/Partial/Failed/Offline/Stale), cocher la confirmation « simulation sans lecture du dossier », puis cliquer Simuler l'inventaire ; progression indéterminée et annulation disponibles. Observer les fichiers fictifs, la couverture ROMS fictive, les diagnostics, la provenance, le dernier complet et la dernière tentative. La consultation est séparée de l'import ES-DE. Fermer la fenêtre pour retrouver le parcours historique inchangé.

IMPORTANT : choisir un dossier dans cette fenêtre ne signifie **jamais** que son contenu a été observé. Les chemins inventés se trouvent sous ROMS/simulation/ et toutes les données sont signalées SIMULATION. Les valeurs ne valent aucune preuve pour un SSD réel, aucun média ou jeu. Aucun log contenant le chemin sélectionné n'est publié. La source réelle reste interdite.

## Tests

Tests en mémoire : InventoryViewModelTests (confirmation requise, commande, Inconnu pour null, états et annulation) et InventoryPreviewServiceTests (consentement, single-flight, Complete/Partial, Offline/Stale simulés). Aucune ROM ou bibliothèque personnelle, aucun scanner, aucune lecture de dossier. Compiler la nouvelle fenêtre sur Windows avec le SDK .NET verrouillé, faire tourner les suites existantes et comparer au HEAD exact.

Recette Windows humaine et E2E du vrai service : **NOT_EXECUTED** dans ce lot, notamment souris/clavier, Tab/Shift+Tab, DPI 125–200 %, 760×480 et 1120×720, fermeture pendant scan, vrai .m3u, IDEAL/CHAOS/MYSTERY matérialisés. Les cas NX03-01..11 et NX03-RACE-01 de #82 exigent le service DEV 1 intégré et preuves distinctes ; NX03-16 reste BLOCKED pour vraie bibliothèque.

CommunityToolkit.Mvvm 8.4.2 reste évalué mais NON installé : commandes BCL pour conserver locks, notices et distribution inchangés jusqu'à la revue des dépendances. Aucune refonte visuelle #78, fusion, Release ou scan réel.
