record Pack(int Id, string Name, string Slug, string[] Versions)
{
    public override string ToString() => Name;
}

class CurseForge
{
    readonly HttpClient http = new() { BaseAddress = new Uri("https://api.curseforge.com/v1/") };

    public CurseForge(string apiKey) => http.DefaultRequestHeaders.Add("x-api-key", apiKey);

    async Task<JsonNode> Get(string path) => await Data(await http.GetAsync(path));

    async Task<JsonNode> Post(string path, object body) => await Data(await http.PostAsJsonAsync(path, body));

    static async Task<JsonNode> Data(HttpResponseMessage r)
    {
        using var _ = r;
        if (!r.IsSuccessStatusCode)
            throw new HttpRequestException(r.StatusCode == System.Net.HttpStatusCode.NotFound ? "Introuvable sur CurseForge."
                : $"CurseForge a répondu {(int)r.StatusCode}. Clé API valide ?", null, r.StatusCode);
        return JsonNode.Parse(await r.Content.ReadAsStringAsync())!["data"]!;
    }

    static Pack ToPack(JsonNode m) => new(
        m["id"]!.GetValue<int>(),
        m["name"]!.GetValue<string>(),
        m["slug"]!.GetValue<string>(),
        m["latestFilesIndexes"]!.AsArray().Select(i => i!["gameVersion"]!.GetValue<string>()).Distinct()
            .OrderByDescending(v => Version.TryParse(v, out var x) ? x : new Version()).ToArray());

    public async Task<Pack> GetPack(int id) => ToPack(await Get($"mods/{id}"));

    // Accepte un nom, un Project ID ou un lien curseforge.com/minecraft/<kind>/<slug>.
    // kind/classId : "modpacks"/4471 ou "mc-mods"/6. Avec mc (et loader), seuls les projets disponibles pour cette version sont rendus.
    public async Task<List<Pack>> Find(string query, string kind = "modpacks", int classId = 4471, string? mc = null, int loader = 0)
    {
        Pack? direct = null;
        if (int.TryParse(query, out var id)) direct = await GetPack(id);
        else if (SlugFromLink(query, kind) is string slug)
            direct = await BySlug(slug, kind) ?? throw new Exception("Lien non reconnu. Vérifiez-le, ou collez le Project ID.");
        else
        {
            try
            {
                // gameId 432 = Minecraft, sortField 2 = popularité
                var filter = mc == null ? "" : $"&gameVersion={Uri.EscapeDataString(mc)}&modLoaderType={loader}";
                var data = await Get($"mods/search?gameId=432&classId={classId}&sortField=2&sortOrder=desc&pageSize=30{filter}&searchFilter={Uri.EscapeDataString(query)}");
                return data.AsArray().Select(m => ToPack(m!)).ToList();
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // Clés récentes sans accès à la recherche : on tente le nom exact sous forme de lien ("Abyssal Ascent" -> abyssal-ascent).
                direct = await BySlug(Slugify(query), kind) ?? throw new Exception(
                    "Votre clé API n'a pas accès à la recherche CurseForge et rien ne porte exactement ce nom.\n" +
                    $"Collez plutôt le lien (curseforge.com/minecraft/{kind}/...) ou le Project ID (visible sur sa page CurseForge).");
            }
        }
        if (mc != null && (await FilesFor(direct.Id, mc, loader)).Count == 0)
            throw new Exception($"« {direct.Name} » n'est pas disponible pour Minecraft {mc} avec le loader de ce serveur.");
        return [direct];
    }

    // Avec ou sans https:// et www., majuscules, /files ou ?paramètres à la fin.
    public static string? SlugFromLink(string query, string kind = "modpacks")
    {
        var m = Regex.Match(query, $@"curseforge\.com/minecraft/{kind}/([\w-]+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
    }

    public static string Slugify(string name) => Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    // L'API officielle ne résout pas un slug sans la recherche ; cfwidget (service tiers, sans clé) le fait.
    async Task<Pack?> BySlug(string slug, string kind)
    {
        using var w = new HttpClient();
        using var r = await w.GetAsync($"https://api.cfwidget.com/minecraft/{kind}/{Uri.EscapeDataString(slug)}");
        int? found = r.IsSuccessStatusCode ? JsonNode.Parse(await r.Content.ReadAsStringAsync())?["id"]?.GetValue<int>() : null;
        return found is int id ? await GetPack(id) : null;
    }

    // Fichiers (versions) du projet pour cette version de MC (et ce loader si > 0), le plus récent d'abord.
    public async Task<List<JsonNode>> FilesFor(int modId, string mcVersion, int loader = 0) =>
        (await Get($"mods/{modId}/files?gameVersion={Uri.EscapeDataString(mcVersion)}{(loader > 0 ? $"&modLoaderType={loader}" : "")}&pageSize=50")).AsArray()
            .Select(f => f!).OrderByDescending(f => DateTime.Parse(f["fileDate"]!.GetValue<string>())).ToList();

    // Mods déjà présents dans mods/ et mods-desactives/ (projectId -> nom du .jar), reconnus par l'empreinte CurseForge.
    // Limite : un .jar qui ne vient pas de CurseForge n'est pas reconnu ; s'il est aussi une dépendance, elle serait téléchargée en double.
    public async Task<Dictionary<int, string>> InstalledMods(string root)
    {
        var jars = new[] { "mods", "mods-desactives" }.Select(d => Path.Combine(root, d)).Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.jar")).ToList();
        if (jars.Count == 0) return [];
        var prints = await Task.Run(() => jars.Select(j => Fingerprint.Of(File.ReadAllBytes(j))).ToList());
        var data = await Post("fingerprints/432", new { fingerprints = prints });
        return data["exactMatches"]!.AsArray().DistinctBy(m => m!["id"]!.GetValue<int>())
            .ToDictionary(m => m!["id"]!.GetValue<int>(), m => m!["file"]!["fileName"]!.GetValue<string>());
    }

    // Fichiers à télécharger pour ce mod et ses dépendances obligatoires (récursivement).
    // Ce qui est déjà installé est laissé tel quel : jamais mis à jour.
    public async Task<List<JsonNode>> PlanInstall(int modId, string mc, int loader, Dictionary<int, string> installed, Action<string> log)
    {
        var plan = new List<JsonNode>();
        var queue = new Queue<int>([modId]);
        var seen = new HashSet<int>();
        while (queue.TryDequeue(out var id))
        {
            if (!seen.Add(id)) continue;
            if (installed.TryGetValue(id, out var jar)) { log($"  déjà présent, non modifié : {jar}"); continue; }
            var files = await FilesFor(id, mc, loader);
            var file = files.FirstOrDefault(f => f["releaseType"]?.GetValue<int>() == 1) ?? files.FirstOrDefault() // 1 = release, sinon bêta
                ?? throw new Exception($"La dépendance « {(await GetPack(id)).Name} » n'existe pas pour Minecraft {mc} avec ce loader.");
            plan.Add(file);
            log($"  à installer : {file["fileName"]}");
            foreach (var d in file["dependencies"]!.AsArray())
                if (d!["relationType"]!.GetValue<int>() == 3) queue.Enqueue(d["modId"]!.GetValue<int>()); // 3 = dépendance obligatoire
        }
        return plan;
    }

    public static bool HasServerPack(JsonNode f) => f["isServerPack"]?.GetValue<bool>() == true || f["serverPackFileId"]?.GetValue<int>() > 0;

    public async Task<JsonNode> ServerPackOf(JsonNode f) =>
        f["isServerPack"]?.GetValue<bool>() == true ? f : await Get($"mods/{f["modId"]}/files/{f["serverPackFileId"]}");

    public async Task<JsonArray> FilesById(IEnumerable<int> ids) => (await Post("mods/files", new { fileIds = ids })).AsArray();

    public async Task<JsonArray> ModsById(IEnumerable<int> ids) => (await Post("mods", new { modIds = ids })).AsArray();

    // Certains auteurs bloquent downloadUrl dans l'API ; le chemin CDN reste public.
    public static string DownloadUrl(JsonNode f)
    {
        int id = f["id"]!.GetValue<int>();
        return f["downloadUrl"]?.GetValue<string>() ?? $"https://edge.forgecdn.net/files/{id / 1000}/{id % 1000}/{Uri.EscapeDataString(f["fileName"]!.GetValue<string>())}";
    }
}
