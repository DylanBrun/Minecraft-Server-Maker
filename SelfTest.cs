using static ServerFiles;

// Auto-test lancé par « MinecraftServerMaker.exe --selftest » : vérifie la logique sans réseau.
static class SelfTest
{
    public static void Run()
    {

        var tmp = Path.Combine(Path.GetTempPath(), "mcst-" + Guid.NewGuid());
        var inner = Directory.CreateDirectory(Path.Combine(tmp, "Pack Server")).FullName;
        foreach (var f in new[] { "run.bat", "startserver.bat", "install.bat" }) File.WriteAllText(Path.Combine(inner, f), "");
        try
        {
            Check(Unwrap(tmp) == inner, "unwrap");
            Check(Path.GetFileName(FindStartScript(inner)) == "startserver.bat", "script préféré");
            File.Delete(Path.Combine(inner, "startserver.bat"));
            Check(Path.GetFileName(FindStartScript(inner)) == "run.bat", "script suivant");
            File.Delete(Path.Combine(inner, "run.bat"));
            Check(FindStartScript(inner) == null, "aucun lanceur");
        }
        finally { Directory.Delete(tmp, true); }
    }

    static void Check(bool ok, string what) { if (!ok) throw new Exception("selftest échoué : " + what); }
}
