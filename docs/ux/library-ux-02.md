# LIBRARY-UX-02 — parcours des bibliothèques (DEV 3)

Statut : **PR Draft / recette propriétaire requise**. Périmètre strictement Avalonia, présentation et fixtures fictives, sans moteur, scrapers, scan ni opération sur une ROM réelle.

## Deux origines à ne jamais confondre

- **Scénarios IDEAL/CHAOS/MYSTERY** : aperçus de conception établis à partir des contrats synthétiques `contracts/demo-library-01/{catalogue,scenarios}.json`. Les données annoncées restent des **prévisions de fixture**, et non une lecture de disque. IDEAL/CHAOS disposent de titres et de métadonnées **déclarés dans les XML de scénario**. CHAOS distingue référence conservée, ROM absente attendue, média manquant attendu et doublon seulement candidat. MYSTERY n'a aucun XML : aucune identité, titre, image ni nombre de jeux confirmé n'est rendu dans son catalogue.
- **Bibliothèque ES-DE choisie par l'utilisateur** : l'import existant lit en mémoire les gamelists sélectionnées **sur action explicite**, sans scan automatique. Les jaquettes/miniatures/logos réellement déclarés dans l'XML sont ouverts localement en lecture seule via `TryOpenMedia` et un véritable décodeur Avalonia, avec repli distinct si absent/non renseigné/illisible. Les captures, dos de jaquettes et arrière-plans non exposés par l'importeur ne sont **pas inventés**, même si les fichiers existent dans la fixture.

## Parcours cible

Dans le mode Démonstration, ouvrir « Bibliothèques & supports », choisir IDEAL, CHAOS ou MYSTERY. Le choix est conservé pendant le parcours Tableau de bord → Collection → Fiche → Retour Collection. La recherche est limitée au scénario sélectionné. La fiche garde sa provenance de scénario, sans bouton d'édition. Un bouton explicite permet d'importer **une fixture locale** via le sélecteur déjà existant pour voir les médias réels. Le retour au mode Démonstration rétablit la vue historique des six fixtures.

IDEAL présente la spécification de 30 jeux/13 plateformes et six médias **prévus** par fiche. CHAOS présente les mêmes 30 entrées XML avec des anomalies annoncées sans qualifier les doublons de confirmés. MYSTERY n'expose aucun titre ni fiche ; la Collection affiche un état « identité inconnue » sans compteur de jeux à zéro.

## Couverture automatisée du parcours

Le smoke Windows réel `LIBRARY_UX02_JOURNEYS_PASS` vérifie séparément IDEAL et CHAOS : Tableau de bord → Collection → Fiche → Retour Collection → Tableau de bord, provenance fictive, ROM absente prévue pour CHAOS et invalidation de la fiche précédente après changement de scénario. Pour MYSTERY, il vérifie Tableau de bord → Collection → refus d'une fiche non identifiée → retour, absence de titre/ID oracle. **Le refus de fiche est le comportement correct pour MYSTERY**, pas un défaut de navigation.

Huit captures de rendu Avalonia sont vérifiées aux formats 1120×720 / 760×480 (dont les détails IDEAL et CHAOS, accueil et Collection MYSTERY). Ces scénarios ne constituent ni un test de vrai clic souris, ni un scan NEXT-02, ni une preuve d'observation de fichiers. Le libellé MYSTERY « Comprendre les données inconnues » ne promet pas une liste de fichiers tant que le raccordement des observations NEXT-02 n'existe pas.

## Recette propriétaire — PASS/FAIL

1. Sous Windows x64, vérifier Dashboard Darcula Retro, logo et icône officiels, menu et panneaux défilants à 1120×720 puis 760×480 ; vérifier Tab, focus et survol. Aucune fonction d'export active.
2. Choisir IDEAL → Dashboard → Collection → fiche « Metal Deer Salad » → retour. Vérifier titres, provenance « contrat synthétique » et légendes « attendu » et « non vérifié ».
3. Choisir CHAOS → Collection ; les références manquantes 005/017 doivent rester visibles, les médias manquants signalés sans affirmer un effacement. Pas de doublon confirmé.
4. Choisir MYSTERY → Collection ; aucun nom de jeu issu de l'oracle, aucune « 0 jeux identifiés » et aucune fiche fictivement reconnue. Message « INCONNU ».
5. Importer volontairement la racine `IDEAL` ou `CHAOS` générée dans un répertoire **fictif** par DEV 1 : médias locaux effectivement décodés, logo ES-DE si déclaré, repli absent/illisible, aucun téléchargement ni écriture.
6. Sur un PC sans Internet, refaire navigation et import de fixtures (test humain distinct du blocage réseau CI). Comparer les captures Windows de la PR ; signaler toute troncature, perte du focus, ou confusion entre état « non observé » et « manquant ».
7. Vérifier que la dernière source valide persiste après un XML invalide (régression NEXT-01), et que les boutons de pagination sont désactivés sur une page unique.

Captures CI : vrais rendus des contrôles Avalonia sur Windows runner, **pas** des captures de la barre de tâches/Explorateur ou du DPI natif Windows. Aucun merge ni Release sans GO propriétaire distinct.
