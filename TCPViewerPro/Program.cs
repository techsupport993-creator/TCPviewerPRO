using System;
using System.Windows.Forms;

namespace TCPViewerPro;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (SelfUpdateBootstrapper.TryHandle(args)) return;
        Application.Run(new MainForm());
    }
}
