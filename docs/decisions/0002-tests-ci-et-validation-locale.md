# ADR-0002 — Validation en gates distincts

**État historique : ACCEPTED.**

## Contexte

La réussite d'un test de syntaxe, Pester ou intégration synthétique ne démontre ni la lisibilité d'un disque réel, ni le succès d'un export, ni une autorisation d'action.

## Décision

Distinguer formellement : **G1 code/CI sur fixtures**, **G2 validation localisée et bornée des sources réelles lorsque expressément permise**, **G3 autorisation d'opération physique**, distincte d'une simulation. Un rapport indique SHA/version, protocole, données/portée, résultats observés, refus attendus et limites.

## Conséquences

Il est possible de fusionner un composant tout en conservant des usages réels **NOT_VERIFIED**. Aucun job « green » n'accorde implicitement un CommitGate, une copie, une suppression, une conversion ou un accès à StateRoot.
