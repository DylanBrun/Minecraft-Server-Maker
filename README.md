# Minecraft Server Maker

Une petite app Windows pour monter un serveur Minecraft à partir d'un modpack CurseForge, sans toucher à la ligne de commande. On choisit le modpack, la version, on clique sur « Créer le serveur », puis sur le gros bouton vert. L'app reste ensuite dans la barre des tâches, à côté de l'horloge.

Je l'ai faite pour jouer entre potes sans passer une soirée à installer Forge, chercher la bonne version de Java ou comprendre pourquoi le serveur plante au démarrage.

## Ce qu'elle fait

- **Recherche de modpacks** par nom, par lien CurseForge (`https://www.curseforge.com/minecraft/modpacks/...`) ou par Project ID.
- **Installe le serveur tout seul** à partir du server pack officiel du modpack. Si la dernière version du pack n'en a pas, l'app prévient et indique la version que les joueurs doivent installer.
- **Construit le serveur sans server pack** si besoin (case « Sans server pack ») : elle récupère la version joueur, télécharge les mods, écarte ceux qui ne servent qu'au client, installe Forge, NeoForge ou Fabric, puis fait des essais de démarrage en mettant de côté les mods qui font planter le serveur.
- **Télécharge la bonne version de Java** (8, 17, 21 ou 25 selon la version de Minecraft), uniquement pour le serveur. Pas besoin d'avoir Java installé.
- **Un gros bouton pour lancer et arrêter** le serveur. L'arrêt passe par la commande `stop`, donc le monde est toujours sauvegardé.
- **Plusieurs serveurs** : une liste pour passer de l'un à l'autre, et un bouton pour en supprimer un (avec confirmation).
- **Ajout de mods** compatibles avec la version de Minecraft et le loader du serveur, avec leurs dépendances. Les mods déjà présents ne sont jamais mis à jour.
- **Les adresses pour se connecter** s'affichent pendant que le serveur tourne : réseau local, Radmin VPN, ZeroTier, Internet.
- **La RAM de chaque serveur** se règle avec une jauge ou en tapant un nombre (bouton Réglages…). Le bouton Max propose le maximum sans risque pour le PC : la RAM totale moins 8 Go pour Windows et le jeu.
- **Sauvegardes automatiques du monde** : une copie .zip toutes les 30 minutes pendant que le serveur tourne, les 10 dernières gardées dans le dossier `backups` du serveur. Intervalle et nombre de copies se règlent dans Réglages…, et un opérateur peut les piloter depuis le chat du jeu avec `!ASWorld enable`, `!ASWorld disable` ou `!ASWorld set <minutes>`.
- **Une console** pour taper des commandes au serveur (`op Pseudo`, `list`, `say Salut`…).
- **Options** (menu Fichier > Paramètres) : réduire l'app au lancement du serveur, et lancer Radmin VPN automatiquement. Les deux sont désactivées par défaut.

## Ce qu'il faut

- Windows 10 ou 11 en 64 bits.
- Une **clé API CurseForge**, gratuite, à créer sur [console.curseforge.com](https://console.curseforge.com). Une clé toute neuve peut mettre un peu de temps avant d'avoir accès à la recherche par nom : en attendant, colle le lien du modpack ou son Project ID.
- Assez de RAM pour le modpack (6 à 8 Go pour un gros pack).

## Utilisation

1. Lance `MinecraftServerMaker.exe` et colle ta clé API.
2. Cherche le modpack, choisis la version de Minecraft, puis clique sur **Créer le serveur**. L'app te demande d'accepter l'[EULA de Minecraft](https://aka.ms/MinecraftEULA).
3. Clique sur **LANCER**. Quand le statut passe à « En ligne ✔ », tes potes peuvent se connecter avec une des adresses affichées.
4. Au premier lancement, Windows demande d'autoriser Java dans le pare-feu : accepte, sinon personne ne pourra se connecter.

Les joueurs doivent avoir **exactement la même version du modpack** que le serveur, installée depuis l'app CurseForge.

## Où sont les fichiers

Tout est rangé dans `%AppData%\MCServerTray` :

- `servers\` : un dossier par serveur (monde, mods, configs, crash-reports…). Le menu Fichier > « Ouvrir le dossier du serveur » y mène directement.
- `java\` : les versions de Java téléchargées par l'app.
- `logs\` : le journal de l'app, un fichier par jour, gardé 14 jours (menu Fichier > « Ouvrir les journaux de l'app »). Utile pour comprendre un problème après coup.
- `settings.json` : les réglages. La clé API y est chiffrée avec DPAPI, elle n'est lisible que par ton compte Windows sur ce PC.

## Compiler depuis les sources

Il faut le SDK .NET 8.

```bash
dotnet build
```

Pour obtenir un seul `.exe` qui marche sans rien installer :

```bash
dotnet publish src/MinecraftServerMaker -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

Le code est rangé dans `src/MinecraftServerMaker` : `Core/` pour la logique (CurseForge, téléchargements, Java, construction des serveurs) et `UI/` pour les fenêtres.

Les tests (xUnit, sans réseau) sont dans `tests/MinecraftServerMaker.Tests` :

```bash
dotnet test
```

## Bon à savoir

- L'app exécute les scripts et les `.jar` des modpacks, c'est son rôle. Les fichiers venant de CurseForge sont vérifiés (SHA-1) avant d'être utilisés, mais ça garantit seulement qu'ils sont identiques à ceux publiés, pas que leurs auteurs soient fiables.
- Le bouton d'alimentation de Radmin VPN ne peut pas être actionné par une autre app : si Radmin est éteint, l'app ouvre sa fenêtre et te demande de cliquer dessus.
- L'exe n'est pas signé, donc Windows SmartScreen peut afficher « éditeur inconnu » au premier lancement.
- Quilt et les très vieilles versions de Forge (1.7.10 et avant) ne sont pas gérés pour la construction sans server pack.
