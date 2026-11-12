using Microsoft.Win32;
using NewKlear.Core.Cleaning;

namespace NewKlear.Core.Tweaks;

/// <summary>All optimization settings. Each one is a well-known, documented Windows setting — no placebo tweaks.</summary>
public static class TweakCatalog
{
    private const RegistryHive CU = RegistryHive.CurrentUser;
    private const RegistryHive LM = RegistryHive.LocalMachine;
    private const RegistryValueKind DW = RegistryValueKind.DWord;
    private const RegistryValueKind SZ = RegistryValueKind.String;

    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    private static RegValue D(RegistryHive h, string path, string name, int applied, int? def) => new(h, path, name, DW, applied, def);
    private static RegValue S(RegistryHive h, string path, string name, string applied, string? def) => new(h, path, name, SZ, applied, def);

    public static IReadOnlyList<ITweak> Build() =>
        Performance().Concat(Privacy()).Concat(ExplorerTweaks()).Concat(Services()).Where(t => t.IsAvailable).ToList();

    // ------------------------------------------------------------ Performance
    private static IEnumerable<ITweak> Performance()
    {
        yield return new RegistryTweak
        {
            Id = "perf.visualfx", Name = "Reduce visual effects", Category = TweakCategory.Performance, Effect = ApplyEffect.SignOut,
            Description = "Turns off window animations, taskbar animations, shadows and Peek. Keeps smooth fonts and thumbnails. Noticeable on older or low-power PCs.",
            Values =
            [
                D(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3, null),
                new(CU, @"Control Panel\Desktop", "UserPreferencesMask", RegistryValueKind.Binary,
                    new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 }, new byte[] { 0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 }),
                S(CU, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", "1"),
                D(CU, Advanced, "TaskbarAnimations", 0, 1),
                D(CU, Advanced, "ListviewShadow", 0, 1),
                D(CU, @"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0, 1),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "perf.transparency", Name = "Turn off transparency effects", Category = TweakCategory.Performance,
            Description = "Disables the blur/acrylic effect on Start, taskbar and windows. Saves GPU work on integrated graphics.",
            Values = [D(CU, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1)],
        };
        yield return new RegistryTweak
        {
            Id = "perf.menudelay", Name = "Faster menus", Category = TweakCategory.Performance, Effect = ApplyEffect.SignOut,
            Description = "Shortens the delay before sub-menus open from 400 ms to 100 ms.",
            Values = [S(CU, @"Control Panel\Desktop", "MenuShowDelay", "100", "400")],
        };
        yield return new RegistryTweak
        {
            Id = "perf.startupdelay", Name = "Remove startup app delay", Category = TweakCategory.Performance,
            Description = "Windows waits about 10 seconds after sign-in before starting startup apps. This removes that wait.",
            Values = [D(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0, null)],
        };
        yield return new RegistryTweak
        {
            Id = "perf.gamedvr", Name = "Disable Game DVR background recording", Category = TweakCategory.Performance,
            Description = "Stops Xbox Game Bar from constantly recording gameplay in the background. Improves FPS in games.",
            Values =
            [
                D(CU, @"System\GameConfigStore", "GameDVR_Enabled", 0, 1),
                D(CU, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, null),
                D(LM, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "perf.bgapps", Name = "Stop Store apps running in background", Category = TweakCategory.Performance,
            Description = "Prevents Microsoft Store apps from running when you are not using them. Some apps may show fewer notifications.",
            Values =
            [
                D(CU, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, 0),
                D(CU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "perf.edgebg", Name = "Stop Edge running in background", Category = TweakCategory.Performance,
            Description = "Disables Edge Startup Boost and background mode, so Edge doesn't use memory when closed.",
            Values =
            [
                D(LM, @"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0, null),
                D(LM, @"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "perf.storagesense", Name = "Turn on Storage Sense (weekly)", Category = TweakCategory.Performance,
            Description = "Lets Windows clean temporary files automatically every week.",
            Values =
            [
                D(CU, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", 1, 0),
                D(CU, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "2048", 7, null),
                D(CU, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "04", 1, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "perf.dopeer", Name = "Stop sharing updates with other PCs", Category = TweakCategory.Performance,
            Description = "Delivery Optimization uploads updates to other PCs on the internet. This keeps downloads but stops uploads.",
            Values = [D(LM, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0, null)],
        };
        yield return new RegistryTweak
        {
            Id = "perf.faststartup", Name = "Disable Fast Startup", Category = TweakCategory.Performance, Effect = ApplyEffect.Restart,
            Description = "Makes Shut down a real fresh boot. Fixes many driver and update glitches. Boot may take a few seconds longer.",
            Values = [D(LM, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, 1)],
        };
        yield return new RegistryTweak
        {
            Id = "perf.hags", Name = "Hardware-accelerated GPU scheduling", Category = TweakCategory.Performance,
            Risk = Risk.Caution, Effect = ApplyEffect.Restart,
            Description = "Lets the graphics card manage its own memory. Can reduce latency on modern GPUs; revert if you see stutter.",
            Values = [D(LM, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, null)],
        };
    }

    // ---------------------------------------------------------------- Privacy
    private static IEnumerable<ITweak> Privacy()
    {
        yield return new RegistryTweak
        {
            Id = "priv.telemetry", Name = "Diagnostic data: required only", Category = TweakCategory.Privacy,
            Description = "Limits what Windows sends to Microsoft to the minimum and stops feedback notifications.",
            Values =
            [
                D(LM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 1, null),
                D(LM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "priv.tailored", Name = "Disable tailored experiences", Category = TweakCategory.Privacy,
            Description = "Stops Microsoft using your diagnostic data for personalised tips and ads.",
            Values = [D(CU, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, 1)],
        };
        yield return new RegistryTweak
        {
            Id = "priv.adid", Name = "Disable advertising ID", Category = TweakCategory.Privacy,
            Description = "Apps can no longer use your advertising ID to show personalised ads.",
            Values = [D(CU, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1)],
        };
        yield return new RegistryTweak
        {
            Id = "priv.activity", Name = "Disable activity history", Category = TweakCategory.Privacy,
            Description = "Stops Windows collecting and uploading the list of apps and files you use.",
            Values =
            [
                D(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0, null),
                D(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0, null),
                D(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "priv.feedback", Name = "Never ask for feedback", Category = TweakCategory.Privacy,
            Description = "Stops Windows pop-ups asking you to rate your experience.",
            Values =
            [
                D(CU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0, null),
                D(CU, @"Software\Microsoft\Siuf\Rules", "PeriodInNanoSeconds", 0, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "priv.suggestions", Name = "Disable suggestions, tips & sponsored apps", Category = TweakCategory.Privacy,
            Description = "Removes ads and suggestions in Start, Settings and the lock screen, and stops Windows silently installing promoted apps.",
            Values =
            [
                D(CU, Cdm, "SubscribedContent-338388Enabled", 0, 1),
                D(CU, Cdm, "SubscribedContent-338389Enabled", 0, 1),
                D(CU, Cdm, "SubscribedContent-338393Enabled", 0, 1),
                D(CU, Cdm, "SubscribedContent-353694Enabled", 0, 1),
                D(CU, Cdm, "SubscribedContent-353696Enabled", 0, 1),
                D(CU, Cdm, "SubscribedContent-338387Enabled", 0, 1),
                D(CU, Cdm, "SystemPaneSuggestionsEnabled", 0, 1),
                D(CU, Cdm, "SoftLandingEnabled", 0, 1),
                D(CU, Cdm, "SilentInstalledAppsEnabled", 0, 1),
                D(CU, Cdm, "RotatingLockScreenOverlayEnabled", 0, 1),
                D(CU, Advanced, "Start_IrisRecommendations", 0, 1),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "priv.bing", Name = "Remove Bing web results from Start search", Category = TweakCategory.Privacy, Effect = ApplyEffect.RestartExplorer,
            Description = "Start search only shows your apps, files and settings. Faster and keeps searches local.",
            Values =
            [
                D(CU, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, null),
                D(CU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "priv.copilot", Name = "Turn off Windows Copilot", Category = TweakCategory.Privacy, Effect = ApplyEffect.RestartExplorer,
            Description = "Disables Windows Copilot by policy and hides its taskbar button. To remove the Copilot app itself, use the Bloatware page.",
            Values =
            [
                D(CU, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, null),
                D(LM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, null),
                D(CU, Advanced, "ShowCopilotButton", 0, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "priv.recall", Name = "Turn off Recall snapshots", Category = TweakCategory.Privacy,
            Description = "Stops Windows Recall from saving snapshots of your screen (Copilot+ PCs).",
            Values =
            [
                D(CU, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, null),
                D(LM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, null),
            ],
        };
        yield return new RegistryTweak
        {
            Id = "priv.widgets", Name = "Turn off Widgets", Category = TweakCategory.Privacy, Effect = ApplyEffect.RestartExplorer,
            Description = "Removes the news and weather Widgets board and its taskbar button.",
            Values = [D(LM, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0, null)],
        };
        yield return new RegistryTweak
        {
            Id = "priv.chat", Name = "Hide Chat button", Category = TweakCategory.Privacy, Effect = ApplyEffect.RestartExplorer,
            Description = "Hides the consumer Teams Chat button on older Windows 11 versions.",
            Values = [D(CU, Advanced, "TaskbarMn", 0, null)],
        };
    }

    // --------------------------------------------------------------- Explorer
    private static IEnumerable<ITweak> ExplorerTweaks()
    {
        yield return new RegistryTweak
        {
            Id = "exp.extensions", Name = "Show file extensions", Category = TweakCategory.Explorer, Effect = ApplyEffect.RestartExplorer,
            Description = "Shows .exe, .pdf, .docx and so on. Helps you spot fake files like invoice.pdf.exe.",
            Values = [D(CU, Advanced, "HideFileExt", 0, 1)],
        };
        yield return new RegistryTweak
        {
            Id = "exp.hidden", Name = "Show hidden files", Category = TweakCategory.Explorer, Effect = ApplyEffect.RestartExplorer,
            Description = "Shows hidden files and folders such as AppData. Protected system files stay hidden.",
            Values = [D(CU, Advanced, "Hidden", 1, 2)],
        };
        const string classicMenu = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
        yield return new RegistryTweak
        {
            Id = "exp.classicmenu", Name = "Classic right-click menu", Category = TweakCategory.Explorer, Effect = ApplyEffect.RestartExplorer,
            Description = "Brings back the full Windows 10 style context menu, without needing 'Show more options'.",
            Values = [S(CU, classicMenu + @"\InprocServer32", "", "", null)],
            OwnedKeys = [(CU, classicMenu)],
        };
        yield return new RegistryTweak
        {
            Id = "exp.endtask", Name = "End Task in taskbar menu", Category = TweakCategory.Explorer,
            Description = "Adds 'End task' when you right-click an app on the taskbar, to close frozen apps quickly.",
            Values = [D(CU, Advanced + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 1, null)],
        };
        yield return new RegistryTweak
        {
            Id = "exp.thispc", Name = "Open File Explorer to This PC", Category = TweakCategory.Explorer,
            Description = "File Explorer opens on your drives instead of Home / recent files.",
            Values = [D(CU, Advanced, "LaunchTo", 1, null)],
        };
        yield return new RegistryTweak
        {
            Id = "exp.taskview", Name = "Hide Task View button", Category = TweakCategory.Explorer, Effect = ApplyEffect.RestartExplorer,
            Description = "Removes the Task View button from the taskbar. Win+Tab still works.",
            Values = [D(CU, Advanced, "ShowTaskViewButton", 0, null)],
        };
        yield return new RegistryTweak
        {
            Id = "exp.alignleft", Name = "Align taskbar to the left", Category = TweakCategory.Explorer,
            Description = "Moves the Start button and taskbar icons to the left, like Windows 10.",
            Values = [D(CU, Advanced, "TaskbarAl", 0, null)],
        };
    }

    // --------------------------------------------------------------- Services
    private static IEnumerable<ITweak> Services()
    {
        yield return ServiceTweak.Create("svc.diagtrack", "Disable telemetry service",
            "Stops 'Connected User Experiences and Telemetry', which uploads usage data.",
            TweakCategory.Services, Risk.Safe, ("DiagTrack", 2), ("dmwappushservice", 3));
        yield return ServiceTweak.Create("svc.xbox", "Disable Xbox services",
            "Turns off Xbox Live sign-in and cloud saves. Leave on if you play Game Pass or Microsoft Store games.",
            TweakCategory.Services, Risk.Caution, ("XblAuthManager", 3), ("XblGameSave", 3), ("XboxNetApiSvc", 3), ("XboxGipSvc", 3));
        yield return ServiceTweak.Create("svc.fax", "Disable Fax service", "Not needed unless you send faxes from this PC.",
            TweakCategory.Services, Risk.Safe, ("Fax", 3));
        yield return ServiceTweak.Create("svc.retaildemo", "Disable Retail Demo service", "Only used on display PCs in shops.",
            TweakCategory.Services, Risk.Safe, ("RetailDemo", 3));
        yield return ServiceTweak.Create("svc.maps", "Disable Downloaded Maps Manager", "Not needed if you don't use the Maps app.",
            TweakCategory.Services, Risk.Safe, ("MapsBroker", 2));
        yield return ServiceTweak.Create("svc.wer", "Disable Windows Error Reporting", "Stops crash reports being sent to Microsoft.",
            TweakCategory.Services, Risk.Safe, ("WerSvc", 3));
        yield return ServiceTweak.Create("svc.spooler", "Disable Print Spooler",
            "Only if you never print. This also disables 'Microsoft Print to PDF'.",
            TweakCategory.Services, Risk.Caution, ("Spooler", 2));
        yield return ServiceTweak.Create("svc.search", "Disable Windows Search indexing",
            "Reduces background disk activity. Searching files in Start and Explorer becomes slower.",
            TweakCategory.Services, Risk.Caution, ("WSearch", 2));
    }
}
