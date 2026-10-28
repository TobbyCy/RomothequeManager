# DEMO-LIBRARY-01 — générateur synthétique

**Candidat DEV 1 en attente de revue du catalogue DEV 2 — Issue #71.** Les 30 titres sont fictifs. Aucun contenu commercial, BIOS, API, scraper ni téléchargement.

## Exécution explicite
Nécessite **Node.js 20+** et aucun module npm tiers.

Sous PowerShell ou bash, depuis le dépôt :
- Générer : node tools/demo-library/generate.cjs --output "C:\temp\demo01-inexistant"
- Vérifier en lecture seule : node tools/demo-library/generate.cjs --verify "C:\temp\demo01-inexistant"

La cible doit être un chemin absolu local vers un dossier **qui n'existe pas** ; son parent doit exister. Le programme refuse les chemins UNC, un dossier sous le dépôt de l'application, les liens symboliques dans la chaîne parente et tout écrasement de sortie existante. Le chemin fourni est l'autorisation explicite de créer exclusivement la nouvelle démo. Aucun scan de bibliothèque existante. Catalogue alternatif : argument --catalog avec un JSON contenant exactement 30 jeux et schemaVersion demo-library-01/1.

## Trois variantes
- **IDEAL** : ROMS/platform + MEDIAS/platform/metadata/es-de-gamelist.xml ; 30 jeux importables, descriptions, genres, éditeurs/années fictifs, médias PNG locaux réels (covers, thumbnails, logos, screenshots, backcovers, backgrounds).
- **CHAOS** : mêmes 30 identifiants, dossiers complexes, caractères accentués, ROMs et médias intentionnellement absents, quelques doublons candidats supplémentaires et entrées folder. XML reste syntaxiquement correct afin de produire des diagnostics.
- **MYSTERY** : mêmes 30 identifiants manifestés, noms de fichiers peu significatifs, pas de XML ES-DE, aucun média. Test exclusif de l'inventaire fichier NEXT-02.

Chaque variante possède un manifest JSON : identités, fichiers, états attendus et SHA-256 **des seules fixtures générées**. Le manifest racine pointe vers les trois manifests par hash.

Les fichiers ROM factices sont du texte en clair avec le préfixe DEMO01_SIMULATED_NON_EXECUTABLE_NO_ROM_OR_BIOS_CONTENT, jamais des ROMs exécutables. Une playlist .m3u texte référence deux faux disques .chd. Les PNG sont des images raster générées localement par code bitmap original (titres et couleurs distincts, sans œuvres ou polices tierces).

## Sécurité et limites
Aucune suppression, aucun overwrite ni nettoyage automatique. Les chemins sont vérifiés **avant** la première écriture. Une interruption peut laisser une nouvelle sortie incomplète ; sans manifest racine valide elle ne constitue pas une livraison. Générer dans un **autre** nouveau dossier au prochain essai. Aucune écriture dans des dossiers personnels non choisis ni dans la distribution de l'application. Pas de protection atomique complète contre les remplacements concurrents de chemins : ne pas utiliser sur bibliothèque réelle.

Pour un catalogue, runtime Node et version zlib identiques, la sortie est reproductible (aucun timestamp/chemin absolu intégré aux fixtures). --verify vérifie SHA, taille et signatures PNG. Les fixtures historiques #64 ne sont jamais ouvertes en écriture.

Les tests MSTest dédiés vérifient import ES-DE, inventaire NEXT-02, images locales, hashes, homonymes, multidisque, cas manquants, sorties déterministes et refus de chemins incorrects. Tests réalisés exclusivement dans des dossiers temporaires synthétiques.

**Aucun merge, scan réel ni Release dans cette PR Draft.** Le contrat DEV 2 demeure à approuver.
