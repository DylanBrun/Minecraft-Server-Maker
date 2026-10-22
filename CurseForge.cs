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
        if (!r.IsSuccessStatusCode)
            throw new HttpRequestException(r.StatusCode == System.Net.HttpStatusCode.NotFound ? "Modpack introuvable sur CurseForge."
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

    // Accepte un nom, un Project ID ou un lien curseforge.com/minecraft/modpacks/<slug>.
    public async Task<List<Pack>> Find(string query)
    {
        if (int.TryParse(query, out var id)) return [await GetPack(id)];

        if (SlugFromLink(query) is string slug)
            return [await BySlug(slug) ?? throw new Exception("Lien non reconnu. Vérifiez-le, ou collez le Project ID du modpack.")];

        try
        {
            // gameId 432 = Minecraft, classId 4471 = Modpacks, sortField 2 = popularité
            var data = await Get($"mods/search?gameId=432&classId=4471&sortField=2&sortOrder=desc&pageSize=30&searchFilter={Uri.EscapeDataString(query)}");
            return data.AsArray().Select(m => ToPack(m!)).ToList();
        }
        catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new Exception("Votre clé API n'a pas accès à la recherche CurseForge.\nCollez plutôt le lien du modpack (curseforge.com/minecraft/modpacks/...) ou son Project ID (visible sur sa page CurseForge).");
        }
    }

    // Partie <slug> d'un lien curseforge.com/minecraft/modpacks/<slug> (avec ou sans https://, www. ou /files).
    public static string? SlugFromLink(string query)
    {
        var m = Regex.Match(query, @"curseforge\.com/minecraft/modpacks/([\w-]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    // L'API officielle ne résout pas un slug sans la recherche ; cfwidget (service tiers, sans clé) le fait.
    async Task<Pack?> BySlug(string slug)
    {
        using var w = new HttpClient();
        using var r = await w.GetAsync($"https://api.cfwidget.com/minecraft/modpacks/{Uri.EscapeDataString(slug)}");
        int? found = r.IsSuccessStatusCode ? JsonNode.Parse(await r.Content.ReadAsStringAsync())?["id"]?.GetValue<int>() : null;
        return found is int id ? await GetPack(id) : null;
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
