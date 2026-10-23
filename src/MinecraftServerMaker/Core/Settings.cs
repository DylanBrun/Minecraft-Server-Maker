class Settings
{
    // La clé n'est jamais écrite en clair : chiffrée avec DPAPI, lisible seulement par ce compte Windows sur ce PC.
    [JsonIgnore] public string ApiKey { get; set; } = "";
    public string ApiKeyProtected
    {
        get => ApiKey == "" ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(ApiKey), null, DataProtectionScope.CurrentUser));
        set
        {
            try { if (value != "") ApiKey = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser)); }
            catch (Exception e) when (e is CryptographicException or FormatException) { } // autre PC ou compte : clé à ressaisir
        }
    }
    // Ancien format (clé en clair) : relu une fois, puis réécrit chiffré par Load().
    [JsonPropertyName("ApiKey"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyApiKey { get => null; set { if (!string.IsNullOrEmpty(value)) ApiKey = value; } }
    public string ServerDir { get; set; } = "";
    public string McVersion { get; set; } = "";
    public bool HideOnStart { get; set; } // désactivé par défaut
    public bool AutoRadmin { get; set; } // désactivé par défaut

    public static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCServerTray");
    static string FilePath => Path.Combine(DataDir, "settings.json");

    public static Settings Load()
    {
        try
        {
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new();
            s.Save(); // migre une éventuelle clé en clair vers le format chiffré
            return s;
        }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDir);
        // Écrit à côté puis remplace : une coupure pendant l'écriture ne corrompt pas les réglages.
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this));
        File.Move(tmp, FilePath, true);
    }
}
