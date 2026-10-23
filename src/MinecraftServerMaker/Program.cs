static class Program
{
    [STAThread]
    static void Main()
    {
        // Une erreur imprévue est notée dans le journal avant d'être affichée, pour pouvoir la retrouver ensuite.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            AppLog.Write("ERREUR IMPRÉVUE : " + e.Exception);
            MessageBox.Show(e.Exception.Message, "Erreur imprévue", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Write("ERREUR FATALE : " + e.ExceptionObject);
        AppLog.Prune();
        AppLog.Write("Démarrage de Minecraft Server Maker");
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
