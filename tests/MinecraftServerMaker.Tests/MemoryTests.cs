public class MemoryTests
{
    [Theory]
    [InlineData(16310, 8118)] // PC de 16 Go : 8 Go restent à Windows et au jeu
    [InlineData(32768, 24576)]
    [InlineData(9216, 1024)]
    [InlineData(8192, 1024)]  // petit PC : on garde quand même 1 Go pour le serveur
    [InlineData(4096, 1024)]
    public void SafeMaxMb_laisse_8_Go_au_PC(long totalMb, int expected) =>
        Assert.Equal(expected, Memory.SafeMaxMb(totalMb));

    [Theory]
    [InlineData(6144, "-Xms1024M -Xmx6144M")]
    [InlineData(512, "-Xms512M -Xmx512M")] // la mémoire de départ ne dépasse jamais le maximum
    public void JavaOptions_fixe_le_maximum_de_RAM(int ramMb, string expected) =>
        Assert.Equal(expected, Memory.JavaOptions(ramMb));

    [Fact]
    public void TotalMb_lit_une_quantite_de_RAM_plausible() =>
        Assert.InRange(Memory.TotalMb(), 1024, 4L * 1024 * 1024);

    [Fact]
    public void StartInfo_impose_la_RAM_choisie_via_JAVA_OPTIONS()
    {
        var psi = ServerFiles.StartInfo(@"C:\serveur\run.bat", null, ramMb: 6144);
        Assert.Equal("-Xms1024M -Xmx6144M", psi.Environment["_JAVA_OPTIONS"]);
    }

    [Fact]
    public void StartInfo_laisse_le_modpack_choisir_sans_reglage()
    {
        var env = ServerFiles.StartInfo(@"C:\serveur\run.bat", null).Environment;
        Assert.Equal(Environment.GetEnvironmentVariable("_JAVA_OPTIONS"), env.TryGetValue("_JAVA_OPTIONS", out var v) ? v : null);
    }

    [Fact]
    public void ServerConfig_est_relu_depuis_le_dossier_du_serveur()
    {
        using var tmp = new TempDir();
        new ServerConfig { RamMb = 7168 }.Save(tmp.Path);
        Assert.Equal(7168, ServerConfig.Load(tmp.Path).RamMb);
        Assert.False(File.Exists(Path.Combine(tmp.Path, "minecraft-server-maker.json.tmp")));
    }

    [Fact]
    public void ServerConfig_absent_ou_abime_donne_les_valeurs_par_defaut()
    {
        using var tmp = new TempDir();
        Assert.Equal(0, ServerConfig.Load(tmp.Path).RamMb);
        tmp.File("minecraft-server-maker.json", "{ pas du json");
        Assert.Equal(0, ServerConfig.Load(tmp.Path).RamMb);
    }
}
