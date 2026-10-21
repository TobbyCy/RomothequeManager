# Sécurité, limitations et bonnes pratiques

**ROMothèque Manager est actuellement en développement et ne fournit aucune application de production.** Les écrans décrits sont des maquettes, et les contrats d'opération physique ne sont pas autorisés.

## Ce que signifie « hors ligne »

Une bibliothèque déconnectée peut toujours posséder un ancien catalogue cohérent, **si son index est encore accessible**. Un disque OFFLINE ne signifie pas que ses jeux sont supprimés. Si l'index lui-même était sur le support absent, l'application ne peut pas promettre de consultation. Un résultat de scan PARTIAL, FAILED ou STALE conserve ses limites et ne transforme jamais les zones inconnues en « zéro élément ».

## Protection des collections

- Ne jamais utiliser de véritables ROM, BIOS, XML ES-DE, StateRoot ou captures de bibliothèques privées pour les tests GitHub.
- La consultation d'un snapshot ne valide pas automatiquement la présence actuelle des fichiers.
- La réassociation d'un support USB doit nécessiter des preuves et, selon la politique retenue, une confirmation ; une lettre de lecteur n'est pas une identité.
- Un favori ou une annotation utilisateur doit rester séparé d'un index reconstructible dans une future version. Cette capacité n'est pas encore livrée.
- Un rapport PASS de tests synthétiques ne permet pas d'en déduire une autorisation d'export réel, de conversion, de suppression ou de scraping.

## Téléchargement futur

Lorsque les Releases existeront, utiliser la page GitHub Release officielle, vérifier le nom/version de l'asset et son SHA-256, puis lire les notes de limitations. Le ZIP Windows self-contained devra faire l'objet d'un test sur un PC sans runtime .NET installé séparément. **Aucun tel ZIP n'est actuellement disponible** : ne cherchez pas un installateur dans ce dépôt.

Pour une question, consulter [SUPPORT](../../SUPPORT.md) ; pour une vulnérabilité, consulter [SECURITY](../../SECURITY.md).
