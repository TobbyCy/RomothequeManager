# Sécurité, limitations et bonnes pratiques

**Référence au 29 octobre 2026 : [v0.3.0-alpha.1 / NEXT-03](https://github.com/TobbyCy/RomothequeManager/releases/tag/v0.3.0-alpha.1).** Une application Windows portable est disponible. Elle reste une Alpha de développement et de démonstration, sans usage en production ni opération physique sur les ROMs.

## Ce qui est disponible et ce qui reste limité

- L'interface permet de consulter les démonstrations synthétiques et d'essayer un import ES-DE volontaire en lecture seule sur une disposition compatible. La compatibilité avec une bibliothèque personnelle n'est pas validée.
- Le service d'inventaire du binaire public refuse le scan personnel. Les essais du vrai service avec les fixtures utilisent une compilation CI distincte.
- Le catalogue importé est conservé en mémoire pendant la session ; aucune persistance durable du catalogue, des favoris ou des annotations n'est livrée.
- Les vues et maquettes décrivant des capacités futures ne les rendent pas opérationnelles. NEXT-04 reste en développement.
- Logo pixélisé, défilement perdu au retour, import réel non validé et présentation ultralarge perfectible restent des réserves connues de l'Alpha.

## Ce que signifie « hors ligne »

Une session déjà ouverte peut conserver un snapshot en mémoire lorsqu'un support devient indisponible. Cela ne garantit pas une consultation après fermeture de l'application : le catalogue persistant reste à développer. Un disque OFFLINE ne signifie pas que ses jeux sont supprimés. Un résultat PARTIAL, FAILED ou STALE conserve ses limites et ne transforme jamais les zones inconnues en « zéro élément ». Les médias sont revérifiés à l'ouverture et peuvent devenir inaccessibles.

## Protection des collections

- Ne jamais utiliser de véritables ROM, BIOS, XML ES-DE, StateRoot ou captures de bibliothèques privées pour les tests et rapports GitHub.
- La consultation d'un snapshot ne valide pas automatiquement la présence actuelle des fichiers.
- Une lettre de lecteur n'est pas une identité. Le code dispose d'une réassociation ES-DE vérifiée au sein d'une session, sans garantie de reconnexion automatique ou de persistance après redémarrage.
- Un favori ou une annotation utilisateur devra rester séparé d'un index reconstructible dans une future version. Cette capacité n'est pas encore livrée.
- Un rapport PASS de tests synthétiques ne permet pas d'en déduire une autorisation d'export réel, de conversion, de suppression ou de scraping.
- Aucun scan personnel automatique, déplacement, effacement ou réorganisation de ROMs n'est autorisé dans cette Alpha.

## Télécharger et démarrer

Suivre les [instructions du README](../../README.md#télécharger-et-démarrer) pour télécharger le ZIP Windows x64 et son SHA-256 depuis la Pre-release officielle. Extraire l'archive entièrement, conserver DLL, données et licences ensemble, puis lancer `RomothequeManager.V2.exe` sans droits administrateur. Le runtime .NET est inclus.

Le binaire n'est pas signé ; SmartScreen peut avertir. Ne pas désactiver les protections globalement. Aucun installateur ni système de mise à jour automatique n'est livré. Le ZIP autonome ne garantit pas encore que les paramètres et catalogues soient stockés sur le même SSD que l'application.

Pour une question, consulter [SUPPORT](../../SUPPORT.md) ; pour une vulnérabilité, consulter [SECURITY](../../SECURITY.md).
