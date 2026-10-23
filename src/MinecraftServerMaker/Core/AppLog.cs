// Journal de l'app sur disque, un fichier par jour, pour comprendre un problème après coup.
// La sortie brute des serveurs n'y va pas : chaque serveur a déjà ses logs\latest.log et ses crash-reports.
static class AppLog
{
    public static string Dir { get; set; } = Path.Combine(Settings.DataDir, "logs");
    static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(Path.Combine(Dir, $"app-{DateTime.Now:yyyy-MM-dd}.log"), $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // un journal qui échoue ne doit jamais bloquer l'app
    }

    // Supprime les journaux de plus de keepDays jours.
    public static void Prune(int keepDays = 14)
    {
        if (!Directory.Exists(Dir)) return;
        foreach (var f in new DirectoryInfo(Dir).GetFiles("app-*.log").Where(f => f.LastWriteTime < DateTime.Now.AddDays(-keepDays)))
            try { f.Delete(); } catch (IOException) { }
    }
}
