// Lancement, arrêt et sortie console du serveur.
partial class MainForm
{
    void Toggle()
    {
        if (!busy) _ = Run(IsRunning ? StopServer : StartServer);
    }

    async Task StartServer()
    {
        var script = ServerFiles.FindStartScript(settings.ServerDir) ?? throw new Exception("Créez d'abord un serveur.");
        // Anciennes installs sans version connue : Java du système.
        string? javaBin = settings.McVersion == "" ? null : await Java.Ensure(Java.MajorFor(settings.McVersion));
        online = false;
        int ramMb = ServerConfig.Load(settings.ServerDir).RamMb;
        server = Launch(script, javaBin, () => Ui(() => { Log("Serveur arrêté."); UpdateUi(); }), nogui: settings.HideOnStart, ramMb);
        Log($"Démarrage : {script}" + (ramMb > 0 ? $" ({ramMb / 1024.0:0.#} Go de RAM)" : ""));
        if (settings.HideOnStart) HideToTray("Le serveur démarre en arrière-plan.");
        // Le serveur tourne déjà : un souci avec Radmin est signalé sans interrompre la suite.
        if (settings.AutoRadmin)
            try { await EnsureRadmin(); }
            catch (Exception e) { Log("Radmin VPN n'a pas pu être lancé : " + e.Message); }
        _ = ShowAddresses(); // après Radmin, pour que son adresse soit listée
    }

    // Lance un .bat en arrière-plan avec le bon Java en tête du PATH ; sa sortie va dans le journal.
    Process Launch(string script, string? javaBin, Action? onExit = null, bool nogui = false, int ramMb = 0)
    {
        var p = new Process { StartInfo = ServerFiles.StartInfo(script, javaBin, nogui, ramMb), EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => OnServerLine(e.Data);
        p.ErrorDataReceived += (_, e) => OnServerLine(e.Data);
        if (onExit != null) p.Exited += (_, _) => onExit();
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    // Pas d'accès UI ici + ConfigureAwait(false) : on peut faire .Wait() dessus à la fermeture.
    async Task StopServer()
    {
        if (server is not { HasExited: false } p) return;
        try { p.StandardInput.WriteLine("stop"); p.StandardInput.Close(); } catch (IOException) { }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await p.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } // s'est arrêté entre-temps
        }
    }

    void OnServerLine(string? line)
    {
        if (line == null) return;
        if (ServerFiles.IsCrash(line) && server is { HasExited: false } crashed)
        {
            try { crashed.StandardInput.Close(); } catch (IOException) { } // débloque le "pause" final du .bat
            // La JVM peut rester bloquée après un crash ; on lui laisse le temps de sauvegarder puis on la ferme.
            _ = Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith(_ =>
            {
                try { if (!crashed.HasExited) crashed.Kill(true); } catch (InvalidOperationException) { } // s'est arrêté entre-temps
            });
            Ui(() => Log("Le serveur a planté : voir le dossier crash-reports du serveur."));
        }
        Ui(() =>
        {
            Log(line, fromServer: true);
            if (!online && line.Contains("Done (") && line.Contains("help"))
            {
                online = true;
                tray.ShowBalloonTip(3000, "Serveur Minecraft", "Le serveur est en ligne.", ToolTipIcon.Info);
                UpdateUi();
            }
        });
    }
}
