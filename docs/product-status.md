# État produit vérifiable

**Dépôt en migration privée ; état à recontrôler après intégration technique.** Document destiné aux utilisateurs et développeurs : ne pas confondre étude, code présent, suite de tests exécutée et Release téléchargeable.

| Domaine | État | Limite importante |
| --- | --- | --- |
| Socle PowerShell 5.1 Rev.3 | Baseline historique existante, transfert et octets encore à vérifier dans ce dépôt | Ne prouve pas une application GUI |
| Inventaire E1 read-only | Code/contrats historiques connus, tests synthétiques à reproduire ici | Aucun scan sur collection réelle autorisé |
| Tests Pester et CI adaptative | Transfert par lots, résultat GitHub à établir sur HEAD de ce dépôt | Pas de « CI verified » sans vrais jobs applicables |
| Interface Dashboard / Collection / Fiche | Maquettes et critères décrits, **NOT_IMPLEMENTED** | Aucun exécutable livré |
| Bibliothèques, Offline/Stale et snapshots V2 | Sémantique étudiée ; interfaces/candidats NON ADOPTÉS | Pas de fiabilité physique revendiquée |
| Préparation E2–E5 | Intentions/contrats de conception | Pas de validation ou écriture physique |
| E6 / copie réelle, conversion et scraping | **NOT_AUTHORIZED** | Ne pas tester sur ROM/BIOS/SSD/StateRoot |
| Runtime .NET et UI Avalonia | Options étudiées, pas de build/test .NET prouvé | Aucun SDK requis pour ce que l'utilisateur peut télécharger : **aucun téléchargement aujourd'hui** |
| GitHub Release et distribution win-x64 ZIP | **NOT_PUBLISHED / NOT_TESTED** | SHA-256, test externe sans runtime et droits requis à l'avenir |
| Licence et branding | DECISION_PENDING | Cinq assets graphiques non inclus sans autorisation |

La [roadmap](roadmap.md) porte les intentions et non les fonctionnalités livrées. Le [changelog](../CHANGELOG.md) porte les changements réellement intégrés. Ce fichier sera corrigé par chaque PR technique dès preuve disponible ; une mise à jour de date sans contrôle ne vaut pas vérification.
