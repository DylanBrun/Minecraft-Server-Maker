static class Java
{
    // Limite : table fixe des versions Java de Mojang ; à compléter si une future version de MC exige plus récent.
    public static int MajorFor(string mcVersion)
    {
        var v = Version.TryParse(mcVersion, out var x) ? x : new Version(99, 0);
        return v < new Version(1, 17) ? 8 : v < new Version(1, 20, 5) ? 17 : v < new Version(26, 0) ? 21 : 25;
    }

    // Télécharge une fois un JRE Temurin 64 bits (le Java du PC peut être absent, 32 bits ou de la mauvaise version).
    public static async Task<string> Ensure(int major)
    {
        var home = Path.Combine(Settings.DataDir, "java", major.ToString());
        if (!Directory.Exists(home))
            await ServerFiles.Install($"https://api.adoptium.net/v3/binary/latest/{major}/ga/windows/x64/jre/hotspot/normal/eclipse", home);
        return Path.GetDirectoryName(Directory.GetFiles(home, "java.exe", SearchOption.AllDirectories).First())!;
    }
}
