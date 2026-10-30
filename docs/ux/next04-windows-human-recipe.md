# NEXT-04.1 / USAGE-02 — recette humaine Windows du ZIP portable

**Historique :** les dix scénarios HUM-01..10 de NEXT-04 ont été exécutés et reçus **10/10 PASS** avec le précédent ZIP entièrement fictif ([Produit #97](https://github.com/TobbyCy/RomothequeManager/issues/97)). Ce résultat ne valide **pas** le nouveau binaire NEXT-04.1.

**Nouvelle recette NEXT-04.1 (#99/#100/#101) : 12 contrôles NOT_EXECUTED avant essai humain.** La CI Windows automatise le moteur, l'interface, l'archive et une simulation de déplacement ; elle ne prouve ni clics réels, ni DPI physique, ni SSD amovible réel.

## Préparation — ZIP application et démo fictive séparés

1. Depuis [PR #102](https://github.com/TobbyCy/RomothequeManager/pull/102), relever le **HEAD exact** et le run `NEXT-04 / ES-DE Avalonia Windows synthetic UX proof` **SUCCESS** de ce HEAD. Télécharger l'artefact temporaire (rétention 3 jours) : ZIP applicatif, ZIP DEMO-FICTIVE, deux fichiers `.zip.sha256` et captures CI.
2. Vérifier chaque archive séparément avec `Get-FileHash -Algorithm SHA256 .\<nom>.zip` et la valeur du `.sha256`. Extraire les deux ZIP dans des dossiers distincts, sans modifier leurs fichiers.
3. Le dossier application doit présenter immédiatement **`RomothequeManager.V2.exe` à la racine**, le logo, l'ICO, des notices et le dossier `licenses/`. L'EXE est `win-x64`, autonome, single-file self-contained non trimé. Aucun Node, Git, SDK ou installateur nécessaire. L'application non signée peut déclencher SmartScreen ; aucun contournement automatique.
4. Les racines fictives dans DEMO-NEXT04 sont **IDEAL** (30 jeux/13 systèmes), **CHAOS** (manquants), **MYSTERY** (sans XML), **MALFORMED** (XML invalide) et **LONG** (155 références GBA). Choisir toujours le **parent de ROMS/**, jamais ROMS/ seul. Les pseudo-ROMs sont des fichiers texte inertes, les images générées, sans élément commercial.

## Matrice de réception humaine — PASS / FAIL / NOT_EXECUTED

| ID | Action humaine Windows | Attendu |
| --- | --- | --- |
| HUM-01 | Vérifier archives/SHA, extraire et lancer EXE normal | EXE évident à la racine, fenêtre « ROMothèque Manager », **aucun jeu/démo préchargé**, logo/ICO et guide lisibles |
| HUM-02 | Sur l'accueil vide, ouvrir picker puis Annuler | Accueil vide inchangé, aucun accès implicite ni import |
| HUM-03 | Choisir IDEAL puis annuler confirmation | Accueil vide inchangé ; aucune lecture de catalogue adoptée |
| HUM-04 | Rechoisir IDEAL, confirmer lecture seule | 30 références/13 systèmes ; Tableau de bord → Collection → Fiche → retour |
| HUM-05 | Recherche, filtre, fiche, retour, actualisation | Recherche, page, sélection, médias et scroll cohérents |
| HUM-06 | Import CHAOS puis tenter MYSTERY et MALFORMED | Diagnostics neutres, refus atomique, dernier catalogue valide conservé |
| HUM-07 | Import LONG, page 2, scroll, recherche, fiche, retour, actualisation | Limite 50/page, page et position conservées |
| HUM-08 | Choisir IDEAL/ROMS au lieu d'IDEAL | Refus clair, sans scan ni modification source |
| HUM-09 | Collection IDEAL et LONG : comparer largeurs « Ouvrir la fiche », bord droit grille/recherche/résultats ; Tab, Shift+Tab, Entrée, Échap et redimensionnement | Boutons homogènes et alignement aux formats compacts et larges ; noter tout test réellement effectué à 5120×1440 125 % et 1920×1200 150 % |
| HUM-10 | Fermer et relancer l'application après import | **Retour à l'accueil vide** : aucun chemin ou catalogue persisté, fichiers démo inchangés |
| HUM-11 | Depuis l'accueil : « Essayer une démonstration fictive », puis « Revenir à l'accueil vide » | Démo uniquement sur choix conscient, puis état vide |
| HUM-12 | Copier le dossier complet et relancer ; si autorisé, essayer depuis un SSD amovible et après changement de lettre | Exécutable portable, images et licences toujours accessibles ; ne déclarer SSD physique PASS que si essayé |

Documenter pour chaque FAIL : `action → attendu → observé → sévérité P0/P1/P2`, OS/architecture, SHA **des deux ZIP**, résolution/DPI réellement testés et captures **uniquement fictives**. Les DPI de la CI sont simulés ; l'exécution `SUBST R:/S:` simule des lettres, pas un SSD réel.

**Hors de cette recette :** la ROMothèque personnelle, le XML GameCube, fichiers de jeux, BIOS et médias privés ; une première lecture de source réelle relève d'une autorisation locale distincte et du diagnostic DEV 1/Agent PC. Pas de Release ni scan NEXT-03 réel sans GO du Contrôleur.
