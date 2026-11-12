using NewKlear.Core.Bloat;
using NewKlear.Core.Cleaning;
using NewKlear.Core.Infrastructure;
using NewKlear.Core.Startup;

namespace NewKlear.Core.Tests;

public class BloatCatalogTests
{
    [Theory]
    [InlineData("Microsoft.BingNews", BloatTag.Recommended)]
    [InlineData("Microsoft.Copilot", BloatTag.Recommended)]
    [InlineData("Microsoft.WindowsStore", BloatTag.Protected)]
    [InlineData("Microsoft.WindowsCalculator", BloatTag.Protected)]
    [InlineData("MicrosoftWindows.Client.WebExperience", BloatTag.Optional)] // exact match beats MicrosoftWindows.Client.*
    [InlineData("MicrosoftWindows.Client.CBS", BloatTag.Protected)]
    [InlineData("king.com.CandyCrushSaga", BloatTag.Recommended)]
    [InlineData("NVIDIACorp.NVIDIAControlPanel", BloatTag.Protected)]
    public void Tags_are_correct(string package, BloatTag expected)
    {
        Assert.Equal(expected, BloatCatalog.Match(package)?.Tag);
    }

    [Fact]
    public void Copilot_packages_are_flagged()
    {
        Assert.True(BloatCatalog.Match("Microsoft.Copilot")!.IsCopilot);
        Assert.True(BloatCatalog.Match("Microsoft.Windows.Ai.Copilot.Provider")!.IsCopilot);
    }

    [Fact]
    public void Unknown_package_is_not_matched_and_prettified()
    {
        Assert.Null(BloatCatalog.Match("Contoso.SomethingNew"));
        Assert.Equal("Something New", BloatCatalog.Prettify("Contoso.SomethingNew"));
    }
}

public class StartupTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --minimized", "C:\\Program Files\\App\\app.exe")]
    [InlineData("C:\\Tools\\tool.exe /background", "C:\\Tools\\tool.exe")]
    [InlineData("C:\\Program Files\\Spaced App\\run.exe -x", "C:\\Program Files\\Spaced App\\run.exe")]
    public void Extracts_executable_from_command(string command, string expected)
    {
        Assert.Equal(expected, StartupManager.ExtractExecutable(command));
    }
}

public class FormatTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(-1, "—")]
    public void Formats_bytes(long bytes, string expected) => Assert.Equal(expected, Format.Bytes(bytes));
}

public class CatalogTests
{
    [Fact]
    public void Developer_caches_follow_the_setting()
    {
        var with = CleanerCatalog.Build(new AppSettings { IncludeDeveloperCaches = true });
        var without = CleanerCatalog.Build(new AppSettings { IncludeDeveloperCaches = false });
        Assert.Contains(with, c => c.Category == CleanerCategories.Developer);
        Assert.DoesNotContain(without, c => c.Category == CleanerCategories.Developer);
    }

    [Fact]
    public void Advanced_items_are_caution_and_off_by_default()
    {
        foreach (var c in CleanerCatalog.Build(new AppSettings()).Where(c => c.Category == CleanerCategories.Advanced))
        {
            Assert.Equal(Risk.Caution, c.Risk);
            Assert.False(c.DefaultSelected);
            Assert.False(string.IsNullOrWhiteSpace(c.Warning));
        }
    }

    [Fact]
    public void Cleaner_ids_are_unique()
    {
        var all = CleanerCatalog.Build(new AppSettings { IncludeDeveloperCaches = true });
        Assert.Equal(all.Count, all.Select(c => c.Id).Distinct().Count());
    }
}

/// <summary>Read-only: runs every scanner on this real PC. Nothing is deleted.</summary>
public class LiveScanTests
{
    [Fact]
    public async Task All_cleaners_scan_without_throwing()
    {
        var admin = Elevation.IsAdministrator;
        foreach (var c in CleanerCatalog.Build(new AppSettings { IncludeDeveloperCaches = true }).Where(c => admin || !c.RequiresAdmin))
        {
            var r = await c.ScanAsync(CancellationToken.None);
            Assert.True(r.Bytes >= -1, c.Id);
        }
    }
}
