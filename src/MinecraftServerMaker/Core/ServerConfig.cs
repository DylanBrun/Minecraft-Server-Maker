// Réglages propres à un serveur, rangés dans son dossier : ils le suivent et disparaissent avec lui.
class ServerConfig
{
    public int RamMb { get; set; } // 0 = laisser le modpack décider
    public bool AutoBackup { get; set; } = true;
    public int BackupMinutes { get; set; } = 30;
    public int BackupsToKeep { get; set; } = 10;

    static string PathFor(string root) => Path.Combine(root, "minecraft-server-maker.json");

    public static ServerConfig Load(string root)
    {
        try { return JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(PathFor(root))) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Save(string root)
    {
        var tmp = PathFor(root) + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this));
        File.Move(tmp, PathFor(root), true);
    }
}
