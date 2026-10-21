# ADR-0004 — Documentation synchronisée aux évolutions

**État historique : ACCEPTED, modalités CI à revalider sur la version du dépôt réellement testée.**

## Contexte

Sans règles de mise à jour, la roadmap et les guides peuvent annoncer des fonctionnalités absentes du code ou omettre des changements cassants.

## Décision

Maintenir le **statut produit** pour les capacités effectivement testées, le **CHANGELOG** pour les changements de version, la **roadmap** pour l'intention et les guides d'architecture/CI pour les conventions. Chaque PR de fonctionnalité/Export/Preflight doit traiter explicitement l'impact documentaire. Les changements de tests/CI mettent à jour au minimum les instructions de validation et la traçabilité, selon la politique de chemins adoptée.

## Conséquences

Un gate docs-sync est utile mais doit être **réadapté et testé** sur la branche et le graphe Git effectivement concernés ; on ne présume pas que les anciens SHA/paths sont encore valides. La présence d'un guide ne vaut jamais preuve de tests exécutés ou de Release publiée.
