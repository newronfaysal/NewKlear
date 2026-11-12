using System.Windows;
using System.Windows.Threading;
using NewKlear.App.Services;
using NewKlear.App.ViewModels;
using NewKlear.Core;
using NewKlear.Core.Infrastructure;

namespace NewKlear.App;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        Settings = AppSettings.Load();
        Log.Info($"NewKlear started. Admin: {Elevation.IsAdministrator}. OS: {Core.SystemInfo.SystemInfoService.OsName}");

        ThemeService.Apply(Settings.ThemeId);

        var main = new MainViewModel();
        var window = new MainWindow { DataContext = main };
        MainWindow = window;

        // Developer option: render off-screen without stealing focus (used for automated screenshots).
        if (e.Args.Contains("--offscreen"))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = 0;
            window.ShowActivated = false;
        }
        window.Show();

        // Optional: --page <name> opens a specific page (handy for shortcuts and screenshots).
        var pageArg = Array.IndexOf(e.Args, "--page");
        if (pageArg >= 0 && pageArg + 1 < e.Args.Length)
        {
            var target = main.NavItems.Concat(main.FooterItems)
                .FirstOrDefault(n => n.Title.StartsWith(e.Args[pageArg + 1], StringComparison.OrdinalIgnoreCase));
            Log.Info($"Start page requested: {e.Args[pageArg + 1]} -> {target?.Title ?? "not found"}");
            if (target != null) main.NavigateTo(target.Page);
        }
        if (e.Args.Contains("--theme") && Array.IndexOf(e.Args, "--theme") + 1 < e.Args.Length)
            ThemeService.Apply(e.Args[Array.IndexOf(e.Args, "--theme") + 1]);

        // Developer option: --snapshot <file.png> [--delay seconds] renders the window to a PNG and exits.
        var snapArg = Array.IndexOf(e.Args, "--snapshot");
        if (snapArg >= 0 && snapArg + 1 < e.Args.Length)
        {
            var file = e.Args[snapArg + 1];
            var delayArg = Array.IndexOf(e.Args, "--delay");
            var delay = delayArg >= 0 && delayArg + 1 < e.Args.Length && double.TryParse(e.Args[delayArg + 1], out var d) ? d : 5;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    var root = (FrameworkElement)window.Content;
                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96,
                        System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(root);
                    var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                    using var fs = File.Create(file);
                    enc.Save(fs);
                }
                catch (Exception ex)
                {
                    Log.Error("Snapshot failed", ex);
                }
                Shutdown();
            };
            timer.Start();
        }
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled UI exception", e.Exception);
        MessageBox.Show($"Something went wrong:\n\n{e.Exception.Message}\n\nDetails were written to the log.", "NewKlear",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Settings.Save();
        Log.Info("NewKlear closed");
        base.OnExit(e);
    }
}
