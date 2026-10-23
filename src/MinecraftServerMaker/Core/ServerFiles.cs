// Dossiers des serveurs : scripts de démarrage, loader, crashs.
static partial class ServerFiles
{
    // Type de loader CurseForge du serveur (1 Forge, 4 Fabric, 5 Quilt, 6 NeoForge ; 0 inconnu), d'après ses fichiers.
    public static int LoaderOf(string root)
    {
        bool Has(string pattern) => Directory.Exists(root) && Directory.GetFiles(root, pattern).Length > 0;
        bool Dir(params string[] parts) => Directory.Exists(Path.Combine([root, .. parts]));
        if (Dir("libraries", "net", "neoforged")) return 6;
        if (Dir("libraries", "net", "minecraftforge") || Has("forge-*.jar")) return 1;
        if (Has("fabric-server*.jar") || Dir(".fabric")) return 4;
        if (Has("quilt-server*.jar") || Dir(".quilt")) return 5;
        return 0;
    }

    public static readonly Dictionary<int, string> LoaderNames = new() { [1] = "Forge", [4] = "Fabric", [5] = "Quilt", [6] = "NeoForge" };

    // Forge : "Crash report saved to", vanilla : "This crash report has been saved to".
    public static bool IsCrash(string? line) =>
        line != null && (line.Contains("rash report") && line.Contains("saved to") || line.Contains("Failed to start the minecraft server"));

    // nogui : pas de fenêtre "Minecraft server" (transmis au jar par les scripts qui relaient %*, comme le run.bat de Forge).
    // ramMb > 0 : RAM imposée au serveur, quels que soient les réglages du modpack.
    public static ProcessStartInfo StartInfo(string script, string? javaBin, bool nogui = false, int ramMb = 0)
    {
        // Guillemets extérieurs : cmd /c retire la première et la dernière, il reste "script.bat" nogui.
        var psi = new ProcessStartInfo("cmd.exe", $"/c \"\"{Path.GetFileName(script)}\"{(nogui ? " nogui" : "")}\"")
        {
            WorkingDirectory = Path.GetDirectoryName(script),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        psi.Environment.Remove("NoDefaultCurrentDirectoryInExePath"); // sinon cmd ne trouve ni start.bat ni les "call settings.bat"
        if (javaBin != null)
        {
            psi.Environment["PATH"] = javaBin + ";" + psi.Environment["PATH"];
            psi.Environment["JAVA_HOME"] = Path.GetDirectoryName(javaBin);
        }
        if (ramMb > 0) psi.Environment["_JAVA_OPTIONS"] = Memory.JavaOptions(ramMb);
        return psi;
    }

    // Version de MC d'après le nom de dossier créé par l'app : "<slug>-<version>" ou "<slug>-<version>-<fileId>".
    public static string VersionOf(string dir) =>
        Regex.Match(Path.GetFileName(dir), @"-(\d+\.\d+(?:\.\d+)?)(?:-\d+)?$").Groups[1].Value;

    // Les server packs mettent souvent tout dans un unique dossier racine.
    public static string Unwrap(string dir)
    {
        while (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir) is [var only]) dir = only;
        return dir;
    }

    // shortcut: seuls les lanceurs .bat sont gérés ; un pack livré avec juste un jar/installeur demande un script fait main.
    public static string? FindStartScript(string dir)
    {
        if (!Directory.Exists(dir)) return null;
        var bats = Directory.GetFiles(dir, "*.bat");
        string[] preferred = ["startserver.bat", "start-server.bat", "start_server.bat", "start.bat", "run.bat", "serverstart.bat", "launchserver.bat"];
        return preferred.Select(p => bats.FirstOrDefault(b => Path.GetFileName(b).Equals(p, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(b => b != null)
            ?? bats.FirstOrDefault(b => Regex.IsMatch(Path.GetFileName(b), "start|run|launch", RegexOptions.IgnoreCase));
    }
}
