# État des fonctionnalités

> **Point de référence DOC-GOV-01 — 23 octobre 2026 :** dépôt Produit **PUBLIC**, `main=408d6263be6ae1fbc77c31f6eb9fb530cb4f76e6`. Les [PR #42](https://github.com/TobbyCy/RomothequeManager/pull/42) (POC Windows V2-01A) et [#45](https://github.com/TobbyCy/RomothequeManager/pull/45) (CI-DOTNET-01) sont **fusionnées**. La [CI `push main` #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607) est **COMPLETED / SUCCESS** sur ce SHA, y compris build/tests .NET, smoke de fenêtre Avalonia et `ci-policy-gate`. Cette preuve est propre à ce commit ; elle n'atteste pas une Release ni un usage réel.

Le terme **fonctionnel** désigne ici un POC exécuté et testé **sur fixtures fictives**, pas un produit prêt pour des bibliothèques personnelles. Le code intégré, la validation CI et la disponibilité d'un binaire distribué sont trois états distincts.

## Capacités réellement intégrées

| Domaine | État intégré et preuves | Limites actuelles |
| --- | --- | --- |
| Moteur Windows PowerShell 5.1 / inventaire E1 | Sources et contrôles synthétiques présents ; CI-0 à CI-4 et Pester applicables **SUCCESS** dans le [run #39919523607](https://github.com/TobbyCy/RomothequeManager/actions/runs/39919523607) | Aucun scan ni modification de collection réelle autorisé |
| Prototype web `prototype/ui11/` | Dashboard, Bibliothèques, comparaison A/B, Collection et fiches sur données fictives ; smoke Node et Edge headless `file://` **SUCCESS** dans ce run | Pas de persistance, ni de connexion à une collection réelle ; Edge headless n'est pas une recette graphique humaine |
| Bibliothèques / snapshots / catalogue PS → JSON → JS | Producteur PowerShell, projections, comparateur à six classes et consommateur JS intégrés ; canaris et fixtures synthétiques exécutés en CI | DTO 0.2 encore candidat ; l'état OFFLINE/STALE ou un index inaccessible ne prouve jamais une suppression |
| **POC Windows V2-01A** | `src/RomothequeManager.V2/` intégré via **PR #42** ; .NET 10, Avalonia 12.1.4, **trois vues** Dashboard / Collection-recherche / Fiche, **six jeux inventés** en mémoire ; **8 MSTest** et smoke réel de fenêtre Windows en CI | Lecture seule ; pas de scan, stockage, réseau métier, export, import de ROM, bibliothèques/snapshots en quatrième vue, ni essai utilisateur sur PC externe |
| SDK et restauration .NET | `global.json` impose **SDK 10.0.401 exact**, `rollForward=disable` ; locks NuGet committés, restore verrouillé ; [PR #45](https://github.com/TobbyCy/RomothequeManager/pull/45) ajoute `actions/setup-dotnet` épinglé avant les contrôles | Le run post-fusion prouve le succès du provisionnement sur son runner, **pas encore** le scénario reproductible d'une image initialement dépourvue de 10.0.401 (suivi [#43](https://github.com/TobbyCy/RomothequeManager/issues/43)) |
| ZIP Windows / Release | Publication self-contained `win-x64` et SHA-256 **générés dans `RUNNER_TEMP`**, sans `upload-artifact` ; les **quatre anciens artefacts Actions ont été purgés** et contre-vérifiés ([PUB-04 #44](https://github.com/TobbyCy/RomothequeManager/issues/44#issuecomment-6359600451)) | **Aucun ZIP téléchargeable, aucun installateur, aucune GitHub Release** ; licences/notices tierces du package encore à qualifier ; d'éventuelles copies téléchargées avant purge ne sont pas effacées par GitHub |
| Licence et visuels | Licence MIT pour le code original et la documentation autorisée ; `LEGAL.md` et `BRANDING.md` publics ; bandeau PNG officiel V2 présent dans l'arbre BRAND-01 sur autorisation du propriétaire | Aucune licence MIT implicite sur le bandeau (droits réservés) ; autres SVG/ICO non distribués ; aucun droit sur ROM/BIOS/jeux tiers |

## Travail futur ou non livré

- **V2-01B — Bibliothèques et snapshots dans l'application Avalonia :** quatrième vue, comparaison A/B, états OFFLINE/STALE et données synthétiques ; **préparation de périmètre**, pas de code intégré dans V2-01A.
- **Bibliothèques personnelles, favoris, tags, persistance et synchronisation :** non implémentés dans le POC Windows.
- **Export E2 à E6, conversion, copie et opérations physiques :** conception uniquement ; aucune action réelle autorisée.
- **Distribution Windows, USB, essais externes, compatibilité SteamOS/Linux/ARM :** non validés. L'obtention d'une CI verte ne lève pas l'interdiction de Release.

La [roadmap](roadmap.md) décrit les intentions. Le [CHANGELOG](../CHANGELOG.md) distingue les intégrations des anciens états de sprint. Les sections suivantes conservent les notes historiques **à leur date de rédaction** ; elles ne remplacent pas le tableau courant.

## Historique de construction du prototype

Les paragraphes ci-dessous conservent la trace des anciens sprints au moment de leur conception ; leurs libellés de validation n'attestent pas les tests du `main` publié.

**Sprint #13 (historique de la PR #28, désormais fusionnée)** : ajout d'un adaptateur de DTO `0.1.0-synthetic` pour le registre et les comparaisons précalculées de #27. Deux modes explicitement séparés, sans moteur métier JS en mode projection. Aucune preuve de parité runtime PS5.1 ni navigateur réel.


**Sprint #15 (historique de la PR #28, désormais fusionnée)** : Collection projetée et fiche de jeu synthétique candidate, filtre plateforme, provenance, variantes et observations ROM séparées, navigation vers snapshots. Adaptateur aligné sur `countKnown=false/observedUnitCount=null` de #27. Contrôles V8 simulés exécutés ; Node/navigateur/PS5.1 non exécutés. Le catalogue a ensuite été intégré via la PR #29 ; le DTO 0.2 reste un contrat candidat, sans adoption finale.
