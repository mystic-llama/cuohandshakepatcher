using System.Windows;

namespace CuoHandshakePatcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        base.OnStartup(e);
    }
}
