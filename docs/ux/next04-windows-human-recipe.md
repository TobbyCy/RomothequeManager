# NEXT-04 — recette humaine Windows du binaire standard

**Statut : PREPARED / NOT_EXECUTED.** Cette recette est distincte des 13 captures GUI automatisées et du harnais natif moteur. À exécuter sur le ZIP autonome du dernier HEAD combiné de la **PR Produit #93**, jamais sur une bibliothèque privée.

## Préparation contrôlée

1. Dans le run Windows GUI de #93 correspondant au **HEAD exact**, récupérer l'artefact temporaire en quarantaine. Vérifier `*.zip.sha256` avec `Get-FileHash <fichier.zip> -Algorithm SHA256`, puis extraire le ZIP dans un dossier neuf.
2. Sur la même révision des sources (téléchargées séparément du ZIP Windows), disposer de Node.js et générer des **données exclusivement synthétiques** : `node tools/demo-library/generate.cjs --output <dossier-temporaires>` puis `node tools/demo-library/generate.cjs --verify <dossier-temporaires>`. Ne pas utiliser d'archives ou ROMs personnelles. Le ZIP Windows standard ne contient pas ces fixtures.
3. Lancer `RomothequeManager.V2.exe`. Relever version Windows, résolution et échelle DPI réellement appliquées, et SHA du ZIP, sans enregistrer de chemin de dossier privé dans les preuves.

## Scénarios fonctionnels — saisir PASS, FAIL ou NOT_EXECUTED

| ID | Manipulation réelle | Attendu / observation à noter | Statut initial |
| --- | --- | --- | --- |
| HUM-01 | Bibliothèques → Choisir une bibliothèque → **Annuler dans le sélecteur natif** | Aucun import, pas de scan, vue conservée | NOT_EXECUTED |
| HUM-02 | Choisir **racine IDEAL contenant directement `ROMS/`** → **Annuler dans la confirmation** | Dialogue lisible, aucune modification/association | NOT_EXECUTED |
| HUM-03 | Rechoisir la même racine → **Importer en lecture seule** | 30 jeux fictifs / 13 plateformes, Dashboard/Systèmes/Collection/Fiche accessibles, état d'import indéterminé sans faux pourcentage | NOT_EXECUTED |
| HUM-04 | Naviguer : recherche, système, fiche, retour, actualisation | Conserver filtre/texte/page/scroll et métadonnées ES-DE (ou « Non renseigné »), sans perte de sélection valide | NOT_EXECUTED |
| HUM-05 | Importer CHAOS, puis essayer MYSTERY | Anomalies expliquées, dernier catalogue valide conservé en cas de refus ; aucune suppression suggérée | NOT_EXECUTED |
| HUM-06 | Sélectionner le sous-dossier `ROMS/` au lieu de son parent | Refus intelligible, aucune auto-découverte, pas de chemins privés exposés | NOT_EXECUTED |
| HUM-07 | Parcours réel clavier Tab/Shift+Tab/Entrée/Échap, souris ; taille de fenêtre 760×480 DIP | Boutons utilisables, focus visible, pas de boutons essentiels inaccessibles ni texte critique rogné | NOT_EXECUTED |
| HUM-08 | Contrôler logo et icône (barre des tâches/EXE) ; DPI Windows physique 125 %, 200 % et écran ultralarge **si disponibles** | Netteté perceptive et proportions ; déclarer non testé pour les configurations absentes | NOT_EXECUTED |
| HUM-09 | Fermer et rouvrir l'application | Aucune bibliothèque mémorisée automatiquement ; pas de scan sans choix explicite | NOT_EXECUTED |
| HUM-10 | Vérifier `node tools/demo-library/generate.cjs --verify <dossier-temporaires>` après l'essai | Fixtures demeurées identiques, sans écriture sur les sources | NOT_EXECUTED |

La fixture profonde de 155 jeux (page 2/offset réel) est créée séparément par le **workflow CI**, pas par le générateur standard seul. Ne pas l'annoncer disponible manuellement sans préparation indépendante documentée. La pagination de 27 diagnostics et le clic sur « suivants » sont prouvés par structure et état de l'arbre CI ; le **clic humain** reste à réaliser si requis pour l'Alpha.

## Protocole de preuve et décision

Documenter pour chaque ID le statut, la date, le HEAD exact, le SHA-256 du ZIP, l'édition de Windows, l'échelle DPI/résolution **observées** et une description courte en cas de FAIL. N'utiliser que des captures de fixtures fictives, sans nom ou chemin personnel, sans jaquettes commerciales ni maquettes privées. Aucune valeur de simulation ne doit être présentée comme un résultat d'inventaire réel.

Les tests Windows CI prouvent `GUI_RENDER_IMPORT_INTERNAL_PASS`, pas le flux complet **picker → confirmation → annulation → import**. Les verdicts de DEV 2, Superviseur et Contrôleur restent distincts. Aucun scan NEXT-03 réel, export, écriture, suppression, scraper ni Release.
