using System.IO.Compression;

public class BackupTests
{
    static TempDir ServerWithWorld(string level = "world")
    {
        var tmp = new TempDir();
        tmp.File(Path.Combine(level, "level.dat"), "données du monde");
        tmp.File(Path.Combine(level, "region", "r.0.0.mca"), "région");
        tmp.File(Path.Combine(level, "session.lock"), "verrou");
        return tmp;
    }

    [Fact]
    public void Create_zippe_le_monde_sans_le_verrou_du_serveur()
    {
        using var tmp = ServerWithWorld();
        var zip = Backups.Create(tmp.Path, 10, new DateTime(2026, 10, 23, 14, 30, 5));
        Assert.Equal(Path.Combine(tmp.Path, "backups", "world-2026-10-23_14-30-05.zip"), zip);
        using var archive = ZipFile.OpenRead(zip);
        Assert.Equal(["world/level.dat", "world/region/r.0.0.mca"], archive.Entries.Select(e => e.FullName).Order());
        Assert.Empty(Directory.GetFiles(Path.Combine(tmp.Path, "backups"), "*.part"));
    }

    [Fact]
    public void Create_lit_les_fichiers_que_le_serveur_garde_ouverts()
    {
        using var tmp = ServerWithWorld();
        using var heldByServer = new FileStream(Path.Combine(tmp.Path, "world", "level.dat"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        var zip = Backups.Create(tmp.Path, 10, DateTime.Now);
        using var archive = ZipFile.OpenRead(zip);
        Assert.Contains(archive.Entries, e => e.FullName == "world/level.dat");
    }

    [Fact]
    public void Create_suit_le_nom_du_monde_de_server_properties()
    {
        using var tmp = ServerWithWorld("monde-abyssal");
        tmp.File("server.properties", "motd=Salut\nlevel-name=monde-abyssal\n");
        Assert.StartsWith("monde-abyssal-", Path.GetFileName(Backups.Create(tmp.Path, 10, DateTime.Now)));
    }

    [Fact]
    public void Create_signale_un_monde_absent()
    {
        using var tmp = new TempDir();
        Assert.Throws<DirectoryNotFoundException>(() => Backups.Create(tmp.Path, 10, DateTime.Now));
    }

    [Theory]
    [InlineData("level-name=../../Windows", "Windows")] // jamais de chemin qui sort du serveur
    [InlineData("level-name=", "world")]
    [InlineData("motd=rien", "world")]
    public void WorldName_ne_garde_qu_un_nom_de_dossier(string props, string expected)
    {
        using var tmp = new TempDir();
        tmp.File("server.properties", props);
        Assert.Equal(expected, Backups.WorldName(tmp.Path));
    }

    [Fact]
    public void Prune_garde_les_copies_les_plus_recentes()
    {
        using var tmp = new TempDir();
        for (int i = 0; i < 5; i++)
            File.SetLastWriteTimeUtc(tmp.File($"world-{i}.zip"), DateTime.UtcNow.AddHours(i));
        Backups.Prune(tmp.Path, 2);
        Assert.Equal(["world-3.zip", "world-4.zip"], Directory.GetFiles(tmp.Path).Select(Path.GetFileName).Order());
    }
}
