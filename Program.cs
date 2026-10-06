namespace GamepadLauncher;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // не даём запустить вторую копию
        using var mutex = new Mutex(true, @"Local\GamepadLauncher_SingleInstance", out bool isNew);
        if (!isNew) return;

        ApplicationConfiguration.Initialize();
        bool startHidden = args.Contains("--minimized");
        Application.Run(new MainForm(startHidden));
    }
}
