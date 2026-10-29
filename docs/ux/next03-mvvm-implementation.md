# NEXT-03 / MVVM IMPLEMENTATION — simulation d'interface, sans scan

Références : contrat fonctionnel #82, service canonique DEV 1 #85 (décision actualisée dans #83), fondation MVVM #84 reçue dans sa descendante #86. Les dépendances #76/#75/#77/#85 sont réunies sur la branche de préparation, sans fusion dans main. Ce lot est une prévisualisation et un assemblage de code en revue, non une réception fonctionnelle de NEXT-03.

## Découpage

- View : InventoryPreviewWindow.axaml et code-behind, accessibles par un unique bouton dans Bibliothèques. Palette Darcula Retro et logo/ICO validés réutilisés. Bindings compilés locaux x:DataType / x:CompileBindings, sans changement global de la configuration Avalonia.
- ViewModel : InventoryViewModel et ses commandes Start/Cancel/SelectObservation/ShowCurrent/ShowLastAttempt. ConsentConfirmed (case de confirmation à usage unique) empêche un départ par simple sélection ; INPC et ICommands standards, sans logique scanner ni référence Avalonia.
- Application Service : InventoryPreviewService implémente l'interface unique de DEV 1 #85 pour permettre une GUI interactive isolée. Il ne fait aucune lecture de fichiers, aucun Directory.Exists, aucun import XML, aucun InventorySession. Le chemin sélectionné par le picker natif n'est examiné que syntaxiquement. Le simulateur fabrique en mémoire des observations explicitement étiquetées NEXT03_UI_SIMULATION et expose Complete, Partial, Failed, Offline et Stale simulés. Il maintient un single-flight et distingue Current/LastAttempt ; annulation asynchrone contrôlée. `InventoryApplicationSeam.cs` est supprimé. Le vrai `InventoryApplicationService` est présent dans la même assembly, mais n'est pas injecté dans cette fenêtre de simulation ; son constructeur public refuse toujours les scans.
- Domain : InventoryReader.cs et tous les modèles NEXT-02 restent intacts.

## Parcours de la prévisualisation

Ouvrir Bibliothèques et cliquer « NEXT-03 · Prévisualisation MVVM (SIMULATION) » ; sélectionner volontairement un dossier local. Le picker ne déclenche aucun scan. Choisir le scénario simulé (Complete/Partial/Failed/Offline/Stale), cocher la confirmation « simulation sans lecture du dossier », puis cliquer Simuler l'inventaire ; progression indéterminée et annulation disponibles. Observer les fichiers fictifs, la couverture ROMS fictive, les diagnostics, la provenance, le dernier complet et la dernière tentative. La consultation est séparée de l'import ES-DE. Fermer la fenêtre pour retrouver le parcours historique inchangé.

IMPORTANT : choisir un dossier dans cette fenêtre ne signifie **jamais** que son contenu a été observé. Les chemins inventés se trouvent sous ROMS/simulation/ et toutes les données sont signalées SIMULATION. Les valeurs ne valent aucune preuve pour un SSD réel, aucun média ou jeu. Aucun log contenant le chemin sélectionné n'est publié. La source réelle reste interdite.

## Tests

Tests en mémoire : InventoryViewModelTests (confirmation requise, commande, Inconnu pour null, états et annulation) et InventoryPreviewServiceTests (consentement, single-flight, Complete/Partial, Offline/Stale simulés). Aucune ROM ou bibliothèque personnelle, aucun scanner, aucune lecture de dossier. Compiler la nouvelle fenêtre sur Windows avec le SDK .NET verrouillé, faire tourner les suites existantes et comparer au HEAD exact.

Recette Windows humaine et E2E du vrai service : **NOT_EXECUTED** dans ce lot, notamment souris/clavier, Tab/Shift+Tab, DPI 125–200 %, 760×480 et 1120×720, fermeture pendant scan, vrai .m3u, IDEAL/CHAOS/MYSTERY matérialisés. Les cas NX03-01..11 et NX03-RACE-01 de #82 exigent le service DEV 1 intégré et preuves distinctes ; NX03-16 reste BLOCKED pour vraie bibliothèque.

CommunityToolkit.Mvvm 8.4.2 reste évalué mais NON installé : commandes BCL pour conserver locks, notices et distribution inchangés jusqu'à la revue des dépendances. Aucune refonte visuelle #78, fusion, Release ou scan réel.


### Alignement de la signature DEV 1 (#85)

Le service DEV 1 de la PR #85 expose PrepareSelection(InventoryScope selectedScope), et non deux arguments séparés. La fondation de #86 et le simulateur consomment directement cette interface et ses records ; le seam provisoire est retiré. Le simulateur ne fait toujours aucune lecture de dossier, contrairement au préflight borné du vrai service. La fenêtre et le backend sont compilés ensemble ; seul le banc MSTest injecte une autorisation éphémère pour les fixtures qu'il crée.

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

1. Après GO propriétaire, intégrer #76 → #75 → #77 → #85 → #86, avec CI PR et push main à chaque étape. Les apports de #84 sont conservés via #86 ; aucune seconde fusion de la fondation n'est nécessaire. Leur réception et le classement de #84 restent au Contrôleur.
2. Le contrat unique `IInventoryApplicationService` et les records proviennent de #85 ; `MvvmFoundation/InventoryApplicationSeam.cs` est déjà retiré de cette préparation. Le service conserve la propriété de CTS, single-flight, Current/LastAttempt et refus des racines non autorisées. Le conflit MainWindow conserve ScenarioSelector(), le bouton SIMULATION et LibraryPage.Build().
3. Garder `InventoryPreviewService` exclusivement pour les tests/démonstrations **sans possibilité d'exécution comme scanner**. Installer le vrai service derrière une composition de production distincte ; aucun commutateur public, variable d'environnement ou manifest ne peut autoriser un dossier réel. Vérifier auprès de DEV 1 son périmètre de fixtures.
4. Construire une vraie section Bibliothèques→Collection/Observations (GO séparé), en conservant Darcula Retro, Dashboard, fiches et import ES-DE, et en supprimant le marqueur simulation **uniquement quand une observation physique est réellement prouvée**.
5. Exécuter les cas P1 NX03-01..11 + RACE-01 sur fixtures synthétiques et preuve Windows corrélée SHA. Aucun scan de bibliothèque personnelle ni Release avant GO distinct.

## Préparation de la recette commune — 29.10.2026

Le banc `InventoryApplicationServiceTests` utilise désormais **le vrai service et le vrai ViewModel**, avec dispatch immédiat de test, sans fenêtre Avalonia. Il vérifie le refus du constructeur public après consentement, Complete IDEAL, blocage en playlist, refus d'un second départ et de changement de racine, annulation Partial avec Current conservé, Failed/Offline après indisponibilité temporaire de la fixture, consultation du dernier complet et changement vers MYSTERY sans fuite de contexte. Le manifeste et les hashes des seules fixtures créées sont vérifiés avant/après par le banc existant. Attendre les résultats CI du HEAD avant de qualifier ces nouveaux contrôles PASS.

### Matrice à exécuter dans Windows après intégration autorisée

| Cas | Contrôle et résultat exigé |
| --- | --- |
| NX03-01/02 | Démarrage/navigation/picker sans scan ; annuler conserve la sélection ; UNC, URI et reparse refusés ; consentement distinct pour chaque tentative |
| NX03-03 | Un seul inventaire actif ; progression indéterminée ; choix/effacement interdits jusqu'à la fin effective |
| NX03-04 | IDEAL : 32 observations, total 32, jeux identifiés Inconnu ; import ES-DE facultatif séparé : 30 références |
| NX03-05 | CHAOS : 33 observations et 3 groupes candidats ; aucune suppression ou identité inventée, source XML distincte |
| NX03-06/07 | MYSTERY : 32 observations sans XML et aucun titre oracle ; playlist + deux membres distincts d'un nombre de jeux |
| NX03-08/09 | Annulation pendant lecture réelle de playlist : Partial/CANCELLED ; échec : Failed avec total inconnu ; dernier Current complet toujours consultable |
| NX03-10/11 | Changement A→B et fermeture sans événement tardif ni double tâche ; hashes des fixtures inchangés ; aucune opération sur une bibliothèque personnelle |
| NX03-RACE-01 | Annulation après Complete ne le dégrade pas ; jouer aussi la course clic/fin et la fermeture sur un même HEAD |
| NX03-12..15 | Offline et Stale qualifiés par leur preuve, jamais déduits ; Stale reste simulé actuellement. Médias, clavier, focus, scroll, 760×480, DPI et ZIP autonome hors Internet à observer réellement |
| NX03-16 | BLOCKED : aucune bibliothèque personnelle autorisée |

Préparation des données : utiliser `tools/demo-library/Invoke-Demo02Recipe.ps1` sur une **nouvelle sortie locale exclusive**, puis `node tools/demo-library/generate.cjs --verify` sur cette sortie ; conserver le SHA-256 des manifestes avant/après. Ne pas utiliser une racine existante ou reconnue par son seul nom. Le banc interne de test limite son droit aux racines qu'il vient de générer ; ce droit n'existe pas dans le constructeur public ni par variable d'environnement, manifest ou flag utilisateur.

Le harness **GUI** contre ces fixtures reste à préparer sous mandat de raccordement ; la fenêtre distribuée ici garde sa bannière SIMULATION et ne lance pas le moteur. La recette humaine NX03 et MVVM-W01..W07 demeure **NOT_EXECUTED**. Après autorisation d'intégration, construire le ZIP Windows x64 depuis le HEAD intégré H ; relever H, commit réellement testé, arbre CI, SHA-256 du ZIP et manifestes synthétiques, puis vérifier les interactions réelles. Aucun ZIP intégré, clic humain ou statut PASS Windows n'est revendiqué par cette préparation. Aucune Release.
