# Roadmap fonctionnelle et technique

Ce document présente **des directions de développement**, pas un calendrier de livraison ni une promesse commerciale. Le statut de chaque capacité se contrôle dans [État produit](product-status.md).

| Séquence envisagée | Valeur utilisateur | Critère de sortie avant d'annoncer « livré » |
| --- | --- | --- |
| Base moteur importée de façon fiable | Préserver identités, inventaires et tests | Intégrité des fichiers, suites réelles, garde-fous et packaging du dépôt neuf |
| POC Windows portable en lecture synthétique | Voir Dashboard, Collection, fiche et statuts Offline/Stale sur faux catalogue | Exécutable testé, CI .NET non vide, ZIP self-contained, SHA-256, essai externe |
| Bibliothèques et snapshots V2 | Distinguer support, scan et fraîcheur | Sources de vérité et contrats validés, scénarios d'échec/partial testés |
| Recherche/favoris/collections | Naviguer sans perdre annotations | Stockage personnel séparé d'un cache, sauvegarde/restauration démontrées |
| Préparation et simulation d'export | Comprendre variantes, dépendances, espace et refus | Plans vérifiables sur fixtures, aucune copie réelle implicite |
| Opérations physiques ultérieures | Agir seulement avec consentement et vérification | Contrats, permissions, tests et autorisation distincte ; **non autorisées aujourd'hui** |
| SteamOS / Linux natif éventuel | Usage portable au-delà de Windows | Matériel et distribution natifs réellement testés, pas de promesse de compatibilité |

Les demandes publiques existantes décrivent des besoins et peuvent rester à l'étude longtemps. Une Issue ouverte, une maquette validée ou une CI verte sur synthétique ne vaut pas une Release.
