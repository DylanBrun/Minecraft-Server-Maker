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
        Check(CurseForge.SlugFromLink("https://www.curseforge.com/minecraft/mc-mods/jei", "mc-mods") == "jei", "lien de mod");
        Check(CurseForge.SlugFromLink("https://www.curseforge.com/minecraft/mc-mods/jei") == null, "lien de mod pris pour un modpack");
        Check(Fingerprint.Of([]) == 0x5bd15e36, "empreinte vide");
        Check(Fingerprint.Of("a b\r\nc"u8.ToArray()) == Fingerprint.Of("abc"u8.ToArray()), "empreinte ignore les blancs");
        Check(CurseForge.Slugify("Abyssal Ascent") == "abyssal-ascent", "slug simple");
        Check(CurseForge.Slugify(" All the Mods 9 - ATM9 ") == "all-the-mods-9-atm9", "slug avec tirets");
        Check(!IsCrash("[Server thread/INFO]: Done (6.407s)! For help, type \"help\""), "pas un crash");
        Check(ServerBuilder.LoaderPartsValid("47.4.6", "1.20.1") && ServerBuilder.LoaderPartsValid("0.16.10", "1.21") && ServerBuilder.LoaderPartsValid("21.1.77-beta", "1.21.1"), "versions de loader valides");
        Check(!ServerBuilder.LoaderPartsValid("../../evil", "1.20.1") && !ServerBuilder.LoaderPartsValid("47.4.6", "1.20.1/../x") && !ServerBuilder.LoaderPartsValid("1?x=y", "1.20.1"), "versions de loader piégées refusées");
        Check(Sha1Of(JsonNode.Parse("""{"hashes":[{"value":"md5","algo":2},{"value":"abc123","algo":1}]}""")!) == "abc123", "sha1 lu");
        Check(Sha1Of(JsonNode.Parse("""{"hashes":[]}""")!) == null, "sha1 absent");
        var protectedKey = new Settings { ApiKey = "$2a$10$test" }.ApiKeyProtected;
        Check(!protectedKey.Contains("test") && new Settings { ApiKeyProtected = protectedKey }.ApiKey == "$2a$10$test", "clé chiffrée puis relue");
        Check(JsonSerializer.Deserialize<Settings>("""{"ApiKey":"ancienne"}""")!.ApiKey == "ancienne", "ancienne clé en clair relue");
        Check(!JsonSerializer.Serialize(new Settings { ApiKey = "secret" }).Contains("secret"), "clé jamais écrite en clair");

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
            Check(LoaderOf(inner) == 0, "loader inconnu");
            File.WriteAllText(Path.Combine(inner, "fabric-server-launch.jar"), "");
            Check(LoaderOf(inner) == 4, "loader fabric");
            Directory.CreateDirectory(Path.Combine(inner, "libraries", "net", "minecraftforge"));
            Check(LoaderOf(inner) == 1, "loader forge");
            Directory.CreateDirectory(Path.Combine(inner, "libraries", "net", "neoforged"));
            Check(LoaderOf(inner) == 6, "loader neoforge");
        }
        finally { Directory.Delete(tmp, true); }
    }

    static void Check(bool ok, string what) { if (!ok) throw new Exception("selftest échoué : " + what); }
}
