# NEXT-05 — Parcours UX IDEAL (référentiel de vérité sur fixtures)

**État :** `main=7aa62c8f` au 02.11.2026, parcours et composants **intégrés/testés sur fixtures**, sans qualification humaine du ZIP bêta #154. Cette note décrit le comportement attendu et ses preuves automatisées ; elle n'est **ni** une maquette garantissant toutes les vues futures, **ni** la documentation d'une nouvelle Release. Le ZIP public reste [v0.4.0-alpha.1](../../README.md#télécharger-et-essayer) avec un périmètre antérieur.

## Parcours observables / frontières

| Parcours UX | Intégré sur fixtures dans `main` | À ne pas inférer |
| --- | --- | --- |
| Accueil et import historique ES-DE | Démarrage sans import, sélecteur natif puis confirmation read-only, projection `Current` / `LastAttempt`, filtres/scroll | Scan personnel automatique ou enregistrement d'un grant permanent |
| Collection | Recherche et pagination **50 cartes au plus**, ouverture fiche/retour, référence média consentie et placeholder neutre | Groupements implicites, favoris/tags persistants, quatre modes mixtes UX spécifiés |
| Inventaire autonome ROMS | Racine de test explicitement choisie, démarrer/annuler, diagnostic `Complete/Partial/Failed/Offline/Stale` | Couverture totale d'une ROMothèque personnelle, « zéro jeu » en cas d'inconnu |
| Index MEDIAS et preview | Consentements distincts et contrôles de références, aperçu PNG/JPEG borné et ressource UI libérée | Scraper, attribution automatique, preview personnelle |
| Persistance SQLite | Snapshots/claims/décisions et backups contrôlés dans un stockage synthétique, restauration `Offline/Stale` | Bouton de sauvegarde personnelle dans l'Alpha, support USB automatiquement réassocié |
| Navigation et accessibilité | Contrôles natifs sur fixtures, correction B-05 de la Collection **760×480**, focus filtre/recherche/carte | Validation humaine de tous DPI, Tab/Shift+Tab et matériels |

**La View** possède les sélecteurs natifs, les contrôles/focus, le Dispatcher et les bitmaps adoptés. **La VM** porte la sélection acceptée, les commandes, l'état, Cancel et les générations ; elle ne doit pas ouvrir des chemins ni décoder. **Les services injectables** effectuent l'I/O exclusivement dans le périmètre des fixtures ou des permissions explicitement consenties dans leurs contrats. La voie historique ES-DE ouvre les médias par **`EsDeImportSession.TryOpenMedia`**, pas un second résolveur.

## États, informations manquantes et feedback

- `Current` / `CurrentComplete` = état **accepté** et cohérent dans sa portée ; `LastAttempt` = dernière opération, pouvant être `Partial`, `Failed`, `Cancelled`, `Offline` ou `Stale`. N'ignorer ni écraser l'état accepté lors d'une tentative échouée.
- `Offline` désigne une indisponibilité **constatée**, `Stale` une donnée non fraîche ; `Partial` décrit une couverture insuffisante et `Unknown` l'absence de preuve. Dans tous ces cas, **ne pas convertir en « supprimé » ou compteur zéro**.
- Sur changement de A vers B : l'UI invalide les requêtes médias A en cours, ne publie aucun résultat tardif et libère les bitmaps attachés. Les choix manuels ne doivent jamais être générés par des heuristiques sans consentement.
- Média absent/hors budget/non autorisé : **placeholder lisible**, diagnostic neutre sans chemin ni exception privée. Fallback image→thumbnail seulement si le XML déclare les deux références et que chacune passe les contrôles.
- Consentement : **choisir une racine n'est ni l'autoriser ni démarrer un scan**. ROMS, MEDIAS et PREVIEW ont des frontières d'accès distinctes ; le consentement est explicite, limité et révocable.

## Preuve native B-05 et ses limites

[PR #151](https://github.com/TobbyCy/RomothequeManager/pull/151) a été fusionnée après revue DEV 2. La première capture `B05-Collection-760x480.png` coupait le contenu, parce que la capture était lancée avant stabilisation de la largeur Win32. Après attente du vrai layout/Dispatcher, les vérifications natives sur **50 cartes** à **760×480 et 1120×720** démontrent le cadrage horizontal du titre, du filtre, du compteur et de la première carte, ainsi que focus et navigation de la fiche vers la Collection. [Preuve native corrigée](https://github.com/TobbyCy/RomothequeManager/actions/runs/41422239083) : 9/9 ciblés et six PNG fictifs ; [revue DEV 2](https://github.com/TobbyCy/RomothequeManager/pull/151).

Cette preuve est limitée au rendu synthétique de ce run ; elle ne remplace pas les tests humains, l'accessibilité au clavier sur toutes plateformes/DPI, l'import d'un SSD réel ou la validation de la bêta.

## Parcours futur et interdictions de conclure

Les [spécifications Collection avancée](02-collection.md), [Bibliothèques/snapshots](04-bibliotheques.md), [assistant export](05-assistant-export.md) et [centre Diagnostics](06-diagnostics.md) contiennent des objectifs qui **ne sont pas tous implémentés**. L'UX actuelle ne promet pas recherche multimode complète, association jaquette automatique, export/réorganisation, scraper réseau, SteamOS ni fonctions physiques.

La PR [#154 — ZIP IDEAL](https://github.com/TobbyCy/RomothequeManager/pull/154) reste **DRAFT / CI required rouge** : packaging et probe SQLite natif probants séparément, mais GUI du vrai EXE extrait non-admin **non validée**, cause indéterminée. La [recette dédiée #95](https://github.com/TobbyCy/RomothequeManager/issues/95) n'a pas été exécutée ; le laboratoire Avalonia minimal ne teste pas cette application. **Aucune nouvelle Release ni permission personnelle** ne découle des captures UX.

Voir [parcours utilisateur prudent](../user/parcours-ideal-next05.md), [FAQ](../user/faq-depannage-next05.md), [sécurité](../user/securite-et-limites.md), [guide développeur](../developer/README.md) et [état](../../STATUS.md). L'[ADR MVVM NEXT-03](next03-mvvm-foundation.md) est historique.
