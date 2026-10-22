static class Radmin
{
    // Installé = son service existe ; l'interface est dans le même dossier que le service, où qu'il soit installé.
    public static string? GuiPath()
    {
        var image = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\RvControlSvc")?.GetValue("ImagePath") as string;
        var service = image == null ? null : Regex.Match(image, "^\"?([^\"]+?\\.exe)").Groups[1].Value;
        var gui = string.IsNullOrEmpty(service) ? null : Path.Combine(Path.GetDirectoryName(service)!, "RvRvpnGui.exe");
        return gui != null && File.Exists(gui) ? gui : null;
    }

    // Carte réseau Radmin connectée avec une adresse IPv4 (26.x.x.x).
    public static bool Connected() => NetworkInterface.GetAllNetworkInterfaces().Any(n =>
        n.Description.Contains("Radmin VPN") && n.OperationalStatus == OperationalStatus.Up &&
        n.GetIPProperties().UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork));
}
