using System.Windows;
using Application = System.Windows.Application;
using PatchGUIlite.Core;

namespace PatchGUIlite
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!RuntimeChecker.EnsureWindowsDesktopRuntime())
            {
                Shutdown();
            }
        }
    }
}

