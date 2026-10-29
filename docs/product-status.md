# État des fonctionnalités

## NEXT-04 / Durcissement UX et branding — PR #93 (en validation)

Le pack officiel reste byte-for-byte : barre latérale 264 DIP, logo PNG natif 1960×576 présenté au ratio 228×67, ICO officiel 7 tailles pour fenêtre et exécutable. Mise en page responsive et contenu limité à 1580 DIP ; Dashboard à compteurs qualifiés, Collection en cartes, fiche adaptable et diagnostics consultables dans Bibliothèques. Parcours d'import racine/confirmation/lecture seule, état d'attente indéterminé et messages neutres. Importeur, session ES-DE et protections NEXT-03 inchangés.

Comparaison avec dix références de conception **sur structure et exigences documentées seulement** : binaires privés non visualisables via l'accès courant, illustrations commerciales non redistribuées. CI Windows prévoit 13 captures dont deux **simulations** de DPI 125/200 %, source PNG/ICO contrôlée dans le ZIP normal. Aucune recette humaine DPI/clavier/ultralarge ni usage personnel revendiqué. Voir `docs/ux/next04-hardening-ux-branding.md`. **CI du HEAD exact et revue indépendante en attente ; aucune Release ni fusion autonome.**


## NEXT-04 / ESDE-REAL-IMPORT-01 — amélioration ciblée de l'import ES-DE (PR Draft)

Le parseur existant reconnaît désormais, depuis la racine explicitement sélectionnée contenant `ROMS/`, trois conventions fixes : `ROMS/<système>/gamelist.xml`, `MEDIAS/<système>/metadata/es-de-gamelist.xml` et `ES-DE/gamelists/<système>/gamelist.xml`. Il ne découvre pas les gamelists en dehors de cette racine et refuse les sources concurrentes. Pour la convention ES-DE, les médias **uniquement référencés dans le XML** sont confinés à `ES-DE/downloaded_media/`, jamais à `ES-DE/settings/` ni à une recherche par nom de ROM ; aucun média supplémentaire n'est inféré. Les erreurs de lecture XML sont désormais distinguées des structures XML incompatibles ; aucun diagnostic n'expose de chemin local complet. Genre, développeur et date de sortie sont projetés **uniquement depuis les champs XML déclarés**, sinon non renseignés. Les valeurs invalides facultatives sont ignorées avec diagnostic sans perdre le jeu. Pas de persistance ni d'enrichissement réseau. Import/reimport restent volontaires et en lecture seule, sans modification des sources. La PR backend #92 a été fusionnée dans `main` par le Contrôleur après revue contractuelle et CI ; la compatibilité de la ROMothèque personnelle reste non vérifiée, et la revue de l'interface #93 reste distincte.

## NEXT-04 / DEV 3 — premier lot interface ES-DE préparée (PR Draft)

Travail UI isolé : systèmes uniquement dérivés des jeux effectivement référencés dans les gamelists, sélection de système, recherche et pages 50 après filtre, retour de fiche avec contexte (dont défilement) et reimport conservant la vue en cours. Amélioration responsive de Collection et qualité de redimensionnement du logo officiel, sans changer les images du branding. Confirmation de l'import en lecture seule, sans déclenchement de NEXT-03 ni écriture dans les ROMS/MEDIAS.

La branche DEV 3 #93 **inclut pour test combiné** les blobs exacts du parseur ES-DE #92 et projette Genre, Développeur et Date ES-DE dans la fiche (valeur absente : « Non renseigné »). La PR backend #92 est fusionnée dans `main` et #93 a été synchronisée sur ce `main` sans écraser les modifications UX ni les tests backend. La Collection conserve filtre, recherche, page et offset via la navigation et le réimport ; une recette de 155 entrées synthétiques vérifie le défilement profond. Bibliothèques donne accès à l'aperçu NEXT-03 **SIMULATION seulement**. Aucun stockage persistant ou scan personnel. La recette automatisée Windows du même arbre source a validé 147 tests .NET, 11 captures GUI Avalonia, filtre/page 2/scroll 490, fiche avec/sans métadonnées, réimport et dernier catalogue valide. La CI du HEAD synchronisé exact et la revue indépendante sont à confirmer ; essais humains souris, clavier, DPI et écran physique ultralarge **NOT_EXECUTED**, aucune livraison Beta.

## UX V2 / PR #78 — index documentaire seulement (29 octobre 2026)

La liste CI autorise exclusivement `docs/design/mockups/v2/README.md` pour référencer les dix maquettes. Un canari accepte cet index et refuse les images, archives, manifestes et chemins voisins non autorisés. Les originaux restent à accès restreint ; aucun visuel tiers n'est ajouté au Produit. Les protections, canaris historiques et workflows sont conservés. Correction en revue, CI du HEAD à vérifier dans la PR ; aucune fusion, Release ou modification Avalonia.

## NEXT-03 — REAL GUI WIRING synthétique isolé (PR Draft #86)

Fenêtre Avalonia raccordable par injection au véritable service applicatif #85, sans nouveau moteur ; sélection et exécution réelle uniquement dans une compilation CI dédiée `NEXT03_CI` autorisant les trois fixtures fraîchement générées IDEAL/CHAOS/MYSTERY. Le mode SIMULATION public reste séparé et le ZIP standard est recompilé sans permission de scan. Windows, hash ZIP et captures exigés du HEAD CI exact. Aucune ROMothèque réelle ni fusion/Release. [Détails et limites](ux/next03-mvvm-implementation.md).


## NEXT-03 / PR #86 — préparation commune, main inchangé (29 octobre 2026)

La branche de #86 réunit les dépendances #76/#75/#77/#85 et sa fondation #84 déjà ancêtre, sans fusion de PR. Le seam provisoire est supprimé ; interface et records applicatifs uniques proviennent de #85, dans l'assembly V2 existante. Le sélecteur IDEAL/CHAOS/MYSTERY de #77 et l'entrée MVVM SIMULATION sont conservés ensemble. Un contrôle de raccordement MSTest exerce le vrai ViewModel avec le vrai service sur les seules fixtures générées, en plus des tests historiques. CI du HEAD à vérifier dans la PR ; aucune réception fonctionnelle anticipée.

La fenêtre demeure une simulation sans inventaire de dossier ; le constructeur public du vrai service reste fermé. La recette Windows commune est préparée dans `docs/ux/next03-mvvm-implementation.md`, sans exécution humaine, nouveau harness GUI, scan personnel, modification de l'architecture ou Release. Toutes les PR restent MERGE_HOLD.

## NEXT-03 — MVVM consolidation DEV 3 (Draft #86, non intégrée)

Le ViewModel distingue les notifications hors ordre et les générations, révoque la confirmation après un essai de changement de source, bloque les nouveaux dossiers pendant l'annulation, et affiche des générations `Current`/`LastAttempt` distinctes, y compris lorsque le dernier essai est Failed. Les listes vides et les totaux indisponibles restent **Inconnu**. Les bindings Avalonia montrent les provenances, diagnostics et statuts ROMS fictifs. **Toujours une simulation en mémoire sans accès à ROM ni service DEV 1 raccordé**. Recette Windows physique et P1 #82 non exécutées ; [détails et critères](ux/next03-mvvm-implementation.md). Aucun merge, Release ou accès personnel.


## NEXT-03 / Avalonia MVVM — aperçu simulé (PR Draft, non livré)

Fenêtre MVVM accessible dans Bibliothèques, sélecteur de chemin sans lecture de dossier, confirmation distincte et simulation en mémoire des états, couverture, observations et diagnostics. Aucun appel à InventorySession ni bibliothèque personnelle ; les données affichées sont marquées SIMULATION. Code source historique inchangé hors ajout du bouton dans Bibliothèques ; import ES-DE indépendant. Voir [recette de prévisualisation](ux/next03-mvvm-implementation.md). Attente de #75/#76/#77 et du vrai service DEV 1 #83, sans GO de fusion ni Release.


## NEXT-03 / MVVM FOUNDATION — DEV 3 (branche Draft, non intégrée)

Premiers composants **indépendants de l'interface graphique dans leur code source** : `InventoryViewModel` (notifications/commandes/provenances), `InventoryApplicationSeam` **provisoire conforme à la proposition DEV 1 #83**, tests unitaires en mémoire. **Aucun service de scan implémenté ni raccordement Avalonia/ROM** ; aucun changement de Dashboard, Collection, fiches, Bibliothèques ou import ES-DE. Dépendance `CommunityToolkit.Mvvm 8.4.2` évaluée, **non ajoutée** en attente de NuGet lock/avis licences/CI ; BCL `INotifyPropertyChanged`/`ICommand` utilisée pour la fondation. [ADR et limites](ux/next03-mvvm-foundation.md). #75/#76/#77 restent à intégrer avant toute connexion au service et au moteur. Aucun scan réel, merge ou Release.

## LIBRARY-UX-02 — parcours de bibliothèques (PR Draft #77, 28.10.2026)

- Nouvelle expérience Avalonia Darcula Retro **uniquement de présentation** : choix IDEAL / CHAOS / MYSTERY, Dashboard → Collection → Fiche, recherche dans la sélection et retour. Aucune lecture de ROM, collecte réseau, modification physique ou nouveau moteur.
- Prévisualisations IDEAL/CHAOS basées sur les contrats canoniques **fictifs** intégrés à l'assembly ; tous les chiffres et médias affichés sont des attentes de scénario, non des constatations de fichier. MYSTERY **ne révèle ni titre, ni identité, ni total de jeux confirmé** malgré l'oracle synthétique.
- Fiche importée ES-DE : affichage local en lecture seule des jaquettes, miniatures et logos réellement référencés (avec repli manquant/illisible/inconnu). Captures/dos/fonds hors contrat d'import restent sans aperçu inventé. Pagination désactivée si une seule page.
- Capture réelle de cinq vues Avalonia sur runner Windows et ZIP temporaire autonome vérifié par SHA, sans Release, dans un workflow strictement limité à la branche DEV 3. Recette Windows et GO propriétaire **restent obligatoires** avant merge. [Procédure LIBRARY-UX-02](ux/library-ux-02.md).

## NEXT-03 — backend applicatif seul (PR Draft #85, pas de parcours UI raccordé)

Le service `RomothequeManager.V2.Inventory.Application.IInventoryApplicationService` est implémenté dans **le projet Avalonia existant** mais **ne dépend d'aucune API Avalonia ou MVVM**. Il orchestre exclusivement `InventorySession.Scan(CancellationToken)`, avec sélection locale explicitement consentie, tâche asynchrone, annulation, single-flight, dernière génération complète `Current`, dernière tentative `LastAttempt` et projections d'état sans inférence. `InventorySnapshot` couvre uniquement les fichiers `ROMS/`, **pas les jeux confirmés ni les médias ES-DE**.

Le constructeur public **refuse tout scan réel** ; seule l'assembly MSTest du dépôt peut autoriser ses propres racines synthétiques. L'écran Avalonia et le ViewModel NEXT-03 sont **non raccordés**, les PR #75/#76/#77 restent indépendantes, `NX03-16` scan réel HOLD. [Contrat API et tests](architecture/next03-backend-service.md). Aucune fusion ni Release.

## DEMO-LIBRARY-01 — catalogue canonique candidat (PR Draft #72, sans effet produit)

La [PR #72](https://github.com/TobbyCy/RomothequeManager/pull/72) documente **30 jeux entièrement fictifs, sur les 13 plateformes corrigées par le propriétaire** ([Issue #71](https://github.com/TobbyCy/RomothequeManager/issues/71), notamment Xbox, PS2, PS3, PS4, PC et DOS) ; catalogue de référence `contracts/demo-library-01/`, contrat et briefs sous `docs/architecture/`, variantes IDEAL/CHAOS/MYSTERY et oracles non exécutés. DEV 1 aligne son générateur [#73](https://github.com/TobbyCy/RomothequeManager/pull/73) sur cette source. **Aucune ROM, PNG, XML, bibliothèque générée ou fonctionnalité UI livrée par #72 ; aucun changement à l'intégration NEXT-01.**

## NEXT-01 / PR #64 — vérification du HEAD courant et du fonctionnement hors ligne

- Workflow de recette : checkout strict du `github.event.pull_request.head.sha` au moment du run, vérifié avant restauration SDK et avant packaging. **Aucun ancien SHA `e775cdd6` épinglé** ; chaque artifact contient le manifeste du HEAD exact et le SHA-256 du ZIP `win-x64` autonome produit pour cette révision, sans Release.
- Windows CI : sur le véritable EXE extrait, vérification du sous-système PE GUI = 2, lancement `--smoke-window` avec runtime système volontairement indisponible, lancement ShellExecute (équivalent technique de la voie d'ouverture Windows ; **ce n'est pas un double-clic humain dans l'Explorateur**).
- Recette hors ligne : règle Windows Firewall temporaire bloquant le trafic **sortant de l'EXE exact** (pas de coupure du réseau du runner), vérification de la règle puis démarrage Avalonia. Contrôle des quatre jeux fictifs ES-DE, des médias locaux, de la navigation, des libellés français affichés, et du réimport de XML malformé avec conservation du catalogue, puis restauration de la fixture. La règle est retirée même en cas d'échec. Aucun réseau nécessaire à la logique métier testée.
- Limites à faire valider par Tobby : véritable double-clic Explorer, absence de console perceptible, gestes souris/clavier, rendu des diagnostics et médias, puis essai **sur son PC réellement déconnecté d'Internet** ; les essais CI ne démontrent pas l'état du réseau d'un poste externe.
- **Gates** : résultats exacts du workflow et CI avant verdict ; aucune fusion ni Release sans GO propriétaire. Aucun code NEXT-02 modifié.


## État public actuel — 28 octobre 2026

La [première Pre-release Windows V2-01C a été publiée le 23.10.2026](https://github.com/TobbyCy/RomothequeManager/releases/tag/poc-v2-01c-20261023-4cf475fbd1ec) depuis `main=4cf475fbd1ece16b138403d3bd967a56d295ee54`. Le [ZIP Windows x64 autonome](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip) (243 fichiers ; runtime .NET inclus) et son [fichier de contrôle SHA-256](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip.sha256) sont **téléchargeables**. Son empreinte de référence est `22328d682b7c93edcdeeb735c9913880e68dec64c54b159bd6e6de77bcc7eaaf`.

**Démarrage :** télécharger ZIP et `.sha256` ; lancer `Get-FileHash -Algorithm SHA256 .\RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip` dans PowerShell ; comparer l'empreinte ; **extraire** le ZIP dans un dossier utilisateur ; lancer `RomothequeManager.V2.exe` depuis ce dossier, sans installateur, SDK ou runtime .NET externe. L'application n'est pas signée (avertissement SmartScreen possible).

**Portée attestée :** POC **strictement synthétique** sous .NET 10 / Avalonia 12.1.4 ; quatre vues (Dashboard, Collection, Fiche jeu, Bibliothèques), **six jeux fictifs et trois bibliothèques fictives**, lecture seule. Le [run de publication contrôlée](https://github.com/TobbyCy/RomothequeManager/actions/runs/40004889570) et l'[Issue #50](https://github.com/TobbyCy/RomothequeManager/issues/50) documentent les preuves du paquet. La [réception utilisateur initiale](https://github.com/TobbyCy/RomothequeManager/issues/50#issuecomment-6381903496) confirme le lancement et l'utilisation générale sur le PC de Tobby ; elle **n'atteste pas** les douze contrôles UX détaillés.

**Distinction des versions :** cette Pre-release a été construite depuis le commit historique `4cf475fb…`, **avant les intégrations NEXT-01-A (#62), NEXT-02-CONTRACT (#66) et NEXT-02 moteur (#68)**. Le `main` actuel possède ces sources et leurs tests synthétiques, **mais le moteur d'inventaire NEXT-02 n'est pas accessible depuis l'interface Windows et ne fait pas partie de ce ZIP publié**.

**Hors périmètre actuel :** aucune Release stable ni installateur signé, pas de scan réel, d'import ES-DE dans l'interface, de persistance de collections, de scraper, de synchronisation ou d'export/copie physique. La compatibilité SteamOS/USB et une recette Windows externe exhaustive restent à qualifier. **La réussite du POC n'autorise aucun traitement de ROM personnelles.**

## NEXT-01-UX — PR #64 en revue, hors `main` (28 octobre 2026)

Les sections de NEXT-01 ci-dessous décrivent **une PR non fusionnée**, soumise à la recette humaine et au GO propriétaire. Elles n'annulent pas la Pre-release V2-01C historique décrite plus haut. La synchronisation de la branche de #64 conserve l'inventaire NEXT-02 et les mises à jour DOC-P1 de `main` ; la preuve ZIP applicative demeure figée sur le source corrigé `e775cdd6`.

### Preuve binaire du ZIP corrigé (source `e775cdd6`)

- [Workflow Windows #40689889760 — SUCCESS](https://github.com/TobbyCy/RomothequeManager/actions/runs/40689889760), artifact temporaire [#12816457518](https://github.com/TobbyCy/RomothequeManager/actions/runs/40689889760/artifacts/12816457518) (jusqu'au **31.10.2026 12:43 UTC**).
- Source exacte de compilation (figée, malgré les commits documentaires du workflow) : `e775cdd64522d45ee2fb848df232fa1f19023326`, vérifiée par `UX_RECIPE_FROZEN_SOURCE` et `UX-MANIFEST.json`. ZIP applicatif `RomothequeManager-UX-PR64-win-x64-e775cdd6.zip` : **75 362 552 octets**, **250 fichiers**, SHA-256 **`fe6a4d59adfae5ea171c229838d17fdeef4bd23a41e03924d2e5a93f6c407364`**. Le fichier annexe `.zip.sha256` concorde ; le hash a été recalculé indépendamment après téléchargement de l'artifact Actions (dont le digest externe est un autre fichier).
- 62/62 tests .NET PASS, licences/runtime inclus et contrôlés, ZIP extrait/recomparé et smoke de la fenêtre Avalonia PASS. `UX_RECIPE_WINEXE_GUI_PASS subsystem=2` vérifie le PE GUI de l'exécutable **extrait de ce ZIP** ; cela ne prouve pas un double-clic interactif dans l'Explorateur.
- La fixture fictive intégrée permet de contrôler manuellement le résumé de diagnostics en français, la ROM/le média absent et l'échec XML suivi de la conservation du dernier catalogue valide. **Recette Windows humaine toujours requise** avant GO de fusion.
- **CI adaptative** : le premier run sur la mise à jour du workflow a rencontré `CLASSIFICATION_SCOPE_UNPROVEN / MERGE_PARENTS_INVALID` en raison d'un décalage entre le SHA de base de la PR et le HEAD de `main` pendant une intégration concurrente. Il ne s'agit pas d'un PASS de la CI générale : relancer un contrôle complet sur une base cohérente, sans affaiblir le contrôle des parents.
- **Aucune fusion ni Release ;** cette preuve est un artifact de recette temporaire, jamais une nouvelle publication publique.

## NEXT-01-UX — recette du ZIP corrigé PR #64

- Source applicative de référence : `e775cdd64522d45ee2fb848df232fa1f19023326`, comprenant WinExe et diagnostics ES-DE francisés. La prochaine PR de packaging porte uniquement la modification du workflow, ses canaris et la documentation : le ZIP embarque exactement le source applicatif ci-dessus.
- L'ancien artifact figé `cc35abf7` **ne valide pas** les corrections. Le nouveau workflow conserve toutes les barrières de licence, source, SHA-256, interdiction de ROMs et timeout du smoke. Vérification ajoutée : PE subsystem Windows GUI = 2, sur le .exe du ZIP extrait.
- Recette Windows Tobby : double-clic EXE, absence de console, import de fixture fictive et messages FR ROM/média absents, XML volontairement malformé puis reimport/reprise non destructive. Résultat manuel à reporter à la PR.
- Publication ZIP temporaire sous GitHub Actions seulement : **aucune Release**, aucune bibliothèque réelle, aucun GO de merge présumé.


## VISUAL-POC-01 — retouches de lisibilité et densité (retour propriétaire 28.10.2026)

- Navigation : 4 états **actif, inactif, survolé, indisponible** explicitement contrastés, liseré d'état et vrais boutons conservant focus clavier ; seuil de contraste des textes >= 4,5:1 contrôlé par tests synthétiques.
- Dashboard : marges, vignettes, cartes et espaces verticaux réduits sans supprimer les qualificatifs de disponibilité, d'origine fictive et de lecture seule. Aucun moteur ni import modifié.
- À 760×480, la sidebar dispose de son défilement indépendant : les commandes basses ne sont plus coupées ; la page principale reste scrollable. Six captures Windows (760×480 et 1120×720 à échelles de rendu 100/125/150 %) et CI sont attendues sur le nouveau HEAD. Ces DPI **sont des échelles de rendu dans Avalonia**, pas des changements du DPI natif Windows.
- Icône PE GUI et ressource ICO contrôlées automatiquement ; **Explorer Windows, barre des tâches, focus, hover et DPI système natif** restent à vérifier manuellement sur poste Windows par Tobby. Aucun merge, Release ou scan réel.


- **Revue visuelle Windows réelle :** run #40675500153, 65 tests .NET PASS et captures 1120×720 / 760×480, DPI 100/125/150 %. Après inspection des PNG, correction de la grille large et du contraste navigation ; les contrôles Exports sont représentés comme éléments visuellement lisibles et **non interactifs**, sans fausse action.
- **CI de clôture :** le changement final doit relancer Adaptive CI sur la PR temporairement ciblée `main`, puis être rebasé logiquement sur #64 pour isoler le delta avant toute revue/merge. Référencer le run final exact sur GitHub #69. Aucun droit de distribution en Release n'est accordé.

## VISUAL-POC-01 — Dashboard Darcula Retro / [Issue #67](https://github.com/TobbyCy/RomothequeManager/issues/67) (PR Draft)

- GO propriétaire explicite 28.10.2026 : intégration dans le Produit des **5 assets officiels** (bandeau PNG préexistant + 4 SVG/ICO intacts du Pilotage), y compris logo dans l'interface, `WindowIcon` et `ApplicationIcon` Windows. Droits réservés distincts de MIT ; pas de Release sans accord séparé.
- PR dédiée **empilée sur #64** pour isoler le delta visuel. Palette Darcula Retro `#16181D` / `#B3202E` / `#AAB2BF` / `#E8E6E3`, collection prioritaire, bibliothèques/supports, Exports explicitement désactivés, diagnostics prudents ; données fictives ou ES-DE uniquement sur sélection volontaire. Aucun nouveau moteur, scan/écriture/copie.
- Contrôles d'empreinte des originaux Pilotage et garde CI exacts ; captures du rendu réel Avalonia Windows à produire et publier comme **rendu attaché**, non comme capture native de la barre des tâches. La vérification dans l'Explorateur et la taskbar Windows **nécessite toujours la recette propriétaire**. La maquette PNG historique manque pour une correspondance pixel par pixel.
- Aucune fusion, Release, ROM/BIOS ni média de tiers. Les preuves .NET / capture doivent citer le SHA et run effectifs, sans verdict présumé.


## NEXT-01-UX-FIX — correction suite à recette Windows (PR #64, 28 octobre 2026)

- L'exécutable Avalonia de la PR est configuré en `WinExe`, pour ouvrir la fenêtre sans console au double-clic sous Windows. Le mode `--smoke-window` reste le contrôle automatisé de lancement ; la validation interactive de l'affichage relève de Tobby.
- Le tableau de bord importé donne maintenant des diagnostics français explicites (catégorie à vérifier / information, plateforme, entrée et explication), triés par importance ; les refus d'import / réimport expliquent les problèmes courants (XML invalide, gamelist absente ou ambiguë, dossier inaccessible). Aucun scan supplémentaire, aucune modification de l'adaptateur de lecture seule ni conclusion de suppression.
- Les tests synthétiques supplémentaires vérifient le sens des diagnostics et la conservation du catalogue valide après échec. Validation CI à rattacher au **nouveau HEAD** avant toute décision de merge. L'artifact UX provisoire antérieur, construit sur le SHA `cc35abf7…`, **ne démontre pas ces corrections** et ne doit pas être confondu avec un binaire corrigé.


## UX-ARTIFACT-CI — distribution de recette temporaire, PR #64 uniquement (24 octobre 2026)

- Un workflow **distinct** `.github/workflows/ux-artifact.yml` est proposé **dans la PR #64 Draft uniquement**. Il exige une PR #64 du dépôt même, ouverte vers `main`, et le compte propriétaire `TobbyCy`; il ne s'exécute pas au démarrage de l'application et ne déclenche aucun scan réel.
- La compilation est **figée** au commit déjà validé `cc35abf70acc12120f7617f8e5ab0a8ea84b8801`, indépendamment du commit portant le workflow. SDK `.NET 10.0.401`, restauration NuGet verrouillée, tests, `win-x64 --self-contained`, licences/notices exactes du runtime 10.0.12, inventaire juridique et marqueur LIC-ZIP-01 requis avant toute création de téléchargement.
- Une bibliothèque ES-DE générée **exclusivement dans le runner** contient quatre jeux fictifs, des fichiers texte `.fixture` non exécutables et des mini-images PNG synthétiques, avec cas de jaquette absente/illisible et média manquant. **Aucune ROM/BIOS, aucun média tiers, aucun chemin ou contenu personnel.**
- L'archive est extraite et contrôlée fichier par fichier (SHA-256), ainsi qu'au moyen du smoke Avalonia avec runtime externe volontairement indisponible. La seule sortie distribuable autorisée est l'**artifact GitHub Actions** temporaire de 3 jours contenant ZIP + `.sha256`; **aucun workflow Release, aucun tag, aucune fusion automatique**. Les canaris et la liste blanche CI sont étendus à ce chemin exact, tandis que l'interdiction d'upload du workflow principal `ci.yml` est inchangée.
- Statut courant : **workflow proposé sur la branche, CI et artifact à vérifier avant tout lien de téléchargement**. Un ZIP ne doit pas être annoncé livré sans un run SUCCESS et son artifact disponible. La recette UX clavier/focus/défilement/redimensionnement reste `NOT_TESTED` sans essai humain sous Windows.


## NEXT-01-UX — candidate Avalonia sur bibliothèque ES-DE (PR #64 en revue, non fusionnée)

- La [PR #64](https://github.com/TobbyCy/RomothequeManager/pull/64), dépendante du contrat de lecture seule [PR #62](https://github.com/TobbyCy/RomothequeManager/pull/62) désormais fusionné, propose **sur une branche non distribuée** deux modes clairement distincts : POC fictif et bibliothèque ES-DE importée sur sélection explicite du dossier racine.
- Dashboard, Collection, fiche et Bibliothèques consomment le même catalogue importé ; recherche et pagination de 50 jeux, identités distinctes pour les titres homonymes, jaquette locale avec tentative de miniature de repli, diagnostics et refus de réimport non destructif. Les comparaisons A/B restent **fictives et indisponibles en mode importé**.
- Les tests du lot sont **exclusivement synthétiques** (y compris une fixture de 2 000 entrées), sans scan de collection privée, ROM réelle, scraper, export ou modification des sources. La CI .NET et les captures interactives Windows doivent être qualifiées sur le HEAD exact de la PR avant toute approbation ; **aucune fonctionnalité NEXT-01-UX n'est intégrée sur `main` ni distribuée par cette PR**.
- Cette note de branche sert à la traçabilité du changement de code ; l'actualisation transversale des autres états historiques et de la Pre-release relève séparément du chantier documentaire [PR #63](https://github.com/TobbyCy/RomothequeManager/pull/63).

## Historique de préparation (états au 23 octobre 2026, avant publication)

Les paragraphes DOC-GOV-01 / LIC-ZIP-01 / DIST-ZIP-01 suivants conservent **leurs statuts à la date indiquée**. Les mentions `RELEASE_HOLD`, « non fusionnée », « aucun ZIP téléchargeable » et `PENDING` ci-dessous concernent ces **étapes antérieures** ; elles ne décrivent pas la Pre-release désormais publiée.

> **Référence historique DOC-GOV-01 — 23 octobre 2026 :** dépôt Produit **PUBLIC**, `main=408d6263be6ae1fbc77c31f6eb9fb530cb4f76e6` (avant V2-01B et BRAND-01). Les [PR #42](https://github.com/TobbyCy/RomothequeManager/pull/42) (POC Windows V2-01A) et [#45](https://github.com/TobbyCy/RomothequeManager/pull/45) (CI-DOTNET-01) sont **fusionnées**. La [CI `push main` #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607) est **COMPLETED / SUCCESS** sur ce SHA, y compris build/tests .NET, smoke de fenêtre Avalonia et `ci-policy-gate`. Cette preuve est propre à ce commit ; elle n'atteste pas une Release ni un usage réel.

Le terme **fonctionnel** désigne ici un POC exécuté et testé **sur fixtures fictives**, pas un produit prêt pour des bibliothèques personnelles. Le code intégré, la validation CI et la disponibilité d'un binaire distribué sont trois états distincts.

## V2-01C — packaging Windows candidat (historique : phase sans distribution)

La première phase de l'[Issue #50](https://github.com/TobbyCy/RomothequeManager/issues/50) autorisait **uniquement la préparation technique** d'un ZIP autonome `win-x64` et de son fichier SHA-256. Le workflow construit le paquet avec le SDK exact et les dépendances verrouillées, inscrit un manifeste `POC-MANIFEST.json` (`syntheticOnly=true`, quatre vues, six jeux et trois bibliothèques fictives), vérifie le ZIP puis le réextrait dans un autre dossier et lance l'exécutable depuis cette extraction. Le résultat reste dans `RUNNER_TEMP`, **sans upload, GitHub Pre-release ou Release**.

**LIC-ZIP-01 — inventaire des notices tierces (traçabilité : PR [#56](https://github.com/TobbyCy/RomothequeManager/pull/56)) :** inventaire des 25 paquets NuGet verrouillés, textes éditeurs et index `THIRD-PARTY-NOTICES.txt` proposés. La politique CI est ajustée uniquement pour accepter les fichiers de notices `.txt` sous `packaging/`. Le **publish candidat de DEV 1** est maintenant inventorié (223 fichiers, [.NET CI #174](https://github.com/TobbyCy/RomothequeManager/actions/runs/39958695887), runtime observé 10.0.12), mais le **ZIP final**, les documents de licence du runtime pack Windows exact et les avis natifs transitifs restent à vérifier avec DEV 1 ; **`thirdPartyNotices=PENDING` et `RELEASE_HOLD` demeurent**.

Cette CI n'atteste pas encore un démarrage sur un Windows x64 propre sans runtime/SDK, ni les parcours interactifs clavier, focus, défilement, ComboBox et redimensionnement de [UXZIP-01 à UXZIP-12](https://github.com/TobbyCy/RomothequeManager/issues/50). Les licences/notices tierces et la recette externe restent à valider. Le [guide packaging](developer/v2-01c-packaging.md) précise la vérification et le blocage de publication.

## DIST-ZIP-01 — préparation de distribution (historique : avant publication)

Après fusion de la [PR #53](https://github.com/TobbyCy/RomothequeManager/pull/53) sur `main=31579350033c22cd3c55e156d29a4da7f18ad822`, Tobby a donné **GO_PREPARE_TEST_DISTRIBUTION**, pas le GO final d'upload. Une voie manuelle `workflow_dispatch` est préparée : `validate` (ZIP et SHA-256 éphémères, contrôlés depuis extraction) puis `publish` (pré-release de test uniquement après hash/commit/tag et variables d'approbation exacts). Aucune exécution PR/push n'autorise un upload.

**Correctif P1 proposé — [PR #57](https://github.com/TobbyCy/RomothequeManager/pull/57) (non fusionnée) :** le chemin `publish` épingle et relit explicitement le tag Git distant sur le SHA approuvé **avant toute création de Draft Release et tout upload** ; une Draft peut ne pas créer elle-même ce tag. Les réponses API autres que 201, conflits de création et écarts de SHA bloquent l'opération ; `gh release create --verify-tag` interdit la création implicite. Le contrôle du tag reste répété avant la publication finale. Un échec après création du ref peut laisser un tag orphelin, à traiter manuellement, sans nettoyage automatisé. **Aucune publication autorisée par cette correction seule.**

**Séparation des gates :** `validate` peut construire et contrôler le candidat complet avec les notices source présentes, puis journaliser manifeste exact et SHA-256 **sans approbation juridique préalable**. Si le marqueur DEV 2 manque, l'état reste explicitement `LEGAL_HOLD` et aucun upload n'est effectué. **`publish` échoue avant tout upload** sans marqueur juridique, quatre variables d'approbation propriétaire et SHA source/ZIP/tag concordants. Les notices source manquantes restent bloquantes. La PR #56 est maintenant intégrée, mais la conformité du ZIP final exact reste à vérifier. Revue Agent, validation juridique, approbation finale et UXZIP-01–12 restent à faire ; **aucun ZIP n'est actuellement téléchargeable**. Voir [guide packaging et distribution](developer/v2-01c-packaging.md).

**P1 fin de ligne du marqueur juridique — [PR #60](https://github.com/TobbyCy/RomothequeManager/pull/60) (Draft, non fusionnée) :** après intégration de l'approbation LIC-ZIP-01, le [validate #39996678479](https://github.com/TobbyCy/RomothequeManager/actions/runs/39996678479) a encore signalé `LEGAL_HOLD` car la détection de la ligne isolée ne tolère pas le CRLF du checkout Windows. Correctif ciblé : `\r?` avant la fin de ligne, sans modifier la sensibilité à la casse, les autres contrôles juridiques ou les gates propriétaire/SHA/tag de `publish`. Les dix canaris du contrôle statique Windows (CI-0) couvrent LF, CRLF, l'inventaire réel et les cas de refus, sans modifier la couverture historique Pester. **Nouvelle validation `phase=validate` sur le futur `main` requise après fusion par le Contrôleur ; aucun upload ni Release autorisé par cette PR.**

## V2-01B intégré — Bibliothèques et snapshots

La [PR #48](https://github.com/TobbyCy/RomothequeManager/pull/48), issue du mandat [#47](https://github.com/TobbyCy/RomothequeManager/issues/47), a été **fusionnée** sur `main=eda249cd95327cb4bbba27350898513070eca62b`. Elle ajoute la quatrième vue Avalonia « Bibliothèques & snapshots » : **trois bibliothèques liées aux six jeux fictifs existants**, liste/inspecteur, snapshots datés, états de disponibilité et de fraîcheur, et sélection A/B sur résultats préqualifiés. Les sept scénarios PowerShell historiques restent des vecteurs de test indépendants, non additionnés au Dashboard.

**Intégré puis distribué dans la Pre-release synthétique :** le POC reste limité aux fixtures synthétiques. Aucun scan, collection réelle, moteur de comparaison C#, PowerShell lancé par l'UI, stockage ou export n'est livré. Les compteurs inconnus restent `null`, les absences observées ne prouvent aucune suppression et le dernier complet vérifié reste distinct des tentatives partielles.

### Traçabilité de la fixture Windows V2-01B (POC synthétique intégré)

**`UI-only / handcrafted deterministic demo / NOT_ENGINE_VERIFIED`.** Le JSON embarqué `src/RomothequeManager.V2/Fixtures/library-ui-v2-01b.synthetic.json` est une démonstration fictive **figée et révisable**, non une sortie vérifiée du producteur PowerShell. Les six `syn-001…syn-006` restent répartis en trois `libraryId` (`lib-a/b/c`) ; les sept vecteurs PS demeurent séparés et ne constituent pas sept bibliothèques à afficher. Aucun résultat de classification de cette fixture ne doit être attribué au moteur PS5.1.

Les **oracles indépendants** des champs `counts`, `entries` et `countDenominator` sont codés en dur dans `ThreeFrozenUiOraclesAreIndependentOfFixtureCounts` (MSTest), puis confrontés au JSON :

| Bibliothèque | A → B synthétiques | Couverture | A∪B | Résultats attendus |
| --- | --- | --- | ---: | --- |
| `lib-a` | `a-complete-20261019` → `fictif-2026-10-20` | INCOMPLETE | 2 | UNKNOWN 2 |
| `lib-b` | `b-complete-20261017` → `fictif-2026-10-18` | COMPLETE | 2 | UNCHANGED 1, NOT_COMPARABLE 1 (conflit local d'identité) |
| `lib-c` | `c-complete-20261021` → `fictif-2026-10-22` | COMPLETE | 4 | ADDED 1, CHANGED 1, UNCHANGED 1, NOT_OBSERVED_IN_B_COVERED_SCOPE 1 |

Ces dénominateurs sont des **unions d'unités logiques observées**, jamais des nombres de fichiers physiques supprimés. Les tests de qualification imposent la couverture exacte des partitions `console × sourceKey × rootKey × domain`, vérifient les références A/B et leurs scopes et rejettent les preuves absentes, étrangères ou dupliquées. Le rendu masque l'historique détaillé lorsque `indexAccessible=false` : seule une référence mémorisée explicitement non consultable peut être montrée.

**Limite UX volontaire du POC :** `INTERACTIVE_KEYBOARD_SCROLL_NOT_TESTED`. Le smoke CI Windows atteste l'ouverture des quatre vues et l'exercice de l'état A/B, **pas** un parcours physique clavier/ComboBox, le maintien du focus ou du scroll, ni la vérification humaine à largeur réduite. À suivre séparément après intégration si le Contrôleur le décide.

## Capacités réellement intégrées

| Domaine | État intégré et preuves | Limites actuelles |
| --- | --- | --- |
| Moteur Windows PowerShell 5.1 / inventaire E1 | Sources et contrôles synthétiques présents ; CI-0 à CI-4 et Pester applicables **SUCCESS** dans le [run #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607) | Aucun scan ni modification de collection réelle autorisé |
| Prototype web `prototype/ui11/` | Dashboard, Bibliothèques, comparaison A/B, Collection et fiches sur données fictives ; smoke Node et Edge headless `file://` **SUCCESS** dans ce run | Pas de persistance, ni de connexion à une collection réelle ; Edge headless n'est pas une recette graphique humaine |
| Bibliothèques / snapshots / catalogue PS → JSON → JS | Producteur PowerShell, projections, comparateur à six classes et consommateur JS intégrés ; canaris et fixtures synthétiques exécutés en CI | DTO 0.2 encore candidat ; l'état OFFLINE/STALE ou un index inaccessible ne prouve jamais une suppression |
| **POC Windows V2-01A/B** | `src/RomothequeManager.V2/` intégré via **PR #42 et #48** ; .NET 10, Avalonia 12.1.4, **quatre vues** Dashboard / Collection-recherche / Fiche / Bibliothèques-snapshots avec A/B, **six jeux fictifs et trois bibliothèques fictives** ; **31 MSTest** et smoke réel de fenêtre Windows à quatre vues dans la CI de #48 | Lecture seule sur fixtures UI, résultats A/B préqualifiés et non vérifiés par le moteur ; pas de scan, stockage, réseau métier, export, import de ROM ou essai utilisateur sur PC externe |
| SDK et restauration .NET | `global.json` impose **SDK 10.0.401 exact**, `rollForward=disable` ; locks NuGet committés, restore verrouillé ; [PR #45](https://github.com/TobbyCy/RomothequeManager/pull/45) ajoute `actions/setup-dotnet` épinglé avant les contrôles | Le run post-fusion prouve le succès du provisionnement sur son runner, **pas encore** le scénario reproductible d'une image initialement dépourvue de 10.0.401 (suivi [#43](https://github.com/TobbyCy/RomothequeManager/issues/43)) |
| **NEXT-01-A / NEXT-02 dans `main` (hors Pre-release 23.10)** | [#62](https://github.com/TobbyCy/RomothequeManager/pull/62), [#66](https://github.com/TobbyCy/RomothequeManager/pull/66) et [#68](https://github.com/TobbyCy/RomothequeManager/pull/68) fusionnées ; import ES-DE facultatif et inventaire indépendant en lecture seule, [CI `push main` #40691495453](https://github.com/TobbyCy/RomothequeManager/actions/runs/40691495453) SUCCESS | **Moteur non branché à l'interface Avalonia, absent du ZIP de Pre-release historique ; seulement tests fictifs, aucun scan réel** |
| ZIP Windows / Release | [Pre-release V2-01C publiée](https://github.com/TobbyCy/RomothequeManager/releases/tag/poc-v2-01c-20261023-4cf475fbd1ec) : [ZIP `win-x64` autonome](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip) et [`.sha256`](https://github.com/TobbyCy/RomothequeManager/releases/download/poc-v2-01c-20261023-4cf475fbd1ec/RomothequeManager-V2-01C-win-x64-4cf475fbd1ec.zip.sha256), 243 fichiers, SHA-256 `22328d682b7c93edcdeeb735c9913880e68dec64c54b159bd6e6de77bcc7eaaf` ; contrôles d'extraction, manifeste, notices et smoke des quatre vues en CI | **Version de test synthétique**, pas d'installateur/Release stable ; recette clavier/focus/scroll détaillée et collections réelles non validées |
| Licence et visuels | Licence MIT pour le code original et la documentation autorisée ; `LEGAL.md` et `BRANDING.md` publics ; bandeau PNG officiel V2 ajouté dans BRAND-01 avec autorisation propriétaire | Le bandeau reste sous droits graphiques réservés, distincts de MIT ; autres SVG/ICO non distribués ; aucun droit sur ROM/BIOS/jeux tiers |

## Travail futur ou non livré

- **Raccordement des bibliothèques réelles à l'interface Windows :** non implémenté dans V2-01B ; la vue intégrée utilise exclusivement des fixtures fictives et aucun moteur de comparaison métier C#.
- **Bibliothèques personnelles, favoris, tags, persistance et synchronisation :** non implémentés dans le POC Windows.
- **Export E2 à E6, conversion, copie et opérations physiques :** conception uniquement ; aucune action réelle autorisée.
- **Distribution et compatibilité :** la première **Pre-release de test Windows x64 est publiée** ; portabilité USB, Windows hors poste initial, recette UX détaillée, SteamOS/Linux/ARM et version stable restent à valider. La CI seule ne vaut pas GO pour une nouvelle Release.

La [roadmap](roadmap.md) décrit les intentions. Le [CHANGELOG](../CHANGELOG.md) distingue les intégrations des anciens états de sprint. Les sections suivantes conservent les notes historiques **à leur date de rédaction** ; elles ne remplacent pas le tableau courant.

## Historique de construction du prototype

Les paragraphes ci-dessous conservent la trace des anciens sprints au moment de leur conception ; leurs libellés de validation n'attestent pas les tests du `main` publié.

**Sprint #13 (historique de la PR #28, désormais fusionnée)** : ajout d'un adaptateur de DTO `0.1.0-synthetic` pour le registre et les comparaisons précalculées de #27. Deux modes explicitement séparés, sans moteur métier JS en mode projection. Aucune preuve de parité runtime PS5.1 ni navigateur réel.


**Sprint #15 (historique de la PR #28, désormais fusionnée)** : Collection projetée et fiche de jeu synthétique candidate, filtre plateforme, provenance, variantes et observations ROM séparées, navigation vers snapshots. Adaptateur aligné sur `countKnown=false/observedUnitCount=null` de #27. Contrôles V8 simulés exécutés ; Node/navigateur/PS5.1 non exécutés. Le catalogue a ensuite été intégré via la PR #29 ; le DTO 0.2 reste un contrat candidat, sans adoption finale.

## NEXT-01-A — adaptateur ES-DE intégré au code source

La [PR #62](https://github.com/TobbyCy/RomothequeManager/pull/62), désormais fusionnée, intègre un importateur .NET **en lecture seule** des `gamelist.xml` ES-DE depuis une racine explicitement choisie : conventions `ROMS/<plateforme>/gamelist.xml` ou `MEDIAS/<plateforme>/metadata/es-de-gamelist.xml`, identité stable relative, jeux homonymes distincts, unités `.m3u`, références de médias et diagnostics de disponibilité. Le chargement est entièrement préparé avant substitution en mémoire ; un second import échoué conserve le dernier instantané valide. Les chemins hors racines autorisées, liens symboliques, DTD et sources ambiguës sont refusés. Seules des fixtures synthétiques sont exécutées en CI ; **ni UI réelle, ni scanner complet, ni accès au SSD utilisateur, ni Release**. Le correctif P1 de revue ajoute une identité de bibliothèque **stable dans la session** malgré l'ajout/retrait de jeux, une relocalisation explicite vérifiée par empreinte du contenu et `TryOpenMedia` qui réapplique confinement et contrôle des reparse points lors de l'ouverture. L'identité de bibliothèque n'est **pas persistée entre redémarrages** sans mécanisme de réassociation futur. DEV 3 raccordera ultérieurement cette projection aux vues Avalonia après revue du contrat.


## NEXT-02 — moteur d'inventaire autonome intégré au code source

La [PR #68](https://github.com/TobbyCy/RomothequeManager/pull/68), désormais fusionnée sur `main`, introduit un moteur .NET d'observations de fichiers **indépendant d'ES-DE et de l'UX #64**. Il exige une racine locale explicitement sélectionnée avec `ROMS/`, ne crée aucun `GameId`, rejette les reparse points, refuse URL/réseau et ne lit que des playlists texte bornées ; pas de hash de ROM, copie, suppression, scraping ou upload. `InventorySnapshot` distingue `COMPLETE/PARTIAL/FAILED`, disponibilité, couverture, compteurs inconnus et groupes de doublons uniquement candidats. Un scan partiel/échoué ne remplace pas le dernier complet. Aucun scan automatique au démarrage, aucun test sur bibliothèque privée. Contrat P1 [#66](https://github.com/TobbyCy/RomothequeManager/pull/66) également fusionné ; code et tests synthétiques validés par la [CI `push main` #40691495453](https://github.com/TobbyCy/RomothequeManager/actions/runs/40691495453) sur `9830c5a2…`. L'adaptation `CatalogueReadModel` et le branchement Avalonia restent **non livrés** : **aucun bouton ni parcours Windows ne permet encore l'inventaire NEXT-02** ; aucun usage réel autorisé.


**Revue DEV 2 — correctifs P1 de robustesse `.m3u` (PR #68 intégrée au code source).** Le moteur candidate applique un plafond d'octets et de lignes pendant la lecture ; l'annulation en cours de lecture rend `PARTIAL` avec diagnostic `CANCELLED` et conservation du dernier `Current` complet. La fermeture des flux est garantie dans le chemin normal et annulé, et l'indisponibilité liée à un verrou concurrent retourne un diagnostic sans inventer des suppressions. Les opérations `Scan` et `RelocateVerified` sont **single-flight, non thread-safe** : leur appelant futur devra sérialiser les appels ; le contrôle check-then-use des reparse points conserve des courses TOCTOU résiduelles, **aucune garantie de sûreté contre modifications adversariales simultanées et aucun GO scan réel**. Le test des symlinks est déclaré `NOT_TESTED` si l'hôte Windows ne permet pas leur création. Une relocalisation par empreinte de métadonnées n'est pas une preuve binaire d'identité physique ; aucune campagne de hachage n'est autorisée dans ce lot.


## DEMO-LIBRARY-01 — générateur DEV 1 aligné au catalogue propriétaire (PR Draft #73)

Contrat source de vérité : PR #72 HEAD `968d1f61420a5d0b70eb5160e9d57d505560f2b7` ; 30 jeux fictifs exacts, 13 plateformes, identités et médias/simulations décrits dans `contracts/demo-library-01/`. DEV 1 génère sous chemin local explicitement choisi et inexistant les scénarios IDEAL (32 ROMS/30 XML/180 PNG), CHAOS (33/30/166, deux ROMs absentes, 3 doublons candidats), MYSTERY (32/0/0). Images PNG réelles 100 % originales ; fausses ROMs texte non exécutables. Tests entièrement synthétiques ; fixtures historiques #64 conservées, aucun scan de bibliothèque réelle, téléchargement, suppression de données personnelles, merge ou Release. La PR reste soumise à la revue indépendante Agent.


## DEMO-02 — exploitation des bibliothèques fictives (PR Draft #75)

La recette Windows `tools/demo-library/Invoke-Demo02Recipe.ps1` réutilise strictement le générateur DEMO-01 de `main` après intégration des PR #72/#73/#69 ; la CI `push main` initiale #40732508284 est verte. Les trois sorties sont créées uniquement sous une nouvelle destination absolue locale choisie par l'opérateur et vérifiées par les manifests. La suite MSTest existante a quatre tests E2E supplémentaires : import NEXT-01, inventaire NEXT-02, 30 jeux de référence, 13 plateformes explicites, 32/30/180 (IDEAL), 33/30/166 (CHAOS), 32/0/0 (MYSTERY), deux ROMs manquantes, médias absents, trois groupes de doublons potentiels, playlist de deux disques, idempotence et lecture seule. Aucun scanner parallèle, aucun GO de scan réel, suppression, scraping, fusion ni Release. La PR reste Draft et nécessite revue indépendante avant intégration.


**Contrôle croisé DEMO-02 (PR #75 vs #76/#77).** Le dossier `docs/ux/demo02-technical-cross-review.md` fournit la matrice honnête des 28 scénarios, sans déclarer PASS les parcours UX non testés. Deux tests synthétiques supplémentaires assurent l'exécution effective du lanceur Windows PowerShell de recette sur une sortie temporaire (`-SkipDotNetTests`, sans suppression/overwrite) et la conservation de `Current` après tentative NEXT-02 annulée (`LastAttempt=PARTIAL`). Le raccordement réel NEXT-02 à l'interface, les états Offline/Stale historiques, la comparaison A/B de scénario et les tests manuels d'accessibilité Windows demeurent hors périmètre du livrable DEV 1 ; aucune Release ou scan réel autorisé.
