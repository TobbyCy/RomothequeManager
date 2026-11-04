# Expérience utilisateur — vues intégrées et conception future

**Point NEXT-05 du 02.11.2026 :** les spécifications UX ci-dessous comprennent à la fois des parcours *intégrés et testés sur fixtures* et des **propositions non implémentées**. Ne pas confondre ces documents avec l'Alpha [v0.4.0-alpha.1](../../README.md#télécharger-et-essayer) ni avec une recette humaine. La PR [#154](https://github.com/TobbyCy/RomothequeManager/pull/154) reste DRAFT, sa CI Windows non-admin est rouge et le candidat bêta n'est pas publié.

| Vue ou parcours | Code NEXT-05 intégré sur fixtures | Maquettes/fonctions futures à ne pas annoncer comme disponibles |
| --- | --- | --- |
| [Dashboard](01-tableau-de-bord.md) | Accueil, états synthétiques et résumés bornés | Toutes les statistiques enrichies ou dynamiques du concept |
| [Collection](02-collection.md) | Liste/grille, filtre, recherche, pagination, 50 cartes ; affichage historique ES-DE et médias autorisés ; [B-05 corrigé](https://github.com/TobbyCy/RomothequeManager/pull/151) | Les **quatre modes**, inspecteur redimensionnable, tags/favoris persistants, matching implicite |
| [Fiche jeu](03-fiche-jeu.md) | Navigation et média lié à une référence validée dans les fixtures | Variantes complètes, gestion avancée des associations et export |
| [Bibliothèques et snapshots](04-bibliotheques.md) | Inventaire et snapshots de test, `Offline/Stale`, conservation `CurrentComplete` | Comparateur deux générations complet, réassociation USB automatique |
| [Assistant export](05-assistant-export.md) | Spécification seulement | Export, déplacement, copie/réorganisation physique **non autorisés** |
| [Diagnostics](06-diagnostics.md) | Codes neutres/états des opérations intégrées | Centre d'analyse exhaustif avec toutes les actions prévues |

**Nouveau guide de vérité opérationnelle sur fixtures :** [NEXT-05 / parcours IDEAL, médias, état et consentement](next05-ideal-parcours.md). [Ancienne décision MVVM NEXT-03](next03-mvvm-foundation.md) est **HISTORIQUE**, non le plan d'intégration courant.

Les spécifications (modes avancés, navigation finale, assets non disponibles) restent consultables dans [charte visuelle](charte-visuelle.md), [critères de recette](criteres-de-reception.md), [limites fonctionnelles](exigences-et-limites.md) et [illustrations non distribuées](illustrations-non-distribuees.md). Les logos officiels ont des droits distincts du code ; les icônes de consoles #120 ne sont pas intégrées.

**Invariants d'interface :** disponibilité physique, état d'inventaire, couverture et fraîcheur sont **quatre dimensions distinctes**. `Offline`, `Stale`, `Partial` ou `Unknown` ne prouvent aucune suppression. Une jaquette non autorisée se présente sous forme de placeholder, jamais comme une association inventée. Les annulations et changements de bibliothèque écartent les résultats tardifs. La preuve de focus et cadrage Windows 760×480 du lot B est **sur fixtures**, pas une recette humaine générale.

[Manuel de parcours prudent](../user/parcours-ideal-next05.md) · [Sécurité](../user/securite-et-limites.md) · [État produit](../../STATUS.md). Aucune capture fictive ou maquette ne vaut validation d'un ZIP IDEAL autonome sous compte non-admin.
