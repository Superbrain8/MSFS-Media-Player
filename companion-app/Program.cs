using System.Windows.Forms;

namespace MsfsMediaPlayer.Companion;

internal static class Program
{
    // Stable mutex name keeps the app single-instance across user sessions.
    private const string SingleInstanceMutexName = "Global\\MsfsMediaPlayerCompanion";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "MSFS Media Player companion is already running. Check the system tray.",
                "Already running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Log.Init();
        Log.Info("Companion starting (v0.1.0)");

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());

        Log.Info("Companion exited");
    }
}
