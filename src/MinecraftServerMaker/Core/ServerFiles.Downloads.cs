// Téléchargements vérifiés (SHA-1, fichiers .part) et installation des archives.
static partial class ServerFiles
{
    // Assez long pour un server pack de plus d'1 Go, mais une connexion figée finit en erreur au lieu de bloquer l'app.
    public static HttpClient Downloader() => new() { Timeout = TimeSpan.FromMinutes(30) };

    // SHA-1 annoncé par CurseForge pour un fichier (algo 1), ou null s'il n'est pas fourni.
    public static string? Sha1Of(JsonNode f) =>
        f["hashes"]?.AsArray().FirstOrDefault(h => h?["algo"]?.GetValue<int>() == 1)?["value"]?.GetValue<string>();

    // Télécharge dans un .part, vérifie le SHA-1 s'il est connu, puis renomme :
    // jamais de fichier tronqué ou altéré à l'emplacement final.
    public static async Task Download(HttpClient http, string url, string path, string? sha1, CancellationToken ct = default)
    {
        var part = path + ".part";
        try
        {
            await using (var src = await http.GetStreamAsync(url, ct))
            await using (var dst = File.Create(part))
                await src.CopyToAsync(dst, ct);
            if (sha1 != null)
            {
                await using var read = File.OpenRead(part);
                var actual = Convert.ToHexString(await SHA1.HashDataAsync(read, ct));
                if (!actual.Equals(sha1, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"Fichier corrompu ou modifié (SHA-1 différent de CurseForge) : {Path.GetFileName(path)}");
            }
            File.Move(part, path, true);
        }
        finally { File.Delete(part); }
    }

    public static async Task DownloadMods(IEnumerable<JsonNode> files, string modsDir)
    {
        Directory.CreateDirectory(modsDir);
        using var http = Downloader();
        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (f, ct) =>
        {
            var name = Path.GetFileName(f["fileName"]!.GetValue<string>()); // GetFileName : pas de chemin venant de l'API
            if (name == "") throw new IOException("Nom de fichier vide renvoyé par CurseForge.");
            await Download(http, CurseForge.DownloadUrl(f), Path.Combine(modsDir, name), Sha1Of(f), ct);
        });
    }

    public static async Task Install(string url, string dir, string? sha1 = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dir)!);
        var zip = dir + ".zip";
        try
        {
            using (var http = Downloader())
                await Download(http, url, zip, sha1);
            await Task.Run(() => ZipFile.ExtractToDirectory(zip, dir)); // refuse les chemins qui sortent du dossier
        }
        catch
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true); // installation partielle, créée par nous
            throw;
        }
        finally { File.Delete(zip); }
    }
}
