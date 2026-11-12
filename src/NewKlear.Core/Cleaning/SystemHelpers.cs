using System.Diagnostics;
using System.ServiceProcess;
using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Cleaning;

public static class ServiceHelper
{
    /// <summary>Stops a service and returns true if it was running (so it can be restarted later).</summary>
    public static async Task<bool> StopAsync(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending) return false;
            if (!sc.CanStop) return false;
            sc.Stop();
            await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)));
            Log.Info($"Stopped service {name}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not stop service {name}: {ex.Message}");
            return false;
        }
    }

    public static async Task StartAsync(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status == ServiceControllerStatus.Running) return;
            sc.Start();
            await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)));
            Log.Info($"Started service {name}");
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not start service {name}: {ex.Message}");
        }
    }
}

public static class ShellHelper
{
    /// <summary>
    /// Restarts Windows Explorer. When NewKlear is elevated, Explorer is relaunched through a one-off
    /// scheduled task with a limited token so the taskbar never ends up running as administrator.
    /// </summary>
    public static async Task RestartExplorerAsync()
    {
        var session = Process.GetCurrentProcess().SessionId;
        foreach (var p in Process.GetProcessesByName("explorer").Where(p => p.SessionId == session))
        {
            try { p.Kill(); p.WaitForExit(5000); } catch { /* ignore */ }
        }

        await Task.Delay(1500);
        if (Process.GetProcessesByName("explorer").Any(p => p.SessionId == session)) return; // auto-restarted

        if (!Elevation.IsAdministrator)
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
            return;
        }

        const string task = "NewKlear_RestartExplorer";
        var schtasks = ProcessRunner.System32("schtasks.exe");
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        await ProcessRunner.RunAsync(schtasks, $"/Create /TN {task} /TR \"{explorer}\" /SC ONCE /ST 00:00 /RL LIMITED /IT /F");
        await ProcessRunner.RunAsync(schtasks, $"/Run /TN {task}");
        await Task.Delay(1500);
        await ProcessRunner.RunAsync(schtasks, $"/Delete /TN {task} /F");

        if (!Process.GetProcessesByName("explorer").Any(p => p.SessionId == session))
            Process.Start(new ProcessStartInfo(explorer) { UseShellExecute = true }); // last resort
        Log.Info("Explorer restarted");
    }

    public static long SystemDriveFreeBytes()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory)!;
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch { return 0; }
    }
}
