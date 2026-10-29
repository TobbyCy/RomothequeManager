# ROMothèque Manager V2 — maquettes de vision cible

Statut : **références UX historiques, non contractuelles**. Ces images illustrent la direction produit souhaitée par le propriétaire ; elles ne constituent ni des captures de l'application actuelle, ni des preuves de fonctionnalités livrées.

## Inventaire des 10 écrans distincts identifiés (28 octobre 2026)

| Identifiant | Écran | Référence |
| --- | --- | --- |
| UX-V2-01 | Tableau de bord général | Vue d'ensemble collection, bibliothèques, exports, alertes |
| UX-V2-02 | Collection | Grille, recherche, filtres, détail latéral |
| UX-V2-03 | Fiche de jeu | Présentation, variantes, médias, fichiers |
| UX-V2-04 | Bibliothèques et disques | Bibliothèques, supports, inventaires, snapshots |
| UX-V2-05 | Diagnostics | Anomalies, preuves, inspecteur, recommandations |
| UX-V2-06 | Export — sélection (1/4) | Plateformes, filtres, variantes, estimation |
| UX-V2-07 | Export — configuration (2/4) | Profils, destination, organisation, formats |
| UX-V2-08 | Export — simulation (3/4) | Opérations prévues, contrôles, blocages |
| UX-V2-09 | Export — validation (4/4) | Synthèse, rapport, validation sans exécution |
| UX-V2-10 | Export — variante exploratoire | Vue condensée sélection/simulation |

L'archive fournie le 29 octobre contient **dix JPEG, dix SHA-256 différents et dix écrans distincts**. L'index initial mentionnait onze transmissions, dont une répétition visuelle de la fiche Castlevania ; cette onzième transmission n'est pas incluse dans l'archive et sa duplication binaire reste **NOT_VERIFIED**.

## Principes de lecture

- **Style cible** : sombre, accents rouges, navigation claire, hiérarchie de l'information et états explicites. La maquette Diagnostics bleue est une référence fonctionnelle, **pas** une nouvelle palette officielle.
- **Fonctions futures** : les éléments dessinés (export, conversions, métadonnées, synchronisation, etc.) ne sont pas nécessairement implémentés.
- **Données fictives / illustratives** : compteurs, tailles, chemins, estimations et disponibilités ne doivent pas être traités comme des preuves.
- **Sécurité fonctionnelle** : ne jamais confondre indisponibilité, observation manquante et suppression ; distinguer doublon candidat et doublon confirmé ; ne jamais inventer une identité de jeu en absence de métadonnées.
- **Priorité** : les contrats et gates de livraison du projet priment sur l'imitation pixel-perfect des maquettes.

## Provenance et droits

Les maquettes ont été créées dans les échanges de conception avec le propriétaire, puis transmises à nouveau le 28 octobre 2026. Elles contiennent des illustrations, marques et visuels de jeux commerciaux utilisés comme **références de design**, sans preuve de droits de redistribution. **Ne pas publier les images dans le dépôt public avant revue des droits** ; préférer le dépôt privé de pilotage pour les originaux, ou des versions expurgées / synthétiques pour le dépôt public.

## Références et contrôles des originaux — 29 octobre 2026

Lot de référence : **UX-V2-2026-10-28**. Archive `Romotheque_Manager_V2_Maquettes_2026-10-28.zip` (4837639 octets), SHA-256 `1f058ce1da16125e46d2e0d22b338eb46e35b1533c3ca664a9eded08c377cc5e`.

Les originaux et leur manifeste sont conservés dans un espace à accès restreint, avec une page de consultation réservée aux personnes autorisées. Les références ci-dessous permettent d'identifier les fichiers sans republier leurs illustrations ni exposer les liens de cet espace. L'accès est communiqué dans le suivi privé du projet.

| ID | Fichier original | Dimensions | Octets | SHA-256 |
| --- | --- | --- | ---: | --- |
| UX-V2-01 | `01-dashboard.jpeg` | 1536 × 864 | 420161 | `d7df1ce5d318df884d3a4753b88bb59e7e0bed703d73291b058dd1913e5179df` |
| UX-V2-02 | `02-collection.jpeg` | 1536 × 864 | 539537 | `416170795034548eef7df5ec972f25e77eab862f5c73a8fd97e3b6e1fac961fd` |
| UX-V2-03 | `03-fiche-jeu.jpeg` | 1536 × 1024 | 502338 | `3b67a4ed03f94fe1b3b7a67e5e1f803f1abb20c20d9aea4c2aa5c85f4392d8bc` |
| UX-V2-04 | `04-bibliotheques.jpeg` | 1536 × 1024 | 493314 | `b4034c1c340fb0866d87ee0c58cd8c6fa017527be6c523cf1b3ca35ea3e6fdc5` |
| UX-V2-05 | `05-diagnostics.jpeg` | 1312 × 1199 | 499019 | `2d49d5b9862d6480c816e067c2fec4c8ff43a75a4d1682cbaec3ae9d8bd2ce31` |
| UX-V2-06 | `06-export-selection.jpeg` | 1536 × 1024 | 482779 | `34b87e170779f5a1f81d2b1da02e0421b237fd0d5fbd5313f263a68357896b21` |
| UX-V2-07 | `07-export-configuration.jpeg` | 1536 × 1024 | 445724 | `7f843f0c842f921a6383b32337509f5f059431fd293fda91d0ff317c4c071ecb` |
| UX-V2-08 | `08-export-simulation.jpeg` | 1536 × 1024 | 508998 | `b112d6be4e3871cd59a03f3a3ea465e40493e853c04a5135a61b045331ce8984` |
| UX-V2-09 | `09-export-validation.jpeg` | 1536 × 1024 | 515432 | `271e6b5ba24d6850f01b5089054dc92f87ee7c4b5e002999d69fdb193aca63ce` |
| UX-V2-10 | `10-export-variante.jpeg` | 1536 × 1024 | 491278 | `d15de9e4812e2a2bc440422f50519192589d1bef8a68aab7cf3de9f2e3b8a3f7` |

Contrôles réalisés : extraction limitée aux dix chemins attendus, décodage JPEG 10/10, inspection visuelle 10/10 et correspondance à l'index, absence de doublon binaire, relevé des dimensions et empreintes. Les dix fichiers relus depuis GitHub sont **identiques octet pour octet** aux fichiers de l'archive. Aucun fichier n'a été redessiné, converti ni redimensionné.

La vérification des octets accessibles et l'inspection locale sont acquises. Le rendu de la page dans un navigateur authentifié demeure **NOT_VERIFIED**. Le dépôt des fichiers sur une branche soumise à revue n'implique pas leur fusion dans une branche principale.

## État d'intégration

- [x] Inventaire des dix vues et cadrage de la vision cible.
- [x] Dix originaux transférés dans GitHub, à accès restreint, sans image commerciale ajoutée ici.
- [x] Dimensions, SHA-256, décodage et correspondance visuelle vérifiés.
- [x] Manifeste, chemins de consultation et relecture des dix fichiers GitHub vérifiés dans le suivi à accès restreint.
- [ ] Droits de redistribution des illustrations tierces établis : **NOT_ESTABLISHED**, aucune diffusion de ces images autorisée par ce lot.
- [ ] Rendu de la page de consultation dans un navigateur authentifié : **NOT_VERIFIED**.

**UX_V2_MOCKUPS_INTEGRATED_AND_VERIFIED** qualifie le dépôt conservatoire et le contrôle des dix originaux. Il ne qualifie ni leur publication, ni une fusion, ni une conformité visuelle de l'application. Aucun changement Avalonia, scan réel ou Release.
