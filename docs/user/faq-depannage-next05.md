# FAQ et dépannage — ROMothèque Manager NEXT-05

**Portée au 02.11.2026 :** l'Alpha [v0.4.0-alpha.1](../../README.md#télécharger-et-essayer) est téléchargeable ; NEXT-05 est intégré dans `main` **sur fixtures** ; le ZIP bêta #154 **n'est pas publié** et le parcours Windows dédié reste `NOT_EXECUTED`. Cette FAQ ne remplace pas un accord d'accès aux données réelles.

## Quelle version dois-je utiliser ?

La **seule Alpha la plus récente effectivement publiée** est [v0.4.0-alpha.1](https://github.com/TobbyCy/RomothequeManager/releases/tag/v0.4.0-alpha.1). Ne pas confondre une archive CI, un ancien ZIP POC, un commit GitHub ou la PR #154 avec un téléchargement public. L'Alpha contient l'import historique ES-DE en lecture seule, **pas** l'inventaire autonome ROMS/MEDIAS NEXT-05 ni SQLite.

## Pourquoi mon catalogue a-t-il disparu après fermeture ?

Dans la Release v0.4.0, le catalogue ES-DE est **en mémoire** : il faut refaire l'import à la session suivante. Les sauvegardes SQLite minimales sont développées et testées avec des fixtures dans `main`, **pas disponibles dans cette Alpha**. Ne pas créer ni copier une base personnelle en suivant un guide de test.

## Pourquoi une jaquette n'apparaît-elle pas ?

Une référence XML peut être absente, non autorisée, pointer hors de la racine, désigner un média absent/corrompu ou dépasser les limites PNG/JPEG. Le résultat correct est un **placeholder**, pas un téléchargement automatique. Dans l'Alpha, une référence exploitable dans le XML est nécessaire ; l'index MEDIAS indépendant des XML appartient au code NEXT-05 testé sur fixtures. Aucun scraper ni attribution automatique de jaquette n'est prévu.

## Une erreur `ROM_MISSING` signifie-t-elle que mon jeu est effacé ?

**Non.** Dans l'import historique, elle signifie que **le chemin exact déclaré** par ES-DE n'a pas été retrouvé dans la portée autorisée. La ROM peut avoir un autre nom ou emplacement ; le programme n'effectue pas de recherche/renommage/rapprochement général pour conclure. Ne pas déplacer ni supprimer un fichier en se basant sur ce seul diagnostic.

## `Offline`, `Stale`, `Partial` et `Unknown` indiquent-ils zéro jeu ?

**Non.** `Offline` concerne la disponibilité, `Stale` la fraîcheur, `Partial` la couverture et `Unknown` l'absence de preuve. La dernière tentative `LastAttempt` ne doit pas écraser `CurrentComplete` lorsque ses garanties sont insuffisantes. Un écran historique conservé ne prouve pas que le support est encore connecté.

## L'annulation stoppe-t-elle immédiatement la lecture du disque ?

Pas toujours : un parseur ou un appel système déjà lancé peut finir son travail. Le contrat testé est que le résultat annulé, tardif ou appartenant à une autre bibliothèque **ne remplace pas l'état accepté**. Un changement de sélection révoque aussi les références médias de l'ancienne sélection.

## Pourquoi la sauvegarde SQLite n'est-elle pas accessible dans le ZIP public ?

La persistance PERSIST-01 (snapshots, décisions manuelles, backup/restore) a été développée dans le **backend sous fixtures synthétiques**. La Release Alpha est antérieure et n'embarque pas ce parcours. Le stockage personnel reste refusé ; pas de mode portable activé ni de rattachement USB automatique.

## Puis-je tester le ZIP bêta #154 ?

**Aucun ZIP bêta public n'existe actuellement.** Le ZIP de validation est produit en CI, contrôlé puis **quarantainé sans téléversement**. Sa provenance doit être établie sur les octets exacts avant une recette dédiée autorisée. Les runs CI ne valent pas autorisation d'utiliser des fichiers personnels.

## Pourquoi la CI de #154 est-elle rouge alors que SQLite et DocFX passent ?

Le [run Windows #41482716933](https://github.com/TobbyCy/RomothequeManager/actions/runs/41482716933) prouve des éléments **séparés** : deux builds ZIP identiques sur le checkout testé, notices, probe SQLite natif sous compte non-admin, vérifications de bureau Windows et couverture DocFX **860/938, 3/3 nouvelles API**. Mais le vrai EXE extrait n'a pas produit l'oracle `MainWindow.Opened` sous le worker standard en **90 secondes**. La cause est **non démontrée**. Le job `.NET` et `ci-policy-gate` échouent ; il est interdit de substituer un lancement administrateur ou un mini-hôte Avalonia à l'application exacte.

## Comment préparer un rapport utile sans exposer de données ?

Indiquer uniquement la **Release ou le SHA**, le **run CI**, le code diagnostic neutre, des étapes reproductibles **sur fixtures inventées**, les dimensions d'affichage et les statuts `PASS`, `FAIL` ou `NOT_EXECUTED`. Ne pas partager de ROM, BIOS, XML, médias tiers, capture privée, chemin utilisateur complet, profil, token ou log brut sensible.

## Que faire si Windows avertit que l'application n'est pas signée ?

La Release Alpha est **non signée**. Comparer le SHA fourni sur la [page de Release](https://github.com/TobbyCy/RomothequeManager/releases/tag/v0.4.0-alpha.1), conserver le dossier extrait et les notices. **Ne désactiver aucune protection Windows** pour contourner un avertissement ou faire passer une recette.

## Quels contrôles restent bloquants ?

Le [gate #154](https://github.com/TobbyCy/RomothequeManager/pull/154) et la [recette Windows #95](https://github.com/TobbyCy/RomothequeManager/issues/95) doivent encore démontrer le fonctionnement du **vrai ZIP candidat** sous compte standard non-admin, GUI et parcours ES-DE/SQLite métier sur fixtures, avec un SHA de provenance contrôlé. Sans leur validation et un **GO propriétaire de Release**, pas de bêta publiée.

[Retour au manuel IDEAL](parcours-ideal-next05.md) · [Sécurité](securite-et-limites.md) · [État produit](../../STATUS.md) · [Support](../../SUPPORT.md).
