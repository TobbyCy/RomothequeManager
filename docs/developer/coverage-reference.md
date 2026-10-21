# Référence de mesure de couverture — reproductibilité

**État : protocole hérité, à vérifier lors de la migration technique.**

Pour juger un rapport de couverture recevable, contrôler successivement : correspondance de la révision mesurée avec HEAD de PR ; découverte des suites ; exécution des tests synthétiques avec Pester 4.10.1 ; liste des sources instrumentées ; dénominateur et exclusions vérifiés ; nombre de points mesurés ; canaris de découverte ; deux mesures reproductibles sans contamination d'une session précédente.

Une valeur de référence historique (289/5 797) sert uniquement de point de comparaison après validation du nouveau périmètre. Ne pas la déclarer atteinte automatiquement dans le dépôt Produit. Une absence de runner, un test manquant ou une erreur dans l'instrumentation donne FAIL ou NOT_VERIFIED, jamais zéro défaut par convention.

Le rapport de PR doit lier vrais logs et sources, expurger toute donnée privée et distinguer les contrôles Pester des validations V2 non exécutées. Aucun accès aux collections réelles n'est nécessaire ou autorisé.
