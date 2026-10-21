# Workflow Git et revue — contributeurs

1. Choisir une Issue **Produit** existante avec résultat attendu et contraintes ; les documents de discussion non adoptés ne donnent pas d'autorisation de travail physique.
2. Vérifier main et le SHA de base ; branche isolée pour chaque sujet. Ne pas importer de refs/branches externes d'un autre dépôt.
3. Commit régulier, diff minimal, tests synthétiques sur les véritables fichiers modifiés, canaris de refus.
4. PR Draft avec références **Produit seulement**, base/HEAD et checkout CI distingués ; expliquer si GitHub Actions exécute une fusion temporaire et démontrer les arbres si possible.
5. Revue indépendante par compte réellement disponible et gate CI réellement exécuté. **Aucun label, badge ou statut de Project ne donne un GO à lui seul.**
6. Fusion seulement par le mainteneur autorisé, puis vérification du run applicable sur main, mise à jour du changelog/statut et fermeture justifiée de l'Issue terminée.
7. Ne pas utiliser de données réelles, secrets, assets sans droits ni lien vers une zone privée.

**Dépôt produit actuel : staging privé ; aucune protection main ni règles de contribution publique ne peuvent être réputées actives sans preuve API/UI.** Voir [CONTRIBUTING](../../CONTRIBUTING.md), [tests Pester](pester-execution.md) et [sécurité](../../SECURITY.md).
