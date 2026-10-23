// Les tests de cette classe s'exécutent l'un après l'autre : ils modifient le dossier du journal, partagé.
public class AppLogTests
{
    [Fact]
    public void Write_ajoute_une_ligne_horodatee_dans_le_fichier_du_jour()
    {
        using var tmp = new TempDir();
        AppLog.Dir = tmp.Path;
        AppLog.Write("Serveur prêt");
        AppLog.Write("Serveur arrêté.");
        var lines = File.ReadAllLines(Path.Combine(tmp.Path, $"app-{DateTime.Now:yyyy-MM-dd}.log"));
        Assert.Equal(2, lines.Length);
        Assert.Matches(@"^\d\d:\d\d:\d\d Serveur prêt$", lines[0]);
    }

    [Fact]
    public void Write_ne_plante_pas_si_le_dossier_est_inaccessible()
    {
        using var tmp = new TempDir();
        AppLog.Dir = tmp.File("pas-un-dossier"); // un fichier à la place du dossier
        AppLog.Write("message perdu, mais sans erreur");
    }

    [Fact]
    public void Prune_supprime_seulement_les_vieux_journaux()
    {
        using var tmp = new TempDir();
        AppLog.Dir = tmp.Path;
        var old = tmp.File("app-2026-01-01.log", "vieux");
        File.SetLastWriteTime(old, DateTime.Now.AddDays(-30));
        var recent = tmp.File("app-2026-10-20.log", "récent");
        var other = tmp.File("autre.txt", "pas un journal");
        File.SetLastWriteTime(other, DateTime.Now.AddDays(-30));

        AppLog.Prune(14);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(other));
    }
}
