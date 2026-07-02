using ShieldAV.Engine;
using ShieldAV.Forms;

namespace ShieldAV;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Load saved settings (VirusTotal API key etc.) before UI starts
        HashScanner.LoadSettings();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.Run(new MainForm());
    }
}
