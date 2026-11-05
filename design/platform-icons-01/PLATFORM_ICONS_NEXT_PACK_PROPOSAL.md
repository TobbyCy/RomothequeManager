# PLATFORM_ICONS_NEXT_PACK_PROPOSAL — proposition après clôture #164

Proposition : GB + GameCube, deux sujets graphiques distincts (portable verticale et console de salon) déjà présents dans les libellés de travail #120. Ce ne sont pas des PlatformKey certifiées, ni des preuves d'appartenance à une collection personnelle.

Réutilisation stricte : même structure design/platform-icons-01/sources/study-*, 3 styles console-name/controller/console-silhouette, Dark/Light, gabarit 192×64 pour badge, 64×64 pour pictogramme, petits SVG optiques, PNG 16/24/32/48/64/128/256, fallback existant, planches, manifestes, CRC/alpha/provenance/licences. Tous platformKey=null, platformMappings=[] jusqu'à GO mapping.

Style à privilégier : console-silhouette à partir de 32 px après validation juridique, car différenciant mieux les familles matérielles ; à 16/24 px, prévoir optique simplifiée et fallback si validation insuffisante. Controller peut se confondre entre plateformes ; console-name exige 3:1 et un label accessible distinct. Silhouettes et libellés de marques ne sont pas automatiquement redistribuables.

Pas de nouvelle branche ni production avant fermeture de #164, revue DEV 2, CI verte par lot infrastructure distinct et GO propriétaire. #123 reste BACKLOG_ONLY ; NEXT-05 inchangé.

PLATFORM_ICONS_NEXT_PACK_PROPOSAL / NO_NEW_BRANCH / NO_PLATFORM_MAPPING.
