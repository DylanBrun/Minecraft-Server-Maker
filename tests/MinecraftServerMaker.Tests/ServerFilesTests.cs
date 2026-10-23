public class ServerFilesTests
{
    [Theory]
    [InlineData(@"[main/FATAL] [ne.mi.se.lo.ServerModLoader/]: Crash report saved to .\crash-reports\x.txt")]
    [InlineData(@"This crash report has been saved to: C:\x.txt")]
    [InlineData("[main/ERROR] [minecraft/Main]: Failed to start the minecraft server")]
    public void IsCrash_reconnait_un_plantage(string line) => Assert.True(ServerFiles.IsCrash(line));

    [Theory]
    [InlineData("[Server thread/INFO]: Done (6.407s)! For help, type \"help\"")]
    [InlineData("<Dylan> le crash report de hier")]
    [InlineData(null)]
    public void IsCrash_ignore_les_lignes_normales(string? line) => Assert.False(ServerFiles.IsCrash(line));

    [Theory]
    [InlineData("cobblemon-fabric-1.21.1", "1.21.1")]
    [InlineData("all-the-mods-9-1.20.1", "1.20.1")]
    [InlineData("abyssal-ascent-1.20.1-8950247", "1.20.1")]
    [InlineData("skyfactory-4-1.12.2", "1.12.2")]
    [InlineData("pack-26.3", "26.3")]
    public void VersionOf_lit_la_version_dans_le_nom_du_dossier(string dir, string mc) =>
        Assert.Equal(mc, ServerFiles.VersionOf(@"C:\servers\" + dir));

    [Fact]
    public void Unwrap_descend_dans_le_dossier_unique_du_server_pack()
    {
        using var tmp = new TempDir();
        var inner = tmp.Dir("Pack Server", "server");
        tmp.File(@"Pack Server\server\run.bat");
        Assert.Equal(inner, ServerFiles.Unwrap(tmp.Path));
    }

    [Fact]
    public void FindStartScript_prefere_les_lanceurs_connus_puis_un_nom_proche()
    {
        using var tmp = new TempDir();
        tmp.File("install.bat");
        tmp.File("run.bat");
        tmp.File("startserver.bat");
        Assert.Equal("startserver.bat", Path.GetFileName(ServerFiles.FindStartScript(tmp.Path)));

        File.Delete(Path.Combine(tmp.Path, "startserver.bat"));
        Assert.Equal("run.bat", Path.GetFileName(ServerFiles.FindStartScript(tmp.Path)));

        File.Delete(Path.Combine(tmp.Path, "run.bat"));
        tmp.File("Lancer le serveur.bat");
        Assert.Null(ServerFiles.FindStartScript(tmp.Path)); // "install" n'est pas un lanceur, et "Lancer" ne correspond à aucun motif
    }

    [Fact]
    public void FindStartScript_renvoie_null_pour_un_dossier_absent() =>
        Assert.Null(ServerFiles.FindStartScript(@"C:\n-existe\pas"));

    [Fact]
    public void LoaderOf_detecte_le_loader_d_apres_les_fichiers()
    {
        using var tmp = new TempDir();
        Assert.Equal(0, ServerFiles.LoaderOf(tmp.Path));
        tmp.File("fabric-server-launch.jar");
        Assert.Equal(4, ServerFiles.LoaderOf(tmp.Path));
        tmp.Dir("libraries", "net", "minecraftforge");
        Assert.Equal(1, ServerFiles.LoaderOf(tmp.Path));
        tmp.Dir("libraries", "net", "neoforged");
        Assert.Equal(6, ServerFiles.LoaderOf(tmp.Path));
    }

    [Fact]
    public void StartInfo_met_le_bon_Java_en_tete_et_ajoute_nogui()
    {
        var psi = ServerFiles.StartInfo(@"C:\serveur\run.bat", @"C:\java\21\bin", nogui: true);
        Assert.Equal(@"/c """"run.bat"" nogui""", psi.Arguments);
        Assert.Equal(@"C:\serveur", psi.WorkingDirectory);
        Assert.StartsWith(@"C:\java\21\bin;", psi.Environment["PATH"]);
        Assert.Equal(@"C:\java\21", psi.Environment["JAVA_HOME"]);
        Assert.False(psi.Environment.ContainsKey("NoDefaultCurrentDirectoryInExePath"));
    }

    [Fact]
    public void StartInfo_sans_nogui_ni_Java_laisse_le_PATH_du_systeme()
    {
        var psi = ServerFiles.StartInfo(@"C:\serveur\start.bat", null);
        Assert.Equal(@"/c """"start.bat""""", psi.Arguments);
        Assert.False(psi.Environment.ContainsKey("JAVA_HOME") && psi.Environment["JAVA_HOME"]!.StartsWith(@"C:\java"));
    }
}
