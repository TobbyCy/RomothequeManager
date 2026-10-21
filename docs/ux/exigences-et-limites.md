# Contrat de présentation UX / technique (non normatif)

**Ce document exprime des besoins d'interface, pas des DTO adoptés ni une implémentation.** La source de vérité d'une future projection de catalogue, d'un scan, d'un job, de préférences utilisateur et d'opérations doit être déterminée avant de coder les accès.

| Besoin UI | Source conceptuelle attendue | Comportement en absence de preuve |
| --- | --- | --- |
| Jeux, variantes, metadata et média | Projection liée à une génération de snapshot cohérente | Fiche limitée, champ unknown, jamais ID inventé |
| Statut du disque | Observation physique datée et liaison USB confirmée | Disponibilité unknown/non observée |
| Couverture et dernier essai | Snapshot complet/partial/failed, portée explicite | Ne pas conclure à une suppression |
| Fraîcheur selon l'usage | Date de source + politique future d'expiration | Lecture historique qualifiée, opération refusée |
| Diagnostics et jobs | Résultats/états de jobs indépendants de la vue | Fin de worker ≠ publication vérifiée |
| Favoris/notes/tags | Magasin personnel futur, séparé d'index dérivé | Édition désactivée si stockage non garanti |
| Plan d'export | Contrat futur de planification + permissions, source revalidée | UI preview seulement, aucune copie/CommitGate |

La première démonstration de navigation peut utiliser des fixtures et adaptateurs purement fictifs **sans** rendre un composant apte à lire ou écrire des bibliothèques réelles. Les maquettes n'autorisent aucune migration de StateRoot ou schéma de données.

**Répartition de qualité** : le développement de l'interface assure les contrôles de lisibilité et l'absence d'effet implicite ; le noyau/tests attestent la validité de lecture et les garde-fous ; la conformité des fixtures prouve uniquement les cas synthétiques. Le gate Release externalisé doit vérifier les assets réellement téléchargés.
