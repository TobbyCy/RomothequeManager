# V2-01A — POC Windows synthétique (Issue #41)

**Statut : candidat technique, non adopté, non distribué.** Un GO limité autorise
uniquement la démonstration Windows .NET 10 + Avalonia UI Community 12.1.4.

Trois vues : Dashboard, Collection, Fiche. Six jeux **inventés** et des
bibliothèques fictives OFFLINE/UNKNOWN/ONLINE avec provenance et horodatage.
L'absence de source n'est jamais interprétée comme une suppression de ROM.
Ni stockage ni lecture de ROM, BIOS, StateRoot, XML, réseau ou disque externe.

Le projet UI `src/RomothequeManager.V2` est distinct du moteur historique PS5.1.
Les fixtures sont codées en lecture seule et volontairement non représentatives
de la couverture d'une véritable collection.

Le projet de tests `tests/dotnet/RomothequeManager.V2.Tests` contient des
tests MSTest non vides. La CI publique exige des locks NuGet conformes,
restore `--locked-mode`, audit, build sans avertissement et un TRX prouvant
l'exécution de tests. Les locks initiaux sont **à générer via NuGet réel** avant
toute réception ; aucun résultat de build ni de packaging n'est présumé.

Objectif d'artefact ultérieur : `dotnet publish -c Release -r win-x64
--self-contained true -p:PublishSingleFile=false`, ZIP de dossier complet,
inventaire des dépendances et SHA-256, uniquement en candidat CI. Artefact éphémère GitHub Actions (rétention 7 jours), sans GitHub Release. Vérifier les licences des composants transitifs avant toute distribution publique.
La présence de SDK/.NET sur le runner ne prouve pas la portabilité sur PC
sans runtime ; essai Windows propre externe encore requis avec permission.

Ne pas utiliser ce code sur une bibliothèque réelle ou en production.
