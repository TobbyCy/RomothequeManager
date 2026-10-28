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


### Alignement de la signature DEV 1 (#85)

Le service DEV 1 de la PR #85 a été inspecté : il expose PrepareSelection(InventoryScope selectedScope), et non deux arguments séparés. La fondation de #86 et le simulateur utilisent désormais exactement cette signature ; les autres membres State, StateChanged, StartConfirmedScanAsync, RequestCancellation, ClearSelection, DisposeAsync et les records de base restent compatibles. Le simulateur ne fait toujours aucune lecture de dossier, contrairement au préflight borné du vrai service. Les déclarations provisoires devront être retirées lors du raccordement final afin de ne conserver qu'une seule interface.

## Consolidation DEV 3 — MVVM / recette pré-raccordement

Les modifications de cette tranche ne changent **aucun moteur**, aucune source réelle ni le service proposé par DEV 1 dans #85. L'écran reste **une simulation** et n'autorise pas de lecture ROM. La nouvelle interface du VM s'insère dans le contrat provisoire identique aux signatures de service de #85 ; le futur service fournira ses propres tests de consentement et refus deny-by-default.

### Corrections vérifiables dans les tests sans GUI

1. **Notifications hors ordre** : un événement `Running` mis en file sur le dispatcher après un `Completed` du même `SelectionVersion` est écarté si son objet n'est plus l'état courant publié par le service. La garde de génération seule n'aurait pas suffi ; le VM ne devine jamais des états en recalculant la progression.
2. **Consentement lié à la sélection** : sélection acceptée ou refusée retire l'ancienne confirmation lorsque l'utilisateur tente une nouvelle racine. Annuler le picker natif ne fait **aucun** appel au service et conserve la sélection ; aucun scan automatique. Après erreur picker, message neutre `FOLDER_PICKER_UNAVAILABLE` sans révéler le chemin issu de l'exception.
3. **Changement pendant un scan** : picker et effacement désactivés ; la tentative directe par API du VM est refusée pendant `Running` et `CancellationRequested`. Demander `Annuler`, attendre la fin effective, puis sélectionner B ; le dernier résultat de A ne doit pas apparaître dans B.
4. **Source, génération et compteurs** : `Dernier complet` et `Dernière tentative` sont nommés et datés séparément. `Failed` sans observation initiale affiche **« Fichiers observés : Inconnu »**, jamais un faux 0 complet. `Partial` garde le nombre effectivement observé, mais **TotalFileCount=null** reste Inconnu. Une absence d'observation ou de `Current` est explicitement libellée sans créer une fiche jeu. Diagnostics et `InventoryCoverage.Status` restent des preuves de ROMS/ uniquement.
5. **Bindings** : `InventoryPreviewWindow.axaml` reste en bindings compilés locaux `x:DataType`. Les nouvelles propriétés de texte et de sélection déclenchent `PropertyChanged` ; les actions utilisent `ICommand` et `CanExecuteChanged`. ListBox d'observations, détails de provenance, code/sévérité/portée du diagnostic, couverture et message vide sont liés au VM. Les panneaux de commandes et de disponibilités utilisent `WrapPanel` pour limiter la troncature en fenêtre 760×480.

### Matrice Windows indépendante (non encore exécutée manuellement)

| ID | Action manuelle dans la fenêtre de simulation | PASS observable |
|---|---|---|
| MVVM-W01 | 1120×720 puis 760×480 ; DPI Windows 100 %, 125 %, 200 % | Bandeau SIMULATION toujours lisible, scrolling vertical accessible, aucun bouton Annuler hors écran sans possibilité de défiler |
| MVVM-W02 | Tab, Shift+Tab, Entrée, Espace, flèches dans la liste | Focus visible, confirmation obligatoire, commande Annuler accessible, sélection d'observation et retour possible |
| MVVM-W03 | Choisir un dossier, annuler le dialogue, choisir B | Annulation de picker conserve A ; changement B révoque consentement, efface toute observation A ; **aucune énumération** |
| MVVM-W04 | Démarrer simulation, cliquer Annuler puis tenter de changer le dossier | Affichage d'annulation demandée jusqu'au résultat partiel, commandes de changement indisponibles, aucune double tâche |
| MVVM-W05 | Rejouer Complete puis Failed, Partial, Offline, Stale | Dernier complet distinct de Dernière tentative, horodatage et diagnostics, Inconnu ≠ 0, fichier ≠ jeu |
| MVVM-W06 | Lancer depuis Bibliothèques, fermer l'aperçu, revenir à Dashboard/Collection/fiches puis ES-DE | Parcours historiques intacts ; aucune connexion entre SIMULATION et import XML |
| MVVM-W07 | Démarrer puis fermer, lancer à nouveau | Aucune tâche persistante, aucun fichier temporaire/ROM lu, état vide initial explicite |

Ces tests sont **NOT_EXECUTED** tant qu'un humain ou une preuve instrumentée ne les a pas joués sur Windows. La compilation Avalonia et les tests du ViewModel ne prouvent pas un clic réel, un focus ni une visibilité DPI. Les critères NX03-01..11 + RACE-01 de #82 sont une recette **distincte** du vrai moteur sur fixtures, et NX03-16 reste BLOCKED.

### Remplacement du simulateur après intégration autorisée

1. Intégrer #76, #75 et #77 (sous GO Contrôleur/Agent), puis la fondation #84 ; résoudre les conflits docs/CI sans écraser d'autres preuves.
2. Intégrer la signature et **seule déclaration officielle** `IInventoryApplicationService` et les records depuis #85. **Supprimer `MvvmFoundation/InventoryApplicationSeam.cs`**, garder uniquement le contrat DEV 1. Le service conserve la propriété de CTS, single-flight, Current/LastAttempt et refus des racines non autorisées.
3. Garder `InventoryPreviewService` exclusivement pour les tests/démonstrations **sans possibilité d'exécution comme scanner**. Installer le vrai service derrière une composition de production distincte ; aucun commutateur public, variable d'environnement ou manifest ne peut autoriser un dossier réel. Vérifier auprès de DEV 1 son périmètre de fixtures.
4. Construire une vraie section Bibliothèques→Collection/Observations (GO séparé), en conservant Darcula Retro, Dashboard, fiches et import ES-DE, et en supprimant le marqueur simulation **uniquement quand une observation physique est réellement prouvée**.
5. Exécuter les cas P1 NX03-01..11 + RACE-01 sur fixtures synthétiques et preuve Windows corrélée SHA. Aucun scan de bibliothèque personnelle ni Release avant GO distinct.
