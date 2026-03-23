using System.Threading;
using Microsoft.UI.Xaml;

namespace GameCheatsManager.WinUI;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    public static Window? MainWindowInstance { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstanceMutex = new Mutex(true, "GameCheatsManager.WinUI.Singleton", out var createdNew);
        if (!createdNew)
        {
            Exit();
            return;
        }

        MainWindowInstance = new MainWindow();
        MainWindowInstance.Activate();
    }
}
