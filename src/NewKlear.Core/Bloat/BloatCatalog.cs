namespace NewKlear.Core.Bloat;

public enum BloatTag
{
    Recommended,
    Optional,
    Protected,
    Unlisted,
}

public sealed record BloatEntry(string Pattern, string FriendlyName, BloatTag Tag, string Description, bool IsCopilot = false);

/// <summary>Curated knowledge about preinstalled Windows 11 apps. Patterns support a trailing or leading '*'.</summary>
public static class BloatCatalog
{
    private static BloatEntry R(string p, string n, string d) => new(p, n, BloatTag.Recommended, d);
    private static BloatEntry O(string p, string n, string d) => new(p, n, BloatTag.Optional, d);
    private static BloatEntry P(string p, string n, string d) => new(p, n, BloatTag.Protected, d);

    public static readonly IReadOnlyList<BloatEntry> Entries =
    [
        // ---- Copilot / AI
        new("Microsoft.Copilot", "Microsoft Copilot", BloatTag.Recommended, "The Copilot app. Removing it also removes the Copilot key action until reinstalled from the Store.", IsCopilot: true),
        new("Microsoft.Windows.Ai.Copilot.Provider", "Copilot provider", BloatTag.Optional, "Background component that connects Windows features to Copilot.", IsCopilot: true),
        new("Microsoft.MicrosoftOfficeHub", "Microsoft 365 Copilot (Office hub)", BloatTag.Recommended, "Launcher for Office web apps and Microsoft 365 Copilot. Installed Office apps are not affected.", IsCopilot: true),

        // ---- Microsoft promos & legacy
        R("Microsoft.BingNews", "News", "Microsoft Start news feed."),
        R("Microsoft.BingWeather", "Weather", "MSN Weather app."),
        R("Microsoft.BingSearch", "Bing Search", "Bing search integration app."),
        R("Microsoft.BingFinance", "Money", "Legacy MSN Money app."),
        R("Microsoft.BingSports", "Sports", "Legacy MSN Sports app."),
        R("Microsoft.BingTravel", "Travel", "Legacy MSN Travel app."),
        R("Microsoft.BingFoodAndDrink", "Food & Drink", "Legacy MSN app."),
        R("Microsoft.BingHealthAndFitness", "Health & Fitness", "Legacy MSN app."),
        R("Clipchamp.Clipchamp", "Clipchamp", "Video editor promoted by Microsoft."),
        R("Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection", "Card games with ads."),
        R("Microsoft.549981C3F5F10", "Cortana", "Discontinued voice assistant."),
        R("Microsoft.WindowsFeedbackHub", "Feedback Hub", "Sends feedback to Microsoft."),
        R("Microsoft.GetHelp", "Get Help", "Microsoft support chat app."),
        R("Microsoft.Getstarted", "Tips", "Windows tips and tricks."),
        R("Microsoft.Microsoft3DViewer", "3D Viewer", "Viewer for 3D models."),
        R("Microsoft.MixedReality.Portal", "Mixed Reality Portal", "Only needed for Windows Mixed Reality headsets."),
        R("Microsoft.PowerAutomateDesktop", "Power Automate", "Desktop automation tool."),
        R("Microsoft.SkypeApp", "Skype", "Skype has been retired by Microsoft."),
        R("MicrosoftTeams", "Teams (personal)", "Consumer Teams chat app."),
        R("Microsoft.People", "People", "Legacy contacts app."),
        R("Microsoft.WindowsMaps", "Maps", "Offline maps app."),
        R("microsoft.windowscommunicationsapps", "Mail and Calendar", "Retired by Microsoft in favour of the new Outlook."),
        R("Microsoft.Messaging", "Messaging", "Legacy messaging app."),
        R("Microsoft.OneConnect", "Mobile Plans", "Mobile data plan purchasing."),
        R("Microsoft.Print3D", "Print 3D", "Legacy 3D printing app."),
        R("Microsoft.Wallet", "Wallet", "Legacy payments app."),
        R("Microsoft.MicrosoftFamily", "Family Safety", "Parental controls app."),
        R("MicrosoftCorporationII.MicrosoftFamily", "Family Safety", "Parental controls app."),
        R("Microsoft.DevHome", "Dev Home", "Developer dashboard, retired by Microsoft."),
        R("Microsoft.Windows.DevHome", "Dev Home", "Developer dashboard, retired by Microsoft."),
        R("Microsoft.Todos", "Microsoft To Do", "Task list app."),
        R("Microsoft.MicrosoftJournal", "Journal", "Ink note-taking app."),
        R("Microsoft.NetworkSpeedTest", "Network Speed Test", "Bing speed test app."),
        R("Microsoft.News", "News", "Microsoft news app."),

        // ---- Xbox / gaming
        O("Microsoft.GamingApp", "Xbox", "Keep if you use PC Game Pass or the Xbox app."),
        O("Microsoft.XboxApp", "Xbox Console Companion", "Legacy Xbox companion app."),
        O("Microsoft.XboxGamingOverlay", "Xbox Game Bar", "Win+G overlay for recording and widgets. Some games use it for achievements."),
        O("Microsoft.XboxGameOverlay", "Xbox Game Overlay", "Overlay used by some Xbox-enabled games."),
        O("Microsoft.XboxSpeechToTextOverlay", "Xbox speech-to-text", "Party chat transcription overlay."),
        O("Microsoft.XboxIdentityProvider", "Xbox Identity Provider", "Needed to sign in to Xbox Live in PC games."),
        O("Microsoft.Xbox.TCUI", "Xbox TCUI", "Xbox profile UI used by games."),

        // ---- Optional Microsoft apps
        O("MSTeams", "Microsoft Teams", "Remove only if you don't use Teams for work or school."),
        O("Microsoft.YourPhone", "Phone Link", "Connects your Android phone or iPhone."),
        O("MicrosoftWindows.CrossDevice", "Cross Device Experience Host", "Phone and cross-device features."),
        O("MicrosoftWindows.Client.WebExperience", "Widgets", "Taskbar widgets board (news, weather)."),
        O("Microsoft.OutlookForWindows", "Outlook (new)", "The new Outlook for Windows."),
        O("Microsoft.ZuneVideo", "Movies & TV", "Legacy video player."),
        O("Microsoft.ZuneMusic", "Media Player", "Default music and video player."),
        O("Microsoft.MicrosoftStickyNotes", "Sticky Notes", "Desktop sticky notes."),
        O("Microsoft.WindowsAlarms", "Clock", "Alarms, timers and Focus sessions."),
        O("Microsoft.WindowsSoundRecorder", "Sound Recorder", "Simple audio recorder."),
        O("Microsoft.Whiteboard", "Whiteboard", "Collaborative whiteboard."),
        O("Microsoft.Office.OneNote", "OneNote for Windows 10", "Legacy OneNote app."),
        O("MicrosoftCorporationII.QuickAssist", "Quick Assist", "Remote help tool. Keep if someone helps you remotely."),
        O("Microsoft.BingTranslator", "Translator", "Translation app."),
        P("Microsoft.ScreenSketch", "Snipping Tool", "Screenshot tool (Win+Shift+S)."),
        P("Microsoft.Paint", "Paint", "Classic image editor."),
        P("Microsoft.WindowsCamera", "Camera", "Webcam app."),

        // ---- Third-party promos
        R("*CandyCrush*", "Candy Crush", "Preinstalled game."),
        R("king.com.*", "King game", "Preinstalled game."),
        R("*Disney*", "Disney+", "Preinstalled streaming app."),
        R("*TikTok*", "TikTok", "Preinstalled social app."),
        R("Facebook.*", "Facebook / Instagram", "Preinstalled social app."),
        R("*Instagram*", "Instagram", "Preinstalled social app."),
        R("7EE7776C.LinkedInforWindows", "LinkedIn", "Preinstalled social app."),
        R("*Netflix*", "Netflix", "Preinstalled streaming app."),
        R("AmazonVideo.PrimeVideo", "Prime Video", "Preinstalled streaming app."),
        R("*Hulu*", "Hulu", "Preinstalled streaming app."),
        R("*McAfee*", "McAfee", "Trial antivirus. Windows Security already protects you."),
        R("*Norton*", "Norton", "Trial antivirus. Windows Security already protects you."),
        R("*ExpressVPN*", "ExpressVPN", "Preinstalled VPN trial."),
        R("*Booking.com*", "Booking.com", "Preinstalled promo app."),
        O("SpotifyAB.SpotifyMusic", "Spotify", "Music streaming app."),
        O("5319275A.WhatsAppDesktop", "WhatsApp", "Messaging app."),
        O("AD2F1837.*", "HP companion app", "HP utility. Keep if you use HP support tools."),
        O("DellInc.*", "Dell companion app", "Dell utility. Keep if you use Dell support tools."),
        O("E046963F.*", "Lenovo companion app", "Lenovo utility. Keep if you use Lenovo Vantage."),
        O("B9ECED6F.*", "ASUS companion app", "ASUS utility. Keep if you use MyASUS."),
        O("AcerIncorporated.*", "Acer companion app", "Acer utility."),

        // ---- Protected (hidden by default)
        P("Microsoft.WindowsStore", "Microsoft Store", "Needed to install and update apps."),
        P("Microsoft.StorePurchaseApp", "Store Purchase", "Needed for Store purchases."),
        P("Microsoft.DesktopAppInstaller", "App Installer (winget)", "Needed to install apps and for winget."),
        P("Microsoft.WindowsCalculator", "Calculator", "Core Windows app."),
        P("Microsoft.Windows.Photos", "Photos", "Default image viewer."),
        P("Microsoft.WindowsTerminal", "Terminal", "Default command line host."),
        P("Microsoft.WindowsNotepad", "Notepad", "Core Windows app."),
        P("Microsoft.SecHealthUI", "Windows Security", "Antivirus and firewall UI."),
        P("Microsoft.MicrosoftEdge*", "Microsoft Edge", "Many Windows features depend on Edge WebView2."),
        P("Microsoft.Windows.ShellExperienceHost", "Shell Experience Host", "Part of the Windows shell."),
        P("Microsoft.Windows.StartMenuExperienceHost", "Start menu", "Part of the Windows shell."),
        P("MicrosoftWindows.Client.*", "Windows client component", "Part of the Windows shell."),
        P("Microsoft.AAD.BrokerPlugin", "Work or school account", "Needed for Microsoft account sign-in."),
        P("Microsoft.AccountsControl", "Accounts control", "Needed for account sign-in."),
        P("Microsoft.HEIFImageExtension", "HEIF Image Extensions", "Codec for iPhone photos."),
        P("Microsoft.HEVCVideoExtension", "HEVC Video Extensions", "Video codec."),
        P("Microsoft.WebpImageExtension", "WebP Image Extensions", "Image codec."),
        P("Microsoft.WebMediaExtensions", "Web Media Extensions", "Media codecs."),
        P("Microsoft.VP9VideoExtensions", "VP9 Video Extensions", "Video codec."),
        P("Microsoft.AV1VideoExtension", "AV1 Video Extension", "Video codec."),
        P("Microsoft.RawImageExtension", "Raw Image Extension", "Camera RAW codec."),
        P("Microsoft.MPEG2VideoExtension", "MPEG-2 Video Extension", "Video codec."),
        P("*NVIDIACorp*", "NVIDIA Control Panel", "Graphics driver companion."),
        P("*IntelGraphics*", "Intel Graphics Command Center", "Graphics driver companion."),
        P("*AdvancedMicroDevices*", "AMD Radeon Software", "Graphics driver companion."),
        P("*RealtekSemiconductor*", "Realtek Audio Console", "Audio driver companion."),
        P("*DolbyLaboratories*", "Dolby Audio", "Audio driver companion."),
        P("*WavesAudio*", "Waves MaxxAudio", "Audio driver companion."),
        P("Microsoft.Windows.CloudExperienceHost", "Cloud Experience Host", "Windows setup and sign-in."),
        P("Microsoft.WindowsAppRuntime*", "Windows App Runtime", "Needed by many apps."),
        P("Microsoft.LanguageExperiencePack*", "Language pack", "Display language."),
        P("Microsoft.OneDriveSync", "OneDrive", "Remove OneDrive from Settings > Apps instead."),
    ];

    public static BloatEntry? Match(string packageName)
    {
        // Exact matches win over wildcard matches.
        var exact = Entries.FirstOrDefault(e => !e.Pattern.Contains('*') && e.Pattern.Equals(packageName, StringComparison.OrdinalIgnoreCase));
        return exact ?? Entries.FirstOrDefault(e => e.Pattern.Contains('*') && Wildcard(e.Pattern, packageName));
    }

    private static bool Wildcard(string pattern, string value)
    {
        var p = pattern.Trim('*');
        var starts = pattern.EndsWith('*');
        var ends = pattern.StartsWith('*');
        return (starts, ends) switch
        {
            (true, true) => value.Contains(p, StringComparison.OrdinalIgnoreCase),
            (true, false) => value.StartsWith(p, StringComparison.OrdinalIgnoreCase),
            (false, true) => value.EndsWith(p, StringComparison.OrdinalIgnoreCase),
            _ => value.Equals(p, StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>Turns "Microsoft.WindowsSoundRecorder" into "Windows Sound Recorder" for unlisted apps.</summary>
    public static string Prettify(string packageName)
    {
        var name = packageName;
        var dot = name.LastIndexOf('.');
        if (dot >= 0 && dot < name.Length - 1) name = name[(dot + 1)..];
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1])) sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }
}
