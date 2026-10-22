record Pack(int Id, string Name, string Slug, string[] Versions)
{
    public override string ToString() => Name;
}

class CurseForge
{
    readonly HttpClient http = new() { BaseAddress = new Uri("https://api.curseforge.com/v1/") };

    public CurseForge(string apiKey) => http.DefaultRequestHeaders.Add("x-api-key", apiKey);

    async Task<JsonNode> Get(string path) => await Data(await http.GetAsync(path));

    static async Task<JsonNode> Data(HttpResponseMessage r)
    {
        using var _ = r;
        if (!r.IsSuccessStatusCode) throw new Exception($"CurseForge a répondu {(int)r.StatusCode}. Clé API valide ?");
        return JsonNode.Parse(await r.Content.ReadAsStringAsync())!["data"]!;
    }

    static Pack ToPack(JsonNode m) => new(
        m["id"]!.GetValue<int>(),
        m["name"]!.GetValue<string>(),
        m["slug"]!.GetValue<string>(),
        m["latestFilesIndexes"]!.AsArray().Select(i => i!["gameVersion"]!.GetValue<string>()).Distinct()
            .OrderByDescending(v => Version.TryParse(v, out var x) ? x : new Version()).ToArray());

    // Recherche de modpacks par nom, les plus populaires d'abord.
    public async Task<List<Pack>> Find(string query)
    {
        // gameId 432 = Minecraft, classId 4471 = Modpacks, sortField 2 = popularité
        var data = await Get($"mods/search?gameId=432&classId=4471&sortField=2&sortOrder=desc&pageSize=30&searchFilter={Uri.EscapeDataString(query)}");
        return data.AsArray().Select(m => ToPack(m!)).ToList();
    }

    // Fichiers (versions) du modpack pour cette version de MC, le plus récent d'abord.
    public async Task<List<JsonNode>> FilesFor(int modId, string mcVersion) =>
        (await Get($"mods/{modId}/files?gameVersion={Uri.EscapeDataString(mcVersion)}&pageSize=50")).AsArray()
            .Select(f => f!).OrderByDescending(f => DateTime.Parse(f["fileDate"]!.GetValue<string>())).ToList();

    public static bool HasServerPack(JsonNode f) => f["isServerPack"]?.GetValue<bool>() == true || f["serverPackFileId"]?.GetValue<int>() > 0;

    public async Task<JsonNode> ServerPackOf(JsonNode f) =>
        f["isServerPack"]?.GetValue<bool>() == true ? f : await Get($"mods/{f["modId"]}/files/{f["serverPackFileId"]}");

    // Certains auteurs bloquent downloadUrl dans l'API ; le chemin CDN reste public.
    public static string DownloadUrl(JsonNode f)
    {
        int id = f["id"]!.GetValue<int>();
        return f["downloadUrl"]?.GetValue<string>() ?? $"https://edge.forgecdn.net/files/{id / 1000}/{id % 1000}/{Uri.EscapeDataString(f["fileName"]!.GetValue<string>())}";
    }
}
