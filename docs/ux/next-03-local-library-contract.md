# NEXT-03 — Contrat fonctionnel du premier parcours local

**DEV 2 — Draft documentaire.** Cible : **Bibliothèques → Choisir un dossier → Inventorier → Consulter les résultats → Collection / Observations**. Parcours destiné à une **bibliothèque locale réelle**, **non encore autorisé à scanner une bibliothèque personnelle**. Réutiliser [NEXT-02 P1](../architecture/next-02-inventory-contract.md), [UX-CONTRACT-02 #76](https://github.com/TobbyCy/RomothequeManager/pull/76) et [#61](https://github.com/TobbyCy/RomothequeManager/issues/61). **Ne pas modifier #76**, créer de nouveau DTO, scanner, stockage ou architecture parallèle.

## Actions et conditions

1. **Bibliothèques** : afficher les bibliothèques et leurs provenances **réellement connues** ; séparation permanente de la démo. Une simple navigation/selection ne parcourt aucun disque. Bouton **Choisir un dossier** explicite.
2. **Choisir un dossier** : sélecteur local d'une racine absolue comprenant `ROMS/`, éventuellement `MEDIAS/` et métadonnées ES-DE. Annulation = aucun changement. Valider root/permissions/limites et rejeter UNC, URI, liens/jonctions/reparse points, chemins hors périmètre. Une lettre USB ne prouve pas une identité physique.
3. **Inventorier** : action **distincte** après sélection et information « lecture seule / portée ROMS / limites / annulation ». Session `InventorySession.Scan` **single-flight**, budget borné ; aucune ouverture de contenu ROM pour hash, modification, réseau, extraction ou import ES-DE implicite. État visible pendant l'opération, bouton Annuler.
4. **Résultats** : afficher `ScanId`, date, `SourceKind`, `CoverageProof`, `ScanStatus`, `ObservedFileCount`, `TotalFileCount` seulement si prouvé, liste et diagnostics. `Current` = dernier `Complete` ; `LastAttempt` = dernière tentative même partielle/échouée. Distinguer cette source de toute autre projection importée.
5. **Collection / Observations** : **observations de fichiers** : chemin *relatif*, type candidat (dont playlist), extension, taille/date si réellement observées, plateforme déclarée ou `UNKNOWN`, diagnostic, origine et génération. **Jeux référencés ES-DE** : titre/plateforme/édition/description/médias uniquement depuis import explicite distinct. Changer bibliothèque/mode invalide filtres, fiche et sélection A/B étrangers. **Aucune jointure silencieuse** sur nom, chemin, taille ou illustration.

| Preuve disponible | Affichage permis | Non autorisé |
|---|---|---|
| NEXT-02 seul, y compris **MYSTERY sans XML** | `Fichiers observés`, plateformes observées, `Jeux identifiés : Inconnu` | 32 fichiers = 32 jeux ; reconstruire les 30 titres fictifs à partir des IDs oracle |
| ES-DE facultatif (IDEAL / CHAOS) | `Jeux référencés ES-DE` distinct du compteur fichiers ; ROM référencée disponible/introuvable | « jeu jouable/identifié par le scanner » ou import forcé |
| Médias ES-DE | `image`, `thumbnail`, `marquee` déclarés, et état vérifié **si effectivement ouverts** ; fallback explicite | déduire toutes les images du disque ou inventer des médias attendus sans déclaration |
| Deux fichiers ressemblants | `POTENTIAL_DUPLICATE` + motif/provenance | `doublon exact`, fusion, nettoyage ou suppression |

## États orthogonaux et continuité

| État | Comportement obligatoire |
|---|---|
| `COMPLETE` | Portée `ROMS/` **autorisée et énumérée** : `TotalFileCount = ObservedFileCount` y compris `0` si dossier réellement vide. Ce n'est **pas** une collection de jeux exhaustive. |
| `PARTIAL` | Annulation, limites ou accès interrompu **après début** : observations partielles, portée inconnue, `TotalFileCount=null`, diagnostic ; dernier `Current` complet inchangé, `LastAttempt` partiel. |
| `FAILED` | Racine/ROMS inaccessible dès l'ouverture : diagnostic, `TotalFileCount=null`, `Current` précédent conservé ; pas de faux inventaire vide. |
| `OFFLINE` / `INACCESSIBLE` | Disponibilité du support, distincte du scan ; si index déjà accessible, ancien état **daté** en lecture seule ; sinon `Indisponible/Inconnu`. Jamais 0/supprimé. |
| `STALE` | Fraîcheur d'une génération, distincte de disponibilité et de couverture ; anciennes observations datées **seulement si l'index existe** ; aucune promesse de persistance entre redémarrages. |
| `UNKNOWN` | Toute disponibilité, fraîcheur, identité jeu ou total sans preuve = `Inconnu`, jamais zéro par défaut. |

En déconnexion pendant lecture, rendre un `PARTIAL` si le scan est commencé ou `FAILED` si aucune ouverture initiale possible ; conserver `Current` et fournir le diagnostic correspondant. Une reconnexion avec nouvelle lettre n'autorise pas une association automatique : `RelocateVerified` compare une **empreinte de métadonnées**, pas l'identité du SSD.

## Preuves nécessaires et limites P1

Une conclusion doit être rattachable à **librarySessionId / scanId ou LibraryId ES-DE**, source, date, racine relative/portée, couverture et diagnostic. Une référence ES-DE `ROM_MISSING` n'est **pas** une suppression. `MEDIA_MISSING` exige un média effectivement déclaré. `CANCELLED`, `LIMIT_REACHED` et `UNREADABLE_ENTRY` ne produisent aucun total exhaustif. **Absence d'observation ≠ suppression** ; **doublon candidat ≠ confirmé** ; **fichier ≠ jeu identifié** ; **Inconnu ≠ 0**. Afficher le contraire constitue un défaut P1.

**Gate pour bibliothèque réelle** : avant toute opération sur SSD utilisateur, **GO propriétaire distinct**, préflight local sécurité, recette Windows sur **fixtures synthétiques**, vérification des risques de substitution concurrente des chemins/reparse points et gestion des budgets/cancellation. **Aucun scan réel n'est autorisé par la PR documentaire.** Captures/logs/CI n'exposent jamais de ROMs, chemins privés, médias tiers ou hashes de données personnelles.

**Hors périmètre :** scraping, téléchargements, réorganisation, renommage, suppression, déduplication, création/écriture de fichiers ROM/XML/médias, hash de ROM réelle, export/copie/conversion/sauvegarde/synchronisation physiques, stockage persistant nouveau, correspondance automatique des jeux, Release. [Maquettes #78](https://github.com/TobbyCy/RomothequeManager/pull/78) = référence visuelle **non normative**.

[Recette vérifiable NEXT-03](next-03-local-library-recipe.md). Aucune fusion, Release ou modification de #76.
