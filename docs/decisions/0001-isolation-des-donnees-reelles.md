# ADR-0001 — Isolation des données réelles

**État historique : ACCEPTED.** Règle d'ingénierie à maintenir dans les versions suivantes.

## Contexte

Les tests d'inventaire, d'associations et de transaction doivent être reproductibles sans risque pour les bibliothèques d'un utilisateur, ses jeux, métadonnées ES-DE, favoris, fichiers persistants ou données personnelles.

## Décision

Le dépôt et sa CI contiennent **uniquement code, documentation et fixtures synthétiques**. Les tests ne doivent pas accéder à des disques utilisateur, chemins réels, bibliothèques ROM, BIOS, sauvegardes, fichiers ES-DE véritables ou StateRoot utilisateur. Un test local sur données réelles nécessiterait une autorisation distincte et ne relève pas de la contribution ordinaire.

## Conséquences

Les CI sont déterministes et réduisent l'exposition des données. Le revers est une limite assumée : une suite synthétique PASS **ne prouve pas** la sûreté sur du matériel ou des bibliothèques physiques. Les opérations effectives suivent des gates propres, non déclenchés par le merge de code.
