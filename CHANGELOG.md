# Journal des changements

## NEXT-03 — service applicatif DEV 1 (PR Draft #85, 28.10.2026)

- Service `IInventoryApplicationService` ajouté dans l'assembly V2 **existante**, sans extraire Core ni modifier l'unique moteur NEXT-02. Interfaces et coordination sans dépendance Avalonia/MVVM au niveau source.
- Préflight limité à la sélection locale consentie, scan de fixture en tâche de fond, single-flight, annulation, `Current/LastAttempt`, observation/diagnostic/couverture et états d'origine. Le constructeur public bloque les scans de vraies bibliothèques.
- Nouveaux tests synthétiques IDEAL/CHAOS/MYSTERY, annulation pendant playlist, concurrence, échec Offline, propagation de l'état Stale simulé et intégrité des fixtures. **PR Draft, aucun raccordement Avalonia, fusion ni Release.**

## LIBRARY-UX-02 — sélection des scénarios et médias ES-DE (PR Draft #77, 28.10.2026)

- Sélection explicitement fictive IDEAL, CHAOS, MYSTERY ; navigation par Dashboard, Collection et fiche. Scénarios IDEAL/CHAOS décrits par contrats, mais non revendiqués comme des fichiers observés ; MYSTERY ne présente **aucun jeu identifié**.
- Galerie d'images limitée aux types pris en charge par l'import ES-DE existant : jaquette, miniature, logo ; badges honnêtes pour média introuvable, illisible, non renseigné, provenance du contrat et ROM seulement prévue. Pas de scraping, scan, export ou gestion de bibliothèque réelle.
- Tests unitaires du contrat de présentation, capture Windows cinq vues aux deux tailles et artifact ZIP de démonstration avec provenance SHA. Workflow nouveau ajouté à la liste de chemins autorisés **exactement par son nom**, avec contrôles de sécurité associés ; protections de Release et baseline historique inchangées.
- **En cours de revue** : aucune fusion ni Release avant CI complète et recette propriétaire.

- DEMO-02 revue croisée avec la recette DEV 2 #76 (28 scénarios) et la UI Draft DEV 3 #77 : matrice détaillée des preuves moteur versus tests UI, état NOT_EXECUTED/NOT_IMPLEMENTED pour les parcours absents. Deux tests ajoutés pour exécuter le script Windows en sortie temporaire et tester l'annulation NEXT-02 sans perdre le dernier complet. Aucun moteur, fixture canonique, scan réel ou Release modifié.

## DEMO-02 — recettes synthétiques E2E (DEV 1 / PR Draft #75, 28.10.2026)

- Procédure Windows PowerShell 5.1 opt-in pour générer et vérifier IDEAL/CHAOS/MYSTERY à destination locale nouvelle et explicitement choisie ; aucun écrasement, téléchargement, scan réel ni Release.
- Quatre tests E2E supplémentaires exécutent les moteurs existants NEXT-01 et NEXT-02 sur les fixtures DEMO-01, avec les 30 titres, 13 plateformes, oracles 32/30/180, 33/30/166, 32/0/0, diagnostics de fichiers/médias absents, 3 doublons candidats, playlist multidisque et stabilité de relecture. Aucun nouveau moteur ni modification de la génération historique.

## DEMO-LIBRARY-01 — catalogue canonique DEV 2 (PR Draft #72, 28.10.2026)

- Le propriétaire a fixé **30 titres et 13 plateformes** dans [Issue #71](https://github.com/TobbyCy/RomothequeManager/issues/71). Les références de conception sous `contracts/demo-library-01/` et `docs/architecture/` contiennent métadonnées, médias projetés, scénarios IDEAL/CHAOS/MYSTERY et oracles arithmétiques. Le générateur DEV 1 #73 attend son alignement sur cette version canonique.
- **Documentation exclusivement** : aucun fichier ROM, BIOS, image, XML ni test de génération sur ce catalogue livré ; aucune Release ni modification du produit.

## NEXT-01 / PR #64 — recette HEAD exact et hors ligne (28.10.2026)

- Le ZIP Windows CI est construit depuis le HEAD courant de #64, vérifié par SHA, plutôt que l'ancien `e775cdd6` ; manifeste et sidecar SHA-256 exacts.
- Vérifications spécifiques Windows et fixtures fictives : PE WinExe, ouverture via ShellExecute, lancement offline sous blocage Windows Firewall sortant pour l'exécutable, import/diagnostics visibles dans l'arbre Avalonia, supports locaux, XML malformé et restauration.
- La recette manuelle de Tobby (Explorer, clavier/GUI et déconnexion réelle du PC) reste distincte ; aucun moteur NEXT-02, scan réel, merge ou Release.


Ce journal distingue le **code fusionné sur `main`**, les preuves CI attachées à un SHA et les **versions réellement distribuées**. Le projet ne possède encore **aucune GitHub Release** ni ZIP Windows autorisé au téléchargement.


## Vérification de packaging de recette — NEXT-01-UX (28 octobre 2026)

- Workflow GitHub Actions [#40689889760](https://github.com/TobbyCy/RomothequeManager/actions/runs/40689889760) : ZIP autonome corrigé figé sur `e775cdd64522d45ee2fb848df232fa1f19023326` et [artifact #12816457518](https://github.com/TobbyCy/RomothequeManager/actions/runs/40689889760/artifacts/12816457518). Le ZIP applicatif pèse **75 362 552 octets** et son SHA-256 vérifié indépendamment est **`fe6a4d59adfae5ea171c229838d17fdeef4bd23a41e03924d2e5a93f6c407364`** ; manifeste embarqué et 62 tests .NET PASS.
- Exécutable extrait : PE Windows GUI (subsystem 2), smoke PASS. **Tobby doit encore tester en double-cliquant le .exe** et en lisant les diagnostics ES-DE de la fixture ; les tests ne simulent pas le geste dans Explorer.
- Après une intégration concurrente sur `main`, la référence de base de la PR #64 a été rafraîchie pour éliminer un échec de politique `MERGE_PARENTS_INVALID` lié au décalage d'historique. Politique maintenue inchangée, nouveau passage CI requis pour qualifier l'ensemble.
- Aucun merge de PR, aucune Release, aucun scan ni modification d'une ROMothèque réelle.


## En cours — NEXT-01-UX / ZIP corrigé WinExe (28 octobre 2026)

- Recette UX-ARTIFACT-CI désormais **figée sur le source correctif `e775cdd64522d45ee2fb848df232fa1f19023326`**, et non plus l'ancien SHA `cc35abf7`. Les garde-fous PR #64 / acteur propriétaire / droits read-only / fixture fictive restent intacts.
- ZIP autonome win-x64, licences et fichier .sha256, vérification à l'extraction, smoke et inspection PE du **sous-système Windows GUI** sur l'exécutable extrait. Guide Tobby actualisé : double-clic sans console et diagnostics d'import en français.
- CI et artifact Actions requis avant livraison. Aucun merge, Release ou scan réel.


## En cours — VISUAL-POC-01 retouches P1/P2 (28 octobre 2026)

- Navigation latérale plus contrastée : active/inactive/survolée/indisponible, avec canaris de lisibilité des textes.
- Cartes et marges moins hautes, sidebar déroulante à 760×480, six captures Windows à plusieurs échelles de rendu.
- Pas de nouvelles fonctionnalités : mode ES-DE de lecture seule, droits branding réservés et gates de distribution inchangés.


## En cours — VISUAL-POC-01 / Issue #67 (28.10.2026)

- Nouvelle PR Draft empilée sur #64 : Dashboard Darcula Retro centré sur la collection, accès bibliothèques, Exports désactivés et diagnostics avec états et origine explicites. Aucun moteur, scan automatique ni opération physique.
- Transfert octet pour octet des quatre SVG/ICO officiels depuis Pilotage (le PNG public reste inchangé) ; icône de fenêtre/barre des tâches et ressource ICO native de l'exécutable. Droits graphiques réservés, GO propriétaire limité à Produit et artifacts de test.
- Contrôles d'assets et workflows CI révisés **sans relâcher les règles de sécurité**, nouveaux tests fictifs et captures de rendu Avalonia sur runner Windows à confirmer. Maquette historique non disponible pour comparaison pixel à pixel ; aucun merge/Release.


## En cours — NEXT-01-UX-FIX / PR #64 (28 octobre 2026)

- Recette Windows propriétaire : l'exécutable Avalonia devient une application graphique `WinExe` pour éviter la console ouverte au double-clic. Le smoke Windows automatisé reste requis dans la CI.
- Diagnostics ES-DE : libellés français compréhensibles, regroupement « points à vérifier / informations », plateforme et entrée identifiables, sans assimiler une ROM ou un média indisponible à une suppression. Erreurs de gamelist, XML et réimport explicitées, dernier catalogue valide conservé.
- Correctifs uniquement de présentation et de configuration Windows ; adaptateur ES-DE de lecture seule inchangé. Quatre canaris MSTest synthétiques ajoutés. La CI du nouveau HEAD et le verdict externe seront documentés sur [PR #64](https://github.com/TobbyCy/RomothequeManager/pull/64). Aucun merge ou Release autonome.


## En cours — NEXT-01-UX / Issue #61 (PR #64 prête pour revue, pas sur main)

- Sur la base de l'adaptateur [#62](https://github.com/TobbyCy/RomothequeManager/pull/62) désormais fusionné, présentation Avalonia de la bibliothèque explicitement sélectionnée : séparation démo/import, Dashboard/Collection/fiches et jaquettes locales via API de lecture seule. Les images invalides ou absentes peuvent céder la place à la miniature, puis à un message explicite ; A/B synthétique désactivé pour le catalogue importé.
- Recherche et pagination à 50 jeux ; navigation conservant les identités et refus des erreurs de chargement/réimport sans perdre le dernier catalogue validé. Douze tests sources synthétiques NEXT-01-UX, incluant 2 000 entrées fictives et l'isolation de la comparaison A/B.
- **CI/captures à valider sur le HEAD exact après reciblage sur main** ; ce lot n'est ni fusionné, ni distribué, ni autorisé à scanner le SSD personnel. Aucune ROM/BIOS/média tiers n'entre dans GitHub.

## En cours — NEXT-02-INVENTORY / Issue #65 (PR Draft, fixtures exclusivement synthétiques)

- Correctif P1 de la PR #68 : lecture `.m3u` bornée **pendant** les I/O (octets/lignes), annulation effective transformée en `PARTIAL/CANCELLED`, handle refermé ; accès fichiers concurrents traités conservativement en `PARTIAL/UNREADABLE_ENTRY` ; nouveaux tests synthétiques. Le contrôle de taille préalable seul n'est plus considéré suffisant.

- Moteur candidat .NET en lecture seule, autonome d'ES-DE, avec sélection explicite de racine `ROMS/`, observation de fichiers distincte des identités de jeux, plateformes/alias explicitement configurés, extensions conservatrices, relations `.m3u`, groupes de doublons seulement potentiels et limites de ressources.
- `InventorySnapshot` indépendant, `COMPLETE/PARTIAL/FAILED`, couverture, disponibilités, compteurs inconnus et conservation du dernier snapshot complet lorsque le scan suivant échoue. Aucun scan automatique au lancement, aucune opération sur ROMothèque réelle.
- Dix-huit tests MSTest synthétiques proposés/exécutables sur répertoires temporaires ; preuves CI et revue DEV 2 à rattacher au HEAD exact. Contrat candidat : PR documentaire #66, non gelé sans revue.

## En cours — NEXT-01-A / Issue #61 (PR Draft, données synthétiques)

- Adaptateur .NET de lecture seule des métadonnées ES-DE vers une projection de catalogue en mémoire, avec chemins ROM/médias confinés, identités relatives, jaquettes et playlists multidisques.
- Préparation complète avant réimport, sans perte silencieuse du dernier état utile ; identité de bibliothèque stable pour la session et déplacement explicite vérifié, ouverture de média revalidée à la demande, avec tests synthétiques de lecture, sécurité, idempotence et indisponibilités.
- Aucun branchement UX, accès à une collection personnelle, modification des fichiers sources, merge ni nouvelle Release dans ce lot DEV 1.

## En cours — DIST-ZIP-01 / Issue #50 (PR Draft, aucun upload)

- Suite au GO propriétaire de **préparation de distribution de test**, ajout d'une voie `workflow_dispatch` manuelle et distincte du CI PR/push : phase `validate` sans upload, phase `publish` réservée à un SHA/ZIP/tag exacts et aux variables d'approbation finale du propriétaire.
- L'emballage vérifie les notices tierces et leur inventaire, le SHA-256, deux archives identiques, les fichiers extraits un à un, l'absence de contenus interdits et le smoke des quatre vues depuis le ZIP extrait.
- La publication visée, **après un second GO final**, est une **GitHub Pre-release « POC SYNTHÉTIQUE / TEST UNIQUEMENT »**, jamais une version stable : brouillon, retéléchargement/contrôle des deux assets, puis publication.
- **HOLD actuel :** notices validées DEV 2 non encore intégrées, contrôle indépendant Agent et artefact exact non vérifié pour distribution ; aucun ZIP téléchargeable.

## Intégré sur `main` — V2-01C / [PR #53](https://github.com/TobbyCy/RomothequeManager/pull/53) (non distribué)

- [Issue #50](https://github.com/TobbyCy/RomothequeManager/issues/50) : préparation d'un ZIP Windows x64 autonome (runtime .NET inclus), avec SHA-256, manifeste et notice explicite « POC SYNTHÉTIQUE ».
- **CI ciblée :** publication self-contained et tests depuis un dossier extrait distinct, comparaison de la liste des fichiers et contrôle d'absence de ROM/BIOS/sauvegardes. Le smoke automatisé exige l'ouverture de la fenêtre et quatre vues ; les parcours clavier, focus, scrolling et redimensionnement interactifs restent à tester sur Windows externe.
- **Distribution suspendue :** ZIP et empreinte restent temporaires dans le runner CI. Aucun upload GitHub Actions, Release, Pre-release, signature ou partage public n'est autorisé ; revue des notices tierces et GO propriétaire distinct nécessaires.

## Intégré sur `main` — V2-01B / [PR #48](https://github.com/TobbyCy/RomothequeManager/pull/48) (non distribué)

- [Issue #47](https://github.com/TobbyCy/RomothequeManager/issues/47) : quatrième vue Avalonia Bibliothèques & snapshots, trois bibliothèques cohérentes avec six jeux fictifs, états indépendants, provenance, sélection A/B et comparaison **préqualifiée** à six classes. Modèle de présentation synthétique versionné, sans moteur métier C# parallèle.
- **Contrats et garde-fous intégrés :** validation fail-closed des partitions de couverture (multi-root, ROMS/MEDIAS, preuves uniques/exactes), champs A/B et scopes concordants avec les générations canoniques, refus des faux COMPLETE, rendu honnête de l'index inaccessible et libellé « Disponibilité ». Canaris MSTest positifs/négatifs.
- **Oracle de démonstration :** fixture `UI-only / handcrafted deterministic demo / NOT_ENGINE_VERIFIED`, trois paires A/B dont les six classifications et dénominateurs A∪B sont vérifiés contre des attentes codées en dur. Aucune prétention d'équivalence avec le comparateur PS5.1.
- **Limite UX :** `INTERACTIVE_KEYBOARD_SCROLL_NOT_TESTED` ; le smoke quatre vues ne certifie pas le parcours clavier/focus/scroll réel.
- **Statut : code intégré, pas distribué.** Le ZIP V2-01C demeure un candidat sous contrôle CI ; aucune Release.

## Intégré sur `main` — 23 octobre 2026 (non distribué)

- **V2-01A / [PR #42](https://github.com/TobbyCy/RomothequeManager/pull/42), Issue #41 — fusionnée :** premier POC Windows .NET 10 / Avalonia 12.1.4 en lecture seule, avec trois vues (Dashboard, Collection/recherche, Fiche) et six jeux entièrement fictifs en mémoire. Deux `packages.lock.json` et `global.json` SDK **10.0.401** exact (`rollForward=disable`). **8 MSTest** et smoke de fenêtre Avalonia démontrés sous Windows CI. Aucun scan, export, ROM/BIOS, persistance ou réseau métier.
- **CI-DOTNET-01 / [PR #45](https://github.com/TobbyCy/RomothequeManager/pull/45), Issue #43 — fusionnée :** provisionnement explicite du SDK 10.0.401 dans le job `.NET` via `actions/setup-dotnet` épinglé à `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4.3.1), après diagnostic hors dépôt. Pin, locks, tests non vides, canaris négatifs, gate final et interdiction d'upload ZIP inchangés. La [CI `push main` #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607), sur `408d6263be6ae1fbc77c31f6eb9fb530cb4f76e6`, est **SUCCESS**. La validation sur une image où le SDK est initialement absent reste une limite distincte du succès du run.
- **PUB-04 / [Issue #44](https://github.com/TobbyCy/RomothequeManager/issues/44) — terminé :** les quatre anciens artefacts ZIP `v2-01a-win-x64-candidate` ont été supprimés de GitHub Actions et la purge a été contre-vérifiée ([preuve](https://github.com/TobbyCy/RomothequeManager/issues/44#issuecomment-6359600451)). Les nouveaux ZIP restent produits/hashés **temporairement dans le runner**, sans téléversement. Aucune autorisation de redistribution ni garantie sur d'éventuelles copies déjà téléchargées.
- **Prototype web et moteur PowerShell :** les démonstrations locales, projections de bibliothèques/snapshots, catalogue synthétique, validations Node/Edge headless, Pester et préflights restent dans le dépôt public. Le contrat DTO 0.2 et la stack Windows ne sont pas adoptés comme architecture finale.
- **Fonctions futures non livrées :** persistance, favoris/tags, collecte réelle, export E2–E6 et distribution Windows/SteamOS. La quatrième vue Bibliothèques/snapshots est intégrée via V2-01B, sans validation UX interactive externe.

Les notes ci-dessous sont **conservées comme historique de rédaction des sprints**. Les termes « candidat », « PR Draft », « à revalider », « non fusionné » et les états d'artefacts qu'elles contiennent décrivaient leur moment d'écriture ; **ils ne constituent pas l'état courant**. Consulter [l'état produit](docs/product-status.md) pour l'inventaire actualisé.

---

## Archives des notes de sprint (contenu historique conservé)

## Non publié — développement

- **CI-DOTNET-01 / Issue #43 (candidat)** : l'image `windows-2022` mise à jour (20261021.343) n'inclut plus le SDK `10.0.401`, remplacé par `10.0.402`. Provisionnement explicite du SDK **exact** via `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4.3.1) seulement pour le job .NET, diagnostic hors dépôt, garde CI strict et canaris négatifs. Le `global.json` reste intact ; tests, gate final et interdiction des uploads ZIP à revalider sur le HEAD du correctif. Aucune Release ni fusion par DEV 1.


- Revue V2-01A : ajout de `global.json` (SDK .NET **10.0.401** exact et roll-forward désactivé), validation stricte et canaris. Audit des 225 entrées du ZIP : seulement licence MIT du Produit et notice générique, aucune attribution tierce vérifiée. **Upload des nouveaux ZIP suspendu**, build/tests/ZIP SHA toujours exécutés ; suppression manuelle des artefacts historiques requise par le Contrôleur. Aucune Release ni adoption définitive.


- V2-01A / Issue #41 (PR #42 candidate) : fenêtre Avalonia 12.1.4 sous .NET 10 avec trois vues et six jeux entièrement fictifs. Deux locks NuGet générés réellement, 8 tests MSTest exécutés, ouverture Windows des trois vues et ZIP `win-x64` self-contained de 75 Mo avec SHA-256 et artefact CI temporaire. [Preuve sur SHA source intermédiaire](https://github.com/TobbyCy/RomothequeManager/actions/runs/39904964788) ; CI du HEAD de livraison, PC externe sans runtime, licences tierces et revue indépendante restent à valider. Aucune Release, aucune ROM, aucun accès StateRoot, aucune adoption définitive.


- VAL-01 / Issue #39 : smoke **Microsoft Edge réel sous Windows**, en mode headless sur les deux pages HTML `file://` et données fictives. Pilotage CDP par Node natif sans dépendance téléchargée ; gestes souris, navigation, fiches, comparaison, index inaccessible, OFFLINE/STALE/UNKNOWN, exceptions JavaScript et requêtes réseau non autorisées. Contrôle obligatoire dans CI-0 ; preuve Edge et gate à vérifier sur SHA exact, aucun navigateur humain ni Release présumés.

- Issue #2 : correction de l'indentation du commit conditionnel dans le test Python de routage CI (`tests/test_ci_policy.py`, lignes 555–556) ; remplacement d'un canari négatif de chemin devenu obsolète (`.github/CODEOWNERS`, autorisé) par `.github/CODEOWNERS.txt` (toujours interdit et non ignoré par Git). Huit chemins négatifs contrôlés sans assouplir l'allowlist. Une assertion PS5.1 Bibliothèques découvert après déblocage de Python conserve son critère strict et affiche désormais l'exception observée en cas de refus incorrect ; gate complet à revalider.

- Issue #37 : les canaris de collision d'identité et de preuve utilisaient des références partagées aux snapshots nominaux. Chaque registre volontairement muté utilise désormais deux snapshots indépendants ; assertions additionnelles contre les mutations croisées. Validateur, refus d'index et gates inchangés ; CI à vérifier sur le HEAD exact.

- Couverture Pester CI : contrôle fermé des 22 noms de modules actuellement suivis (18 historiques + 4 extensions), tout en conservant la mesure reproductible des 18 modules historiques et de l'export E1. Aucun seuil de couverture artificiel et aucune suppression de canari ; nouvelle preuve runtime attendue sur le HEAD final.

- Guides du produit, de la contribution et de la sécurité, lexique et documentation d'architecture.
- Parcours d'interface et critères d'acceptation décrits pour les travaux futurs.
- Préparation des contrôles du moteur PowerShell 5.1 sur données synthétiques.
- Licence MIT adoptée pour les éléments originaux autorisés (`SPDX-License-Identifier: MIT`, copyright 2026 Cyril Tobler) ; garde CI à chemin exact pour `LICENSE` et admission du script préflight synthétique déjà suivi `tests/Invoke-CiE1PreflightSynthetic.ps1`. Aucun droit tiers ou média externe concédé.

**Aucune application Windows, ZIP ni GitHub Release n'a encore été publiée.** Les PR non fusionnées ne constituent pas des fonctionnalités disponibles.

## En préparation — UI #11 (PR Draft #28, non fusionnée)

- Prototype **hors ligne HTML/CSS/JavaScript natifs**, situé dans `prototype/ui11/` : navigation Dashboard, Bibliothèques, Collection, Comparaison et inspecteur.
- Quatre bibliothèques **fictives** et historique daté ; états ONLINE/OFFLINE/INACCESSIBLE/UNKNOWN, FRESH/STALE/UNKNOWN, dernier complet vérifié indépendant de la dernière tentative.
- Sélection A/B, six classifications, filtres et détail ; couverture A/B toujours affichée, y compris en cas de liste vide sous scan partiel. Aucun libellé de suppression physique.
- Miroir JavaScript du contrat de comparaison #26 **pour démonstration uniquement**, non une exécution PowerShell ni une preuve de parité avec Dev 1.
- Test V8 à DOM simulé effectué en session ; smoke Node.js fourni mais **non exécuté**, pas de capture ni de test navigateur graphique, CI/Windows **NOT_EXECUTED**.
- **Non livré** sur `main`, aucun exécutable Windows, ZIP ou Release, aucune lecture/écriture ROM.


- Sprint #13, **PR Draft #28 uniquement** : adaptateur de projection JSON synthétique `0.1.0-synthetic`, deux modes, contrôle des six comptes, de la couverture et refus sans paire précalculée. **Non fusionné, non publié** ; aucune parité PS5.1 revendiquée.


- **Sprint #15, PR Draft #28** : contrat candidat catalogue de jeux et fiche projetée, provenance datée, variantes, observations ROM séparées, navigation vers snapshots ; sample synthétique hors ligne et smoke Node ajouté (NOT_EXECUTED). Adaptateur registre aligné sur `countKnown`/compteur null. 9 contrôles V8 avec DOM simulé PASS, aucune parité PS5.1 ni recette navigateur.

## Comparaison de snapshots synthétiques — Issue #9

- Module PowerShell de comparaison A/B avec six classifications et couverture prouvée des deux générations.
- Identités `romUnitId` comparées ordinalement et sensibles à la casse ; canaris synthétiques associés.
- Aucun effet sur les fichiers ROM ; tests PS5.1 et CI sur l'arbre consolidé **non vérifiés**.

## Projection synthétique des bibliothèques — Issue #10

- Registre de bibliothèques et générations, disponibilité et fraîcheur distinctes, couverture fiable ou déclarée inconnue.
- Exporteur JSON synthétique pour interface hors ligne et canaris négatifs ; aucun effet physique sur ROM.
- Tests PowerShell 5.1 et CI sur cette intégration : **non vérifiés**.

## Catalogue synthétique partagé — PR #29

- Modules PowerShell de projection et validation du catalogue, contrat DTO candidat 0.2, consommateur JS canonique, provenance et compteurs fail-closed.
- Tests et fixtures négatives versionnés ; contrat encore candidat, pas d'export réel de collection.
- Tests Node/PS5.1 et CI de l'arbre consolidé **non vérifiés**.

## Catalogue consultable et fiches — PR #30

- Extension du prototype UI aux fiches, recherche et filtres sur données synthétiques, avec validation JS partagée et contrôles négatifs.
- Tests UI supplémentaires versionnés ; V8 simulé historique ne certifie pas Node natif, Edge ni la chaîne PS5.1→JSON→JS sur cet arbre.
- Aucune application autonome ni Release.

## CI publique et sécurité de contribution — PR #31

- Raccordement de huit suites Node du prototype à la CI, dont la vérification Unicode alimentée par le producteur PowerShell.
- Vérification des événements et permissions minimales GitHub Actions, CODEOWNERS et gate final fail-closed.
- Règles d'administration de `main` à configurer après PUBLIC. Exécution Actions sur cet arbre non vérifiée.

## Convention

Après chaque changement intégré, préciser le comportement, la compatibilité, les risques, les tests réellement exécutés et la référence de l'Issue ou PR de **ce produit**. Une version publiée nécessite un tag, des artefacts vérifiés et des notes de limites. Ne jamais annoncer un test non exécuté comme PASS.

## DEMO-LIBRARY-01 — DEV 1 / PR #73 (DRAFT)

- Générateur Node sans téléchargement ni dépendances supplémentaires, produisant des ROMs fictives en texte et de vrais PNG originaux sous destination explicitement choisie et inexistante. Source canonique stricte : PR DEV 2 #72, HEAD `968d1f61420a5d0b70eb5160e9d57d505560f2b7`, 30 titres et 13 plateformes (Xbox, PS2, PS3, PS4, PC compris). Oracles : IDEAL 32/30/180, CHAOS 33/30/166, MYSTERY 32/0/0. Tests synthétiques de génération, import ES-DE, inventaire, médias, sécurité et intégrité. Aucun merge ni Release.
