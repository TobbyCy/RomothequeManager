# UI #11 — Prototype interactif hors ligne

**Statut : prototype de parcours, pas application Windows finale.** Branche / PR : [Produit #28](https://github.com/TobbyCy/RomothequeManager/pull/28). Aucun framework imposé ; HTML, CSS et JavaScript natifs, sans CDN, package, accès réseau ou dépendance externe.

## Démarrage

Ouvrir **`index.html`** dans un navigateur moderne (double-clic possible, URL `file://`). Les fichiers `fixtures.js`, `app.js` et `styles.css` doivent rester dans le même dossier. Aucun serveur n'est nécessaire. Pour exécuter les assertions facultatives en environnement de développement disposant de Node.js : `node prototype/ui11/smoke.cjs` depuis la racine du dépôt. **Node.js n'est pas requis pour consulter le prototype.**

## Écrans manipulables

1. **Dashboard** : nombre de bibliothèques, plateformes connues (borne minimale si une source n'a pas de plateformes déclarées), états OFFLINE/STALE et inconnus, derniers snapshots, navigation.
2. **Bibliothèques** : quatre bibliothèques fictives (ONLINE, OFFLINE, INACCESSIBLE, UNKNOWN), plateformes, fraîcheur, dernier snapshot et dernier complet vérifié, historique si index accessible.
3. **Comparaison** : sélectionner bibliothèque, génération A/B, lancer comparaison en mémoire, six classifications, filtres par classification, plateforme et recherche, inspecteur d'unité. Les couvertures A/B restent visibles. Un `entries=[]` sous couverture insuffisante n'est jamais affiché comme « aucune différence ».
4. **Collection** : grille/liste de titres fictifs issus des derniers snapshots complets vérifiés, recherche, filtre bibliothèque, inspecteur sans médias réels ni édition.

**Clavier** : tabulation/focus visible, Entrée ou Espace sur les lignes de comparaison. Interface responsive. Le libellé « Non retrouvé dans B » signifie une absence d'observation datée, **jamais une suppression physique**.

## Données / intégration Dev 1

Les fixtures sont définies dans `fixtures.js`. Le navigateur ne peut pas exécuter PowerShell : `app.js` expose `window.RM_COMPARE(before, after)`, un **miroir JS strictement synthétique** du contrat de la PR Produit #26. Ce miroir n'est pas un nouveau moteur canonique ; **le module PowerShell et l'adaptateur de Dev 1 restent la référence**. La projection attend `libraryId, name, platforms, availability, freshness, observedAtUtc, indexAvailable, lastVerifiedCompleteRef, snapshots[]`. Chaque snapshot respecte la forme v1 : `schemaVersion, libraryId, generationId, capturedAtUtc, scanStatus, sourceAvailability, freshness, coverage[], units[]`. Les unités portent les IDs persistants `romUnitId/gameId/variantId/console`, un `observationDigest` optionnel et un `title` **strictement UI**. La couverture doit être vérifiée pour **A et B**, sans extrapoler console à plusieurs racines ou domaines ROMS/MEDIAS.

**Limites connues** : absence de persistance, de registre réel, de scanner, de lecteur de snapshots sur disque, de médias autorisés, de fiches enrichies et d'exécutable Windows. Aucune association USB, conversion, copie, suppression, export, appel réseau, lecture ROM/SSD/StateRoot ni Release. Les disponibilités sont des **fixtures de démonstration**, non une détection physique.

## Tests et preuves

Le fichier `smoke.cjs` contient des assertions sur les six classes, le refus des identités/entrées invalides, OFFLINE/STALE, les zones partial/failed vides, et la navigation simulée. Un contrôle en V8 avec DOM simulé a été réalisé lors de la PR (voir commentaire de réception/checkpoint) ; il **ne constitue pas un test navigateur graphique ni un PASS CI Windows**. Les tests Node.js et visuels doivent être exécutés indépendamment et leurs résultats datés.

### À raccorder avant l'application

- Adaptateur DEV1 de lecture synthétique et mapping des preuves `coverage` / `lastVerifiedCompleteRef`.
- Contrôle de parité entre `RM_COMPARE` et le module PowerShell 5.1 sur mêmes fixtures.
- Choix technique Windows et packaging portable **sur mandat ultérieur**, tests d'accessibilité et navigateur, puis vérification externe avant tout ZIP/Release.
