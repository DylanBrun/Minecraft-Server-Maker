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
        savedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            int keep = ServerConfig.Load(root).BackupsToKeep;
            var zip = await Backups.CreateWhileRunning(c => Send(p, c), savedSignal.Task, TimeSpan.FromSeconds(60),
                () => Backups.Create(root, keep, DateTime.Now));
            Log($"Sauvegarde du monde : {Path.GetFileName(zip)} ({new FileInfo(zip).Length / (1024 * 1024)} Mo).");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log("Sauvegarde du monde impossible : " + e.Message); }
        finally
        {
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
        var config = ServerConfig.Load(root);
        var (reply, changed) = ChatCommands.Apply(cmd, ChatCommands.IsOp(root, cmd.Player), config);
        if (changed)
        {
            config.Save(root);
            StartBackupTimer();
            Log($"{cmd.Player} (!ASWorld) : {reply}");
        }
        Send(p, ChatCommands.Tell(cmd.Player, reply));
    }
}
