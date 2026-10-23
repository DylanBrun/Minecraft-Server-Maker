// Sauvegardes automatiques du monde et commandes !ASWorld des opérateurs.
partial class MainForm
{
    readonly System.Windows.Forms.Timer backupTimer = new();
    TaskCompletionSource? savedSignal;
    bool backingUp;

    // (Re)lance la minuterie selon les réglages du serveur : quand il passe en ligne ou que les réglages changent.
    void StartBackupTimer()
    {
        backupTimer.Stop();
        var config = ServerConfig.Load(settings.ServerDir);
        if (!config.AutoBackup || !IsRunning) return;
        backupTimer.Interval = config.BackupMinutes * 60_000;
        backupTimer.Start();
    }

    async Task BackupNow()
    {
        if (backingUp || server is not { HasExited: false } p) return;
        backingUp = true;
        var root = settings.ServerDir;
        try
        {
            // Le serveur enregistre tout puis arrête d'écrire le temps de la copie : le zip contient un monde cohérent.
            Send(p, "save-off");
            savedSignal = new();
            Send(p, "save-all flush");
            await Task.WhenAny(savedSignal.Task, Task.Delay(TimeSpan.FromSeconds(60)));
            int keep = ServerConfig.Load(root).BackupsToKeep;
            var zip = await Task.Run(() => Backups.Create(root, keep, DateTime.Now));
            Log($"Sauvegarde du monde : {Path.GetFileName(zip)} ({new FileInfo(zip).Length / (1024 * 1024)} Mo).");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log("Sauvegarde du monde impossible : " + e.Message); }
        finally
        {
            Send(p, "save-on");
            savedSignal = null;
            backingUp = false;
        }
    }

    static void Send(Process p, string command)
    {
        try { if (!p.HasExited) p.StandardInput.WriteLine(command); }
        catch (Exception e) when (e is IOException or InvalidOperationException) { } // serveur en train de s'arrêter
    }

    void HandleAsWorld(AsWorldCommand cmd)
    {
        if (server is not { HasExited: false } p) return;
        var root = settings.ServerDir;
        void Reply(string text) => Send(p, ChatCommands.Tell(cmd.Player, text));
        if (!ChatCommands.IsOp(root, cmd.Player)) { Reply("Commande réservée aux opérateurs."); return; }

        var config = ServerConfig.Load(root);
        switch (cmd.Action)
        {
            case "enable": config.AutoBackup = true; break;
            case "disable": config.AutoBackup = false; break;
            case "set" when int.TryParse(cmd.Argument, out var minutes) && minutes is >= 1 and <= 1440: config.BackupMinutes = minutes; break;
            case "set": Reply("Utilisation : !ASWorld set <minutes>, entre 1 et 1440."); return;
            default:
                Reply($"Sauvegardes {(config.AutoBackup ? "activées" : "désactivées")}, toutes les {config.BackupMinutes} min. " +
                      "Commandes : !ASWorld enable, !ASWorld disable, !ASWorld set <minutes>.");
                return;
        }
        config.Save(root);
        StartBackupTimer();
        var state = config.AutoBackup ? $"activées, toutes les {config.BackupMinutes} min" : "désactivées";
        Reply($"Sauvegardes automatiques {state}.");
        Log($"{cmd.Player} : sauvegardes automatiques {state} (!ASWorld).");
    }
}
