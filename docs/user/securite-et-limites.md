# Sécurité, limitations et bonnes pratiques

**État au 02.11.2026.** Il faut distinguer **l'Alpha publiée**, le **code NEXT-05 testé avec des jeux fictifs** et le **ZIP bêta IDEAL encore en validation**. Ce document ne donne aucune permission d'utiliser des ROM, BIOS, XML privés, jaquettes commerciales ou bibliothèques personnelles dans les tests du projet.

## Ce qui est réellement téléchargeable

La dernière version publiée est [**v0.4.0-alpha.1 / NEXT-04.1**](https://github.com/TobbyCy/RomothequeManager/releases/tag/v0.4.0-alpha.1), ZIP Windows x64 portable du 30.10.2026. Elle propose l'interface, la recherche/les fiches et un **import historique ES-DE explicite et en lecture seule**. L'import est **en mémoire** : un redémarrage exige un nouvel import. Les jaquettes ne sont accessibles que par les références médias du XML, lorsqu'elles sont acceptées.

Cette Alpha **n'intègre pas** la lecture autonome NEXT-05, l'index local MEDIAS indépendant des XML, la sauvegarde/restauration SQLite ou le nouvel EXE bêta. Ne pas lui appliquer la couverture DocFX, les CI et les parcours expérimentaux de `main`. Un ancien essai humain sur une version antérieure ne certifie pas le candidat NEXT-05.

[Instructions de téléchargement et SHA-256](../../README.md#télécharger-et-essayer) · [État vérifié](../../STATUS.md).

## NEXT-05 : capacités techniques limitées aux fixtures

Le code `main` intègre désormais **sur fixtures synthétiques uniquement** l'inventaire ROMS, l'index MEDIAS, des écrans/VM, une persistance SQLite minimale et des contrôles `Offline/Stale`, annulation et consentement. Le candidat [#154](https://github.com/TobbyCy/RomothequeManager/pull/154) n'est **pas publié** : son lancement GUI non-admin depuis le vrai ZIP extrait n'est pas validé, et la recette dédiée [#95](https://github.com/TobbyCy/RomothequeManager/issues/95) n'est **pas exécutée**.

Les frontières suivantes sont obligatoires :
- **Choisir ≠ consentir ≠ lire** : une racine ou un jeu affiché n'ouvre pas automatiquement de droits ROMS, MEDIAS ou PREVIEW. Un refus d'ACL, un support déplacé ou un chemin hors racine se traduit par refus/diagnostic, jamais par élargissement implicite.
- **La lecture ES-DE est historique** : le XML peut être absent, incomplet ou erroné ; ne pas dériver une suppression ou une attribution de jaquette d'une référence manquante. Les visuels autorisés sont PNG/JPEG bornés et disposent de placeholders neutres en cas de refus.
- **Les états restent orthogonaux** : `OFFLINE` = support explicitement indisponible, `STALE` = information non fraîche, `PARTIAL` = couverture incomplète et `UNKNOWN` = absence de preuve. Aucun ne signifie « zéro jeu », « supprimé » ou « support définitivement perdu ». Le dernier snapshot `CurrentComplete` demeure distinct de la tentative `LastAttempt`.
- **Annulation** : une opération synchrone déjà démarrée n'est pas toujours interrompable physiquement. L'application doit au minimum empêcher un résultat tardif annulé/obsolète de remplacer l'état accepté ; la fermeture doit libérer flux et bitmaps.
- **SQLite** : sur fixtures, une base peut sauvegarder les états, décisions manuelles et backups ; l'ouverture historique `Offline/Stale` n'est pas une confirmation de présence du support. Le stockage **personnel** reste refusé, le mode portable n'est pas activé implicitement et aucune lettre USB ne réassocie seule une bibliothèque.

Les fonctions de scraper, attribution automatique, export/copie/réorganisation de ROM, effacement, gestion des BIOS et synchronisation réseau ne sont **ni livrées ni autorisées** dans le parcours NEXT-05 actuel.

## Téléchargement, intégrité et protection du poste

Télécharger uniquement depuis les [Releases officielles](https://github.com/TobbyCy/RomothequeManager/releases) ; vérifier le fichier `.sha256` avec `Get-FileHash`, extraire **tous les fichiers** et conserver `licenses/`/notices avec l'application. Le ZIP public contient le runtime .NET ; aucun SDK requis. **Un SHA-256 n'est pas une signature d'éditeur** : le binaire n'est pas signé et Windows SmartScreen peut avertir. Ne pas désactiver SmartScreen, Defender, les ACL ou le pare-feu pour obtenir un PASS.

Le **ZIP CI de #154** est une archive de validation quarantainée et non téléversée ; son hash probatoire ne prouve **pas** l'authenticité d'un autre ZIP reconstruit ni l'existence d'un téléchargement bêta. Les procédures techniques réservées à l'équipe sont [sur #95](https://github.com/TobbyCy/RomothequeManager/issues/95#issuecomment-6582880001) et ne constituent pas un assistant d'installation public.

## Préserver sa collection et demander de l'aide

Tester les évolutions NEXT-05 **avec des données inventées** sur une machine et un compte dédiés uniquement si une autorisation précise existe. Ne pas joindre aux Issues des chemins absolus personnels, journaux bruts, XML, captures de jaquettes privées, ROM ou BIOS. Pour une erreur, conserver plutôt la **version**, le **SHA**, un **code de diagnostic neutre**, les étapes sur fixture et la séparation entre **PASS / FAIL / NOT_EXECUTED**.

[Parcours IDEAL prudent](parcours-ideal-next05.md) · [FAQ et dépannage](faq-depannage-next05.md) · [Lexique](lexique.md) · [SUPPORT](../../SUPPORT.md) · [Signaler une vulnérabilité](../../SECURITY.md). Le code est sous [MIT](../../LICENSE), mais les marques et illustrations officielles suivent [leurs propres droits](../../BRANDING.md) ; les médias tiers restent à leurs ayants droit.
