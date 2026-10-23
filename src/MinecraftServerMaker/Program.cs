static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--selftest")) { SelfTest.Run(); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
