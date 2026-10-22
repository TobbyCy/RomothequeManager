# Sécurité — signalement responsable

**Ne publiez aucun secret, vulnérabilité exploitable, preuve brute, log sensible, chemin personnel ou donnée de collection réelle dans une Issue, une PR ou une Discussion.**

## Contacter le responsable

Le dépôt source est désormais **public**. L'activation de **Private vulnerability reporting** a été signalée lors de la publication ; son accessibilité effective par un utilisateur extérieur **n'a pas été confirmée indépendamment dans cette revue**.

Pour signaler une vulnérabilité, privilégiez dans l'onglet **Security → Advisories** l'action GitHub **Report a vulnerability**, lorsqu'elle est effectivement proposée avec un formulaire privé. Ne publiez **jamais** de preuve de vulnérabilité, code d'exploitation, secret, journal ou donnée réelle dans une Issue, une PR ou une Discussion publique.

Si cette option privée n'est pas accessible, utilisez au maximum une Issue **sans détail technique sensible** pour demander au mainteneur un moyen de contact privé. N'y joignez aucune information exploitable ; transmettez le dossier complet uniquement après confirmation d'un canal confidentiel.

Un signalement de sécurité ne doit pas être classé comme bug public reproductible. Ni les Issues ni les PR ne constituent un coffre-fort.

## Périmètre et données

Les seules données destinées aux tests de ce dépôt sont synthétiques. Ne joignez pas de ROM/BIOS, sauvegarde, XML ES-DE réel, StateRoot, log de disque ou identifiant matériel. Aucun contrôle CI n'autorise une exécution sur des données utilisateur. Une faille concernant une future fonctionnalité non implémentée doit être qualifiée **hypothétique**, pas présentée comme exploit observé.

## Publication et correctifs

Le mainteneur coordonnera la réponse, la revue, le correctif, les tests et une publication adaptée lorsqu'ils seront réellement disponibles. **Aucune garantie de délai de réponse n'est donnée ici.** L'accessibilité du canal privé à un contributeur extérieur, les versions supportées et la configuration effective de GitHub Security Advisories restent à vérifier par le mainteneur. La publication du code n'est pas une garantie de sécurité ni de support.
