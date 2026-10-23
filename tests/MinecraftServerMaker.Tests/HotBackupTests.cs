// Copie à chaud : ordre des commandes envoyées au serveur, attente de l'enregistrement, et save-on garanti.
public class HotBackupTests
{
    [Fact]
    public async Task Le_serveur_enregistre_puis_se_fige_pendant_la_copie()
    {
        var sent = new List<string>();
        string? sentWhenCopying = null;
        var zip = await Backups.CreateWhileRunning(sent.Add, Task.CompletedTask, TimeSpan.FromSeconds(5), () =>
        {
            sentWhenCopying = string.Join(" | ", sent);
            return "monde.zip";
        });
        Assert.Equal("monde.zip", zip);
        Assert.Equal("save-off | save-all flush", sentWhenCopying); // copie après l'enregistrement, avant le save-on
        Assert.Equal(["save-off", "save-all flush", "save-on"], sent);
    }

    [Fact]
    public async Task La_copie_attend_la_fin_de_l_enregistrement()
    {
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool copied = false;
        var backup = Backups.CreateWhileRunning(_ => { }, saved.Task, TimeSpan.FromSeconds(30), () => { copied = true; return "monde.zip"; });
        await Task.Delay(200);
        Assert.False(copied); // le serveur n'a pas encore fini d'enregistrer
        saved.SetResult();
        await backup;
        Assert.True(copied);
    }

    [Fact]
    public async Task Sans_confirmation_du_serveur_la_copie_part_quand_meme_apres_le_delai()
    {
        var never = new TaskCompletionSource().Task;
        var zip = await Backups.CreateWhileRunning(_ => { }, never, TimeSpan.FromMilliseconds(100), () => "monde.zip");
        Assert.Equal("monde.zip", zip);
    }

    [Fact]
    public async Task Save_on_part_meme_si_la_copie_echoue()
    {
        var sent = new List<string>();
        await Assert.ThrowsAsync<IOException>(() => Backups.CreateWhileRunning(sent.Add, Task.CompletedTask, TimeSpan.FromSeconds(5),
            () => throw new IOException("disque plein")));
        Assert.Equal("save-on", sent[^1]);
    }

    [Fact]
    public async Task Copie_a_chaud_complete_sur_un_vrai_monde()
    {
        using var tmp = new TempDir();
        tmp.File(Path.Combine("world", "level.dat"), "données");
        var sent = new List<string>();
        var zip = await Backups.CreateWhileRunning(sent.Add, Task.CompletedTask, TimeSpan.FromSeconds(5),
            () => Backups.Create(tmp.Path, 10, new DateTime(2026, 10, 23, 12, 0, 0)));
        Assert.True(File.Exists(zip));
        Assert.Equal(["save-off", "save-all flush", "save-on"], sent);
    }
}
