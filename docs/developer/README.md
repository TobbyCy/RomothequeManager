# Guide de développement — ROMothèque Manager

**Ce guide décrit un code en cours d'import dans un dépôt indépendant.** Les commandes exactes doivent être revues lorsque le responsable technique a finalisé le transfert et la nouvelle CI.

## Environnement

- **Moteur historique** : Windows PowerShell **5.1 Desktop**, en raison de ses dépendances et tests dédiés.
- **Tests métier** : Pester 4.10.1 verrouillé/contrôlé, fixtures synthétiques exclusivement ; la version de Pester installée et sa signature doivent être confirmées avant exécution.
- **Version V2** : hypothèses .NET/Avalonia documentées, **pas de projet GUI à compiler pour l'instant** ; aucune réussite .NET ou Release ne peut être inférée de ce guide.
- **Source Git** : ce dépôt possède un historique indépendant ; ne pas importer le Git ou les références d'un ancien projet privé.

## Méthode

Ouvrir [CONTRIBUTING](../../CONTRIBUTING.md), choisir un ticket Produit existant, vérifier main/branche de départ, isoler les modifications, exécuter seulement les tests synthétiques réellement disponibles, consigner SHA, cas et limites, créer Draft PR et attendre revue. Toute écriture de code doit préserver la politique des identifiants et le chemin de données réelles interdit.

## Index technique

- [Moteur existant](../architecture/moteur-actuel.md) ; [Contrat historique](../architecture/contrat-moteur-historique.md)
- [Pester et canaris](pester-execution.md) ; [Couverture](pester-coverage.md) ; [Protocole de référence](coverage-reference.md)
- [Préflight synthétique](preflight-synthetique.md) ; [CI adaptative](ci-adaptative.md) ; [Workflow contribution](workflow.md)
- [Sécurité](../../SECURITY.md) et [statut produit](../product-status.md)

## Interdictions de données

Pas de ROM, BIOS, SSD de bibliothèque réelle, chemins personnalisés, XML ES-DE réel, StateRoot, token, nom d'utilisateur, machine ou pièce jointe privée en fixtures, tests, logs ou artefacts CI. Tester les refus et les inconnus, pas seulement le chemin nominal.
