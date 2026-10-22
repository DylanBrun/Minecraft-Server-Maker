class Settings
{
    public string ApiKey { get; set; } = "";
    public string ServerDir { get; set; } = "";
    public string McVersion { get; set; } = "";
    public bool HideOnStart { get; set; } // désactivé par défaut
    public bool AutoRadmin { get; set; } // désactivé par défaut

    public static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCServerTray");
    static string FilePath => Path.Combine(DataDir, "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
    }
}
