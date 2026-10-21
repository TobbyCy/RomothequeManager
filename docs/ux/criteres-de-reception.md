# Critères de réception UX — futurs tests sur fixtures

**Tous les cas sont NOT_EXECUTED.** La validation des maquettes ne démontre pas la disponibilité du backend, du ZIP ni des périphériques. Le futur développement doit attribuer un HEAD exact et un oracle à chaque test.

| ID | Scénario synthétique | Résultat attendu |
| --- | --- | --- |
| UX-01 | Lancement sans scan physique | Dashboard accessible depuis index fictif, aucune lecture de ROM réelle |
| UX-02 | Source ROM Offline + index toujours accessible | Catalogue historique daté visible, pas zéro jeu |
| UX-03 | Index indisponible | Écran d'indisponibilité, aucun compteur inventé |
| UX-04 | Disponibilité NON OBSERVÉE | Pas de ONLINE/OFFLINE choisi arbitrairement |
| UX-05 | Dernier complet + tentative partielle | Deux références séparées, aucun jeu « supprimé » hors couverture |
| UX-06 | Deux snapshots incompatibles | Comparaison refusée ou bornée à l'intersection vérifiable |
| UX-07 | Quatre modes Collection | Grille/Liste/Mixte grille/Mixte liste cohérents |
| UX-08 | Fiche et retour | Filtres, sélection et scroll préservés |
| UX-09 | Métadonnée ES-DE folder ambiguë | Pas de conversion de dossier en jeu |
| UX-10 | Assistant quatre pages | Simulation ne lance ni copie ni E6 |
| UX-11 | Diagnostics non revérifiés | Ne pas marquer résolu ni intègre |
| UX-12 | Retour d'une lettre USB différente | Identité logique préservée ou reliaison refusée |
| UX-13 | Packaging Windows autonome | ZIP téléchargé, SHA-256 égal, démarrage externe sans runtime installé |
| UX-14 | Approche clavier/accessibilité | Focus visible, libellés non réduits à couleur |
| UX-15 | Média manquant, source Stale | Fallback explicite et provenance |

**Scope des tests** : fixtures inventées uniquement. Le futur test externe d'une GitHub Release Préversion est un gate distinct et demande une autorisation de développement/packaging. Un test UX ne confirme jamais l'innocuité sur une vraie bibliothèque.
