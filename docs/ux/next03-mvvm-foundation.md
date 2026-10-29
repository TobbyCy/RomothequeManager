# NEXT-03 / MVVM FOUNDATION — décision d'architecture DEV 3

> **Actualisation du 29.10.2026, préparation #86 :** les apports de #84 sont conservés par leur descendance Git dans #86, sans seconde fondation. Le seam provisoire a été retiré ; le contrat unique est désormais celui de #85 dans l'assembly V2 existante, `PrepareSelection(InventoryScope)`. La proposition historique de Core séparé et les étapes provisoires décrites ci-dessous ne sont pas des prérequis actuels. Les tests F01 à F14 et P01 à P07 sont conservés. La fenêtre demeure une simulation sans lecture du dossier sélectionné ; la recette GUI du vrai backend n'est pas exécutée. Aucune fusion dans main.

**Statut : fondation isolée, PR Draft, aucun scan réel et aucun raccordement Avalonia.** Contrat fonctionnel [#82](https://github.com/TobbyCy/RomothequeManager/issues/82) ; service applicatif proposé par DEV 1 dans [#83](https://github.com/TobbyCy/RomothequeManager/issues/83) ; UX [#80](https://github.com/TobbyCy/RomothequeManager/issues/80).

## Découpage et limites

- **View (à intégrer plus tard)** : `MainWindow`/nouvelle section Avalonia, `StorageProvider.OpenFolderPickerAsync`, commande utilisateur et consentement, labels et `ProgressBar IsIndeterminate`. Les interactions Avalonia et le dispatch UI ne sont pas dans le ViewModel.
- **ViewModel (cette branche)** : `MvvmFoundation/InventoryViewModel.cs`, `INotifyPropertyChanged`, commandes `StartInventoryCommand`, `CancelInventoryCommand`, `SelectObservationCommand`, affichage facultatif `Current` vs `LastAttempt`, diagnostic/coverage et aucun jeu dérivé d'un chemin. `Action<Action> postToUi` injectée pour le thread de présentation, sans Avalonias dans la classe.
- **Service applicatif (DEV 1, à intégrer ultérieurement)** : la seule autorité du préflight local, de `InventorySession.Scan`, single-flight, CTS, autorisation fixtures, résultat final et version de sélection. `InventoryApplicationSeam.cs` reprend **temporairement les types de contrat de conception de #83** (interface, records, phases) afin de compiler le VM et son faux service. **Ce n'est ni un second moteur ni une implementation de service** : lors de l'intégration, transférer/remplacer cette déclaration par la seule version Core DEV 1, et ne jamais publier deux définitions concurrençantes.
- **Domain NEXT-02** : `InventorySnapshot`, `FileObservation`, `InventoryDiagnostic`, `InventoryCoverage` et règles inchangés dans `InventoryReader.cs`. La compilation est actuellement dans le WinExe Avalonia : l'isolation **au niveau du code source et des tests** est attestable, mais **pas celle de l'assembly**. DEV 1 décidera après intégration s'il déplace l'unique compilation du moteur vers Core sans créer un deuxième scanner.

**Développement interdit ici** : aucun raccordement au sélecteur réel, aucun `Task.Run` de scanner, aucune ouverture ROMS, aucun scan de disque personnel, aucune altération ES-DE, aucun changement aux modes Dashboard/Collection/demo/fiches/Bibliothèques/import ES-DE, aucune persistance, export, scrape, Release ni merge.

## CommunityToolkit.Mvvm — évaluation de dépendance

- **Candidat** : `CommunityToolkit.Mvvm 8.4.2`, licence MIT, bibliothèque légère fournissant `ObservableObject`, `RelayCommand`, `AsyncRelayCommand` et source generators (`[ObservableProperty]` / `[RelayCommand]`). Source : [NuGet version 8.4.2](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2). Les commandes async du Toolkit n'abolissent **ni le single-flight du service ni le contrat de consentement**.
- **Décision pour cette PR de fondation** : **ne pas ajouter de package maintenant**. Le dépôt verrouille NuGet avec `RestorePackagesWithLockFile` et `packages.lock.json`, et le ZIP Windows exige un inventaire réel des notices des binaires publiés. Éviter un nouveau lock et une dépendance tierce avant la validation du contrat DEV 1 et l'intégration #75/#76/#77. La fondation utilise **BCL** `INotifyPropertyChanged`/`ICommand` et un petit `InventoryCommand` interne (aucun framework maison global).
- **Décision future** : après la stabilisation et avec accord Agent/DEV 2, substituer `ObservableObject` / commandes Toolkit au code BCL **sans changer les propriétés publiques ni la sémantique**, ajouter `PackageReference` exact, restaurer/figer le lock, NuGet audit, vérifier licence et notices du ZIP *si et seulement si* les fichiers sont distribués, relancer tests et Windows au HEAD exact. Le Toolkit reste uniquement dans la présentation.

## Binding Avalonia future (non branché, non exécuté)

Avalonia **12.1.4** est configuré avec `AvaloniaUseCompiledBindingsByDefault=false` dans l'EXE. La future vue locale, après intégration, pourra utiliser des bindings compilés **sans changer les autres écrans** :

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:RomothequeManager.V2.MvvmFoundation"
             x:DataType="vm:InventoryViewModel"
             x:CompileBindings="True">
  <StackPanel>
    <TextBlock Text="{Binding StatusMessage}" />
    <ProgressBar IsIndeterminate="{Binding IsProgressIndeterminate}"
                 IsVisible="{Binding IsBusy}" />
    <Button Content="Inventorier en lecture seule"
            Command="{Binding StartInventoryCommand}" />
    <Button Content="Annuler" Command="{Binding CancelInventoryCommand}" />
    <TextBlock Text="{Binding ObservedFilesText}" />
    <TextBlock Text="{Binding TotalFilesText}" />
    <TextBlock Text="{Binding IdentifiedGamesText}" />
    <ListBox ItemsSource="{Binding Observations}" />
  </StackPanel>
</UserControl>
```

Ce snippet **n'est pas une vue compilée ni une UI connectée** ; les commandes, labels, listes, focus, `DataTemplate` typés et routes seront réellement testés plus tard sur Windows. Les bindings compilés se limitent à la nouvelle vue : https://docs.avaloniaui.net/docs/xaml/compilation.

## Invariants de la fondation

1. `PrepareSelectedFolder` ne lance aucun scan : seul le futur service évalue le chemin *après choix*, sans énumérer la bibliothèque avant consentement. Un refus conserve la sélection.
2. `StartConfirmedAsync` envoie `InventoryRunRequest(version, ExplicitUserConsent:true)` suite à une **action explicite** de la View ; le service, pas le VM, fait le contrôle de périmètre et de fixtures. Double départ verrouillé par états du service.
3. `CancelInventoryCommand` ne fait que demander l'annulation et reste occupé tant que la réponse du service n'a pas attesté `Partial/CANCELLED`. `Current` et `LastAttempt` viennent *directement* du service et ne sont pas remplacés par le ViewModel.
4. `ObservedFileCount` vient du snapshot consulté, `TotalFileCount=null` et `ConfirmedGameCount=null` affichent **Inconnu**, pas 0. `CoverageProof` et diagnostics sont exposés sans inférence ES-DE. MYSTERY n'expose jamais un titre d'oracle.
5. Les notifications issues d'un worker passent par l'action `postToUi` injectée. Un événement d'une ancienne `SelectionVersion` est rejeté ; toute notification est ignorée après `Dispose`. Le service reste propriétaire de sa durée de vie.
6. Aucune progression chiffrée : `IsProgressIndeterminate` est contrôlé par l'état applicatif, pas un faux pourcentage.

## Recette et intégration différées

**Tests de fondation** : `InventoryViewModelTests` utilise un **fake en mémoire**, sans `Window`, Avalonia, dossier, ROMS ni I/O. Il teste sélection sans scan, consentement, double clic, notification `CanExecuteChanged`, annulation demandée → résultat `Partial`, conservation `Current`, `LastAttempt` inspectable, 32 observations MYSTERY sans jeu confirmé, événement obsolète, erreur neutralisée, `null` ≠ 0 et désabonnement à Dispose. Cette suite **ne valide pas** la logique du vrai coordinateur DEV 1.

**Après intégration contrôlée** #76 → #75 → #77 (CI `main` après chaque fusion) + service #83 validé + GO d'implémentation : remplacer le seam temporaire par le contrat Core unique ; intégrer la View et son picker distinct d'ES-DE, le dispatch et la garde de fermeture ; utiliser uniquement les fixtures synthétiques DEMO-02 matérialisées. Recette #82 **NX03-01..11 P1**, P2 12..15 étiquetés suivant exécution, **NX03-16 BLOCKED**. Windows réel 760×480/1120×720, DPI, clavier/souris, annulation pendant playlist, captures et ZIP au HEAD exact. Aucune autorisation de première lecture réelle.

**HOLD permanent** jusqu'au GO spécifique : aucune bibliothèque personnelle, aucune fusion ni Release.
