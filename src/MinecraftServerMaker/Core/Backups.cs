// Copies de sauvegarde du monde : backups\<monde>-AAAA-MM-JJ_HH-mm-ss.zip dans le dossier du serveur.
static class Backups
{
    public static string Dir(string root) => Path.Combine(root, "backups");

    // Nom du dossier du monde, lu dans server.properties ("world" par défaut).
    public static string WorldName(string root)
    {
        var props = Path.Combine(root, "server.properties");
        var name = File.Exists(props)
            ? File.ReadLines(props).FirstOrDefault(l => l.StartsWith("level-name="))?["level-name=".Length..].Trim()
            : null;
        name = Path.GetFileName(name ?? ""); // jamais de chemin : le fichier vient du modpack
        return name == "" ? "world" : name;
    }

    // Zippe le monde puis ne garde que les "keep" copies les plus récentes. Renvoie le chemin du zip.
    // Les fichiers ouverts par le serveur sont lus quand même ; session.lock, son verrou, est ignoré.
    public static string Create(string root, int keep, DateTime now)
    {
        var name = WorldName(root);
        var world = Path.Combine(root, name);
        if (!Directory.Exists(world)) throw new DirectoryNotFoundException($"Monde introuvable : {world}");
        var dir = Directory.CreateDirectory(Dir(root)).FullName;
        var zip = Path.Combine(dir, $"{name}-{now:yyyy-MM-dd_HH-mm-ss}.zip");
        var part = zip + ".part";
        try
        {
            using (var file = File.Create(part))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
                foreach (var path in Directory.EnumerateFiles(world, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(path) == "session.lock") continue;
                    var entry = archive.CreateEntry(Path.Join(name, Path.GetRelativePath(world, path)).Replace('\\', '/'), CompressionLevel.Fastest);
                    using var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var dst = entry.Open();
                    src.CopyTo(dst);
                }
            File.Move(part, zip, true);
        }
        finally { File.Delete(part); }
        Prune(dir, keep);
        return zip;
    }

    // Supprime les copies les plus anciennes au-delà de "keep".
    public static void Prune(string dir, int keep)
    {
        foreach (var old in new DirectoryInfo(dir).GetFiles("*.zip").OrderByDescending(f => f.LastWriteTimeUtc).Skip(Math.Max(1, keep)))
            old.Delete();
    }
}
