// Règles de !ASWorld : qui peut quoi, et ce que chaque commande change dans les réglages du serveur.
public class AsWorldRulesTests
{
    static AsWorldCommand Cmd(string action, string? arg = null) => new("Dylan", action, arg);

    [Fact]
    public void Un_non_operateur_ne_change_rien()
    {
        var config = new ServerConfig { AutoBackup = true, BackupMinutes = 30 };
        var (reply, changed) = ChatCommands.Apply(Cmd("disable"), isOp: false, config);
        Assert.False(changed);
        Assert.True(config.AutoBackup);
        Assert.Equal("Commande réservée aux opérateurs.", reply);
    }

    [Fact]
    public void Disable_coupe_les_sauvegardes()
    {
        var config = new ServerConfig { AutoBackup = true };
        var (reply, changed) = ChatCommands.Apply(Cmd("disable"), true, config);
        Assert.True(changed);
        Assert.False(config.AutoBackup);
        Assert.Equal("Sauvegardes automatiques désactivées.", reply);
    }

    [Fact]
    public void Enable_reactive_les_sauvegardes_avec_l_intervalle_actuel()
    {
        var config = new ServerConfig { AutoBackup = false, BackupMinutes = 45 };
        var (reply, changed) = ChatCommands.Apply(Cmd("enable"), true, config);
        Assert.True(changed);
        Assert.True(config.AutoBackup);
        Assert.Equal("Sauvegardes automatiques activées, toutes les 45 min.", reply);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("15", 15)]
    [InlineData("1440", 1440)]
    public void Set_change_l_intervalle(string arg, int minutes)
    {
        var config = new ServerConfig { BackupMinutes = 30 };
        var (_, changed) = ChatCommands.Apply(Cmd("set", arg), true, config);
        Assert.True(changed);
        Assert.Equal(minutes, config.BackupMinutes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("1441")]
    [InlineData("-5")]
    [InlineData("trente")]
    public void Set_refuse_un_intervalle_invalide_sans_rien_changer(string? arg)
    {
        var config = new ServerConfig { BackupMinutes = 30 };
        var (reply, changed) = ChatCommands.Apply(Cmd("set", arg), true, config);
        Assert.False(changed);
        Assert.Equal(30, config.BackupMinutes);
        Assert.StartsWith("Utilisation : !ASWorld set <minutes>", reply);
    }

    [Theory]
    [InlineData("")]
    [InlineData("status")]
    [InlineData("n_importe_quoi")]
    public void Sans_action_connue_affiche_l_etat_et_l_aide(string action)
    {
        var config = new ServerConfig { AutoBackup = false, BackupMinutes = 20 };
        var (reply, changed) = ChatCommands.Apply(Cmd(action), true, config);
        Assert.False(changed);
        Assert.StartsWith("Sauvegardes désactivées, toutes les 20 min.", reply);
        Assert.Contains("!ASWorld set <minutes>", reply);
    }
}
