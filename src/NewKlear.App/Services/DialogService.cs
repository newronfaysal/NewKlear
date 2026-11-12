using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace NewKlear.App.Services;

public enum DialogChoice
{
    Primary,
    Secondary,
    Cancel,
}

/// <summary>Fluent in-window dialogs.</summary>
public static class DialogService
{
    public static ContentDialogHost? Host { get; set; }

    public static async Task<DialogChoice> ShowAsync(string title, object content, string primary, string? secondary = null,
        string close = "Cancel", bool danger = false)
    {
        if (Host is null) return DialogChoice.Cancel;

        var body = content is string text
            ? new TextBlock
            {
                Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 21,
                Foreground = (Brush)Application.Current.Resources["Nk.Text"],
            }
            : content;

        var dialog = new ContentDialog(Host)
        {
            Title = title,
            Content = new ScrollViewer { Content = body, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = primary,
            SecondaryButtonText = secondary ?? "",
            IsSecondaryButtonEnabled = secondary != null,
            CloseButtonText = close,
            PrimaryButtonAppearance = danger ? ControlAppearance.Danger : ControlAppearance.Primary,
            DialogMaxWidth = 560,
            Background = (Brush)Application.Current.Resources["Nk.Surface"],
            BorderBrush = (Brush)Application.Current.Resources["Nk.Border"],
            Foreground = (Brush)Application.Current.Resources["Nk.Text"],
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => DialogChoice.Primary,
            ContentDialogResult.Secondary => DialogChoice.Secondary,
            _ => DialogChoice.Cancel,
        };
    }

    public static async Task<bool> ConfirmAsync(string title, object content, string primary, bool danger = false) =>
        await ShowAsync(title, content, primary, danger: danger) == DialogChoice.Primary;

    public static Task AlertAsync(string title, string message) => ShowAsync(title, message, "OK", close: "Close");

    /// <summary>Builds a dialog body: intro text followed by a bulleted list.</summary>
    public static FrameworkElement BulletList(string intro, IEnumerable<string> items, string? footer = null)
    {
        var res = Application.Current.Resources;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = intro, TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 10), Foreground = (Brush)res["Nk.Text"] });
        foreach (var item in items)
            panel.Children.Add(new TextBlock { Text = "•  " + item, TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(4, 2, 0, 2), Foreground = (Brush)res["Nk.TextMuted"] });
        if (footer != null)
            panel.Children.Add(new TextBlock { Text = footer, TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0, 12, 0, 0), Foreground = (Brush)res["Nk.Warning"] });
        return panel;
    }
}
