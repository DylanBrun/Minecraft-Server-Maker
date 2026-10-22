using static ServerFiles;

// Auto-test lancé par « MinecraftServerMaker.exe --selftest » : vérifie la logique sans réseau.
static class SelfTest
{
    public static void Run()
    {
        foreach (var (mc, major) in new[] { ("1.12.2", 8), ("1.16.5", 8), ("1.18.2", 17), ("1.20.1", 17), ("1.20.5", 21), ("1.21.1", 21), ("1.21", 21), ("26.1", 25) })
            Check(Java.MajorFor(mc) == major, "java pour " + mc);
        Check(IsCrash(@"[main/FATAL] [ne.mi.se.lo.ServerModLoader/]: Crash report saved to .\crash-reports\x.txt"), "crash forge");
        Check(IsCrash("This crash report has been saved to: C:\\x.txt"), "crash vanilla");
        foreach (var (name, mc) in new[] { ("cobblemon-fabric-1.21.1", "1.21.1"), ("all-the-mods-9-1.20.1", "1.20.1"), ("abyssal-ascent-1.20.1-8950247", "1.20.1"), ("pack-26.3", "26.3") })
            Check(VersionOf(@"C:\servers\" + name) == mc, "version de " + name);
        foreach (var link in new[] { "https://www.curseforge.com/minecraft/modpacks/abyssal-ascent", "http://curseforge.com/minecraft/modpacks/abyssal-ascent",
                     "www.curseforge.com/minecraft/modpacks/abyssal-ascent/files", "curseforge.com/minecraft/modpacks/abyssal-ascent?page=2",
                     "HTTPS://WWW.CurseForge.com/minecraft/modpacks/Abyssal-Ascent", " https://www.curseforge.com/minecraft/modpacks/abyssal-ascent/ " })
            Check(CurseForge.SlugFromLink(link) == "abyssal-ascent", "lien " + link);
        Check(CurseForge.SlugFromLink("Abyssal Ascent") == null, "nom n'est pas un lien");
        Check(CurseForge.Slugify("Abyssal Ascent") == "abyssal-ascent", "slug simple");
        Check(CurseForge.Slugify(" All the Mods 9 - ATM9 ") == "all-the-mods-9-atm9", "slug avec tirets");
        Check(!IsCrash("[Server thread/INFO]: Done (6.407s)! For help, type \"help\""), "pas un crash");

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
