static class Memory
{
    // Ce que Windows laisse aux applications : la RAM physique du PC (ou la limite imposée au processus).
    public static long TotalMb() => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);

    // Maximum sans danger pour le PC : on laisse 8 Go à Windows et au jeu lancé sur le même PC, avec au moins 1 Go pour le serveur.
    public static int SafeMaxMb(long totalMb) => (int)Math.Max(1024, totalMb - 8192);

    // Options Java qui imposent la RAM du serveur. Passées via _JAVA_OPTIONS, elles l'emportent sur les -Xms/-Xmx des scripts du modpack.
    public static string JavaOptions(int ramMb) => $"-Xms{Math.Min(1024, ramMb)}M -Xmx{ramMb}M";
}
