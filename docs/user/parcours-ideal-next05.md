# Parcours IDEAL NEXT-05 — lecture prudente des preuves

**Guide de compréhension et de recette sur fixtures, pas mode d'emploi d'une Release bêta.** État au 02.11.2026 : le code [NEXT-05 dans `main`](../../STATUS.md) a des tests automatisés, mais [v0.4.0-alpha.1](../../README.md#télécharger-et-essayer) n'intègre pas ces nouveautés. Le [candidat #154](https://github.com/TobbyCy/RomothequeManager/pull/154) n'est pas distribué et sa recette Windows non-admin #95 reste **NOT_EXECUTED**. Aucun accès à une collection personnelle n'est autorisé par cette page.

## 1. Préparer uniquement une bibliothèque de test

Une fixture **IDEAL** reproduit une bibliothèque volontairement organisée : racine logique ROMS avec fichiers **fictifs non exécutables**, racine MEDIAS distincte avec images de test PNG/JPEG autorisées, et éventuellement un export historique ES-DE XML. Une fixture peut aussi simuler un support Offline/Stale ou des métadonnées manquantes, sans brancher un vrai SSD utilisateur.

L'**import ES-DE** apporte ses métadonnées historiques ; il ne représente **pas** un scraper ni une dépendance permanente de l'inventaire autonome. Les fichiers référencés par XML doivent être considérés comme des références **déclarées**, non comme une preuve de présence ou d'autorisation. Aucun déplacement de fichiers ni téléchargement n'est prévu.

## 2. Sélection, consentement et import historique ES-DE

Dans le parcours autorisé sur fixtures, le choix d'un dossier est distinct d'une **confirmation positive de lecture seule**. Il n'y a **pas d'import automatique au lancement**. La VM demande au service injectable l'import, puis n'accepte un snapshot `Current` qu'après contrôle de session/sélection ; la tentative `LastAttempt` garde son résultat propre. Un XML invalide, un refresh annulé ou un résultat tardif ne remplace pas silencieusement le dernier catalogue accepté.

Si une fixture ne possède **aucun XML**, le système d'inventaire autonome ne doit **pas** inventer le contenu ES-DE ; les diagnostics distinguent le connu de l'inconnu. Sur la **Release publique** l'Alpha, l'import ES-DE reste **en mémoire seulement**.

## 3. Inventaire ROMS et états affichés

Sur le backend/les vues de test NEXT-05, l'utilisateur doit **sélectionner la racine ROMS explicitement** et consentir à l'opération appropriée. Une permission MEDIAS ou PREVIEW n'est jamais déduite du choix ROMS. La progression peut être indéterminée ; l'UI ne doit pas inventer des temps ou pourcentages.

| Résultat affiché | Interprétation correcte |
| --- | --- |
| `Complete` / `CurrentComplete` | Résultat complet pour **la portée observée** ; ne garantit pas le support présent demain |
| `Partial` | Une partie seulement est vérifiée ; les éléments non observés sont **inconnus**, pas supprimés |
| `Failed` | Dernière tentative échouée, sans écraser le dernier état cohérent |
| `Offline` | Support déclaré indisponible ; ne signifie pas bibliothèque vide |
| `Stale` | Projection conservée mais non fraîche ; nouvelle preuve nécessaire avant toute décision |
| `Cancelled` | La tentative ne publie pas un nouveau `Current` ; elle peut laisser achever des I/O non interrompables et en écarter le résultat |

Lorsqu'une sélection change d'A vers B, les vues ne doivent pas afficher les médias A sous B. Les codes de diagnostic et dates ne sont pas des preuves de suppression, d'intégrité physique ou de fusion automatique des jeux.

## 4. Index MEDIAS, jaquettes et aperçu

La voie **MEDIAS autonome** indexe les images de la racine de test consentie sans dépendre d'un XML ES-DE permanent. La voie **historique ES-DE** n'ouvre que les médias **réellement déclarés** pour le jeu dans le snapshot accepté, via `EsDeImportSession.TryOpenMedia`. Les deux voies ne doivent jamais s'accorder des permissions l'une à l'autre.

Les aperçus PNG/JPEG sont soumis aux tailles et budgets #141, à une consommation native sérielle et à la durée de vie du contexte courant. Une jaquette absente, corrompue, hors budget, non déclarée, interdite ou provenant d'un ancien snapshot affiche un **placeholder neutre** ; aucune jaquette n'est attribuée automatiquement, aucun scraper réseau n'est lancé. Les bitmaps transférés à la View doivent être détachés et libérés à la navigation/fermeture.

La Collection pagine **au plus 50 cartes**. La preuve Windows [B-05](https://github.com/TobbyCy/RomothequeManager/pull/151) contrôle notamment une Collection de 50 cartes en **760×480** et 1120×720, avec focus et navigation **sur fixtures**, pas sur tous les postes.

## 5. SQLite : état durable de test, pas stockage personnel

Le backend SQLite **PERSIST-01** peut conserver snapshots, provenance des métadonnées et décisions **manuelles** dans une fixture jetable. Une tentative `Partial`, `Failed`, `Offline` ou `Stale` conserve `CurrentComplete` au lieu d'en faire un inventaire nul. `LoadLastKnown` peut restaurer la représentation **Offline/Stale** sans accéder aux ROMS/MEDIAS, et les backups/restaurations contrôlés ne prouvent pas l'état physique actuel des fichiers.

**Limite importante :** cette capacité n'est **pas dans l'Alpha** et n'autorise ni base SQLite personnelle, ni sauvegarde portable automatique sur SSD, ni réassociation USB par lettre de lecteur. La restauration métier à partir de l'**EXE candidat #154 extrait sous compte standard** est **NOT_EXECUTED**. Une preuve du chargement natif de `e_sqlite3.dll` ne vaut pas cette recette.

## 6. Annuler, débrancher, revenir

- **Annuler un import/inventaire** : vérifier que l'ancien état accepté reste disponible, que la dernière tentative est qualifiée et qu'un résultat tardif n'est pas appliqué.
- **Débrancher un support de fixture** : conserver le snapshot pertinent si disponible, afficher `Offline/Stale` plutôt qu'un compteur zéro ; aucune reconnection automatique n'est promise.
- **Changer de bibliothèque A → B** : annuler/révoquer les chargements médias A en vol ; les cartes et previews ne doivent pas fuiter vers B.
- **Fermer puis rouvrir** : le catalogue ES-DE de l'Alpha est **perdu** (mémoire). La persistance SQLite de test NEXT-05 ne devient pas un droit utilisateur ni une fonction de l'Alpha.

## 7. Réception et limites

La validation officielle [Windows dédiée #95](https://github.com/TobbyCy/RomothequeManager/issues/95#issuecomment-6582880001) doit encore contrôler sous compte **standard non-admin**, à partir d'un **ZIP candidat authentifiable** et de **fixtures synthétiques** : lancement réel Avalonia, consentement, import ES-DE, inventaire, médias, refus hors racine, annulation et restauration SQLite métier. **Aucune de ces étapes ne peut être cochée à partir du seul ZIP CI quarantainé, des tests .NET ou d'une démonstration Avalonia minimale**. Pas de Release sans gates et GO propriétaire distinct.

Pour les problèmes connus : [FAQ et dépannage](faq-depannage-next05.md), [sécurité](securite-et-limites.md), [lexique](lexique.md), [état actuel](../../STATUS.md) et [UX IDEAL](../ux/next05-ideal-parcours.md).
