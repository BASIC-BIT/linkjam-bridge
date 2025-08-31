using System;
using System.Threading;
using System.Windows.Forms;

namespace LinkJam.Companion;

static class Program
{
    private static Mutex? _mutex;
    
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        const string mutexName = "LinkJam.Companion.SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool createdNew);
        
        if (!createdNew)
        {
            MessageBox.Show(
                "LinkJam Companion is already running.\nCheck your system tray.", 
                "Already Running", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Information);
            return;
        }
        
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        ApplicationConfiguration.Initialize();
        
        Application.Run(new MainForm());
        
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }    
}