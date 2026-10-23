// Radmin VPN et adresses de connexion.
partial class MainForm
{
    async Task EnsureRadmin()
    {
        var gui = Radmin.GuiPath();
        if (gui == null)
        {
            Log("Radmin VPN n'est pas installé sur ce PC (https://www.radmin-vpn.com) : le serveur démarre sans.");
            tray.ShowBalloonTip(4000, "Radmin VPN introuvable", "Installez Radmin VPN ou désactivez l'option dans Fichier > Paramètres.", ToolTipIcon.Warning);
            return;
        }
        // Lance Radmin s'il est fermé, ou ramène sa fenêtre au premier plan s'il tourne déjà.
        Process.Start(new ProcessStartInfo(gui, "/show") { UseShellExecute = true });
        Log("Radmin VPN ouvert.");
        for (int i = 0; i < 20 && !Radmin.Connected(); i++) await Task.Delay(1000);
        if (Radmin.Connected()) { Log("Radmin VPN est actif."); return; }
        // Son bouton d'alimentation n'a ni commande ni réglage modifiable sans droits administrateur.
        Log("Radmin VPN est éteint : cliquez sur son bouton d'alimentation pour l'activer.");
        tray.ShowBalloonTip(5000, "Radmin VPN est éteint", "Cliquez sur le bouton d'alimentation de Radmin VPN pour l'activer.", ToolTipIcon.Warning);
    }

    async Task ShowAddresses()
    {
        var props = Path.Combine(settings.ServerDir, "server.properties");
        var port = File.Exists(props) ? File.ReadLines(props).FirstOrDefault(l => l.StartsWith("server-port="))?["server-port=".Length..].Trim() : null;
        if (string.IsNullOrEmpty(port)) port = "25565";

        var lines = new List<string> { "Adresses pour se connecter :", $"Ce PC : localhost:{port}" };
        lines.AddRange(NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."))
                .Select(a => $"{n.Name} : {a.Address}:{port}")));
        SetAddresses(lines);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            if (IPAddress.TryParse((await http.GetStringAsync("https://api.ipify.org")).Trim(), out var ip))
            {
                lines.Add($"Internet : {ip}:{port} (si le port {port} est ouvert sur la box)");
                SetAddresses(lines);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { } // hors ligne : on garde les adresses locales
    }

    void SetAddresses(List<string> lines)
    {
        addresses.Text = string.Join(Environment.NewLine, lines);
        addresses.Height = (lines.Count + 1) * addresses.Font.Height;
    }
}
