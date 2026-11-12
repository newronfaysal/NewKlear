using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace NewKlear.App.Services;

public sealed record ThemeDefinition(
    string Id, string Name, string Tagline, bool IsDark,
    string Background, string Sidebar, string Surface, string Surface2, string Border,
    string Text, string TextMuted, string Accent, string Accent2, string OnAccent)
{
    public Brush PreviewBackground => ThemeService.Brush(Background);
    public Brush PreviewSurface => ThemeService.Brush(Surface);
    public Brush PreviewAccent => new LinearGradientBrush(ThemeService.Color(Accent), ThemeService.Color(Accent2), 0);
    public Brush PreviewText => ThemeService.Brush(Text);
}

/// <summary>Brand themes. "Ember" follows the NewKlear logo: deep black, silver-white and glowing orange.</summary>
public static class ThemeService
{
    public static readonly IReadOnlyList<ThemeDefinition> Themes =
    [
        new("ember", "Ember", "Black & logo orange (default)", true,
            "#0A0A0C", "#0E0E11", "#16161B", "#1F1F26", "#2A2A33", "#F4F4F6", "#9D9DA8", "#FF6A00", "#FFA040", "#FFFFFF"),
        new("ember-light", "Ember Light", "Warm light with orange", false,
            "#F6F4F1", "#FFFFFF", "#FFFFFF", "#F2EEEA", "#E4DED8", "#141416", "#5F5F69", "#E85D00", "#FF8A2B", "#FFFFFF"),
        new("reactor", "Reactor", "Radioactive green — a nod to the name", true,
            "#070D0A", "#0A120E", "#101B15", "#16251C", "#1F3328", "#ECF7F0", "#8FA89A", "#2EE07A", "#B4FF4D", "#04140A"),
        new("cyan", "Carbon Cyan", "Cool carbon with electric cyan", true,
            "#080B0F", "#0B0F14", "#121821", "#18202B", "#223040", "#EEF4FA", "#8D9AAA", "#00AEEF", "#5CE1FF", "#03131C"),
        new("violet", "Midnight Violet", "Deep night with neon violet", true,
            "#0B0911", "#0F0C17", "#16121F", "#1E1929", "#2B2440", "#F3F0FA", "#A39BB5", "#8B5CF6", "#D08CFF", "#FFFFFF"),
    ];

    public static ThemeDefinition Current { get; private set; } = Themes[0];

    public static event Action? ThemeChanged;

    public static Color Color(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static SolidColorBrush Brush(string hex, byte? alpha = null)
    {
        var c = Color(hex);
        if (alpha.HasValue) c.A = alpha.Value;
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static void Apply(string id)
    {
        var theme = Themes.FirstOrDefault(t => t.Id == id) ?? Themes[0];
        Current = theme;
        var res = Application.Current.Resources;

        var appTheme = theme.IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(appTheme, WindowBackdropType.None, false);
        ApplicationAccentColorManager.Apply(Color(theme.Accent), appTheme, false, false);

        res["Nk.Bg"] = Brush(theme.Background);
        res["Nk.Sidebar"] = Brush(theme.Sidebar);
        res["Nk.Surface"] = Brush(theme.Surface);
        res["Nk.Surface2"] = Brush(theme.Surface2);
        res["Nk.Border"] = Brush(theme.Border);
        res["Nk.Text"] = Brush(theme.Text);
        res["Nk.TextMuted"] = Brush(theme.TextMuted);
        res["Nk.Accent"] = Brush(theme.Accent);
        res["Nk.Accent2"] = Brush(theme.Accent2);
        res["Nk.OnAccent"] = Brush(theme.OnAccent);
        res["Nk.AccentSoft"] = Brush(theme.Accent, 0x22);
        res["Nk.AccentSofter"] = Brush(theme.Accent, 0x12);
        res["Nk.AccentColor"] = Color(theme.Accent);
        res["Nk.Hover"] = Brush(theme.IsDark ? "#FFFFFF" : "#000000", 0x0C);
        res["Nk.Danger"] = Brush(theme.IsDark ? "#FF5A5F" : "#D92D20");
        res["Nk.DangerSoft"] = Brush("#FF5A5F", 0x22);
        res["Nk.Success"] = Brush(theme.IsDark ? "#3DDC84" : "#16A34A");
        res["Nk.SuccessSoft"] = Brush("#3DDC84", 0x22);
        res["Nk.Warning"] = Brush(theme.IsDark ? "#FFB020" : "#B45309");
        res["Nk.WarningSoft"] = Brush("#FFB020", 0x26);

        var grad = new LinearGradientBrush(Color(theme.Accent), Color(theme.Accent2), new Point(0, 0), new Point(1, 1));
        grad.Freeze();
        res["Nk.AccentGradient"] = grad;

        // Subtle glow behind the hero card, like the logo's light trail.
        var glow = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.85, 0.2), Center = new Point(0.85, 0.2), RadiusX = 0.7, RadiusY = 1.2,
            GradientStops =
            {
                new GradientStop(WithAlpha(Color(theme.Accent), (byte)(theme.IsDark ? 0x40 : 0x26)), 0),
                new GradientStop(WithAlpha(Color(theme.Accent), 0x00), 1),
            },
        };
        glow.Freeze();
        res["Nk.HeroGlow"] = glow;

        ThemeChanged?.Invoke();
    }

    private static Color WithAlpha(Color c, byte a) => System.Windows.Media.Color.FromArgb(a, c.R, c.G, c.B);
}
