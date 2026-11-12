using Microsoft.Win32;
using NewKlear.Core.Tweaks;

namespace NewKlear.Core.Tests;

/// <summary>Uses a private HKCU test key so the real system is never touched.</summary>
public sealed class TweakTests : IDisposable
{
    private const string TestKey = @"Software\NewKlearTests";
    private readonly TempDir _tmp = new();

    public TweakTests() => Registry.CurrentUser.DeleteSubKeyTree(TestKey, false);

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(TestKey, false);
        _tmp.Dispose();
    }

    private static RegistryTweak Make(string id, params RegValue[] values) => new()
    {
        Id = id, Name = id, Description = "test", Category = TweakCategory.Performance, Values = values,
    };

    [Fact]
    public async Task Apply_then_revert_restores_exact_original_values()
    {
        RegistryHelper.Write(RegistryHive.CurrentUser, TestKey, "Existing", RegistryValueKind.DWord, 5);
        RegistryHelper.Write(RegistryHive.CurrentUser, TestKey, "Text", RegistryValueKind.String, "hello");

        var tweak = Make("t.exact",
            new RegValue(RegistryHive.CurrentUser, TestKey, "Existing", RegistryValueKind.DWord, 0, 1),
            new RegValue(RegistryHive.CurrentUser, TestKey, "Text", RegistryValueKind.String, "changed", "default"),
            new RegValue(RegistryHive.CurrentUser, TestKey, "Missing", RegistryValueKind.DWord, 1, null));

        Assert.False(tweak.IsApplied());
        await tweak.ApplyAsync();
        Assert.True(tweak.IsApplied());

        await tweak.RevertAsync();

        // Original values come back — not the "Windows defaults" (1 / "default").
        Assert.Equal(5, RegistryHelper.Read(RegistryHive.CurrentUser, TestKey, "Existing", out _));
        Assert.Equal("hello", RegistryHelper.Read(RegistryHive.CurrentUser, TestKey, "Text", out _));
        Assert.Null(RegistryHelper.Read(RegistryHive.CurrentUser, TestKey, "Missing", out _));
        Assert.False(tweak.IsApplied());
    }

    [Fact]
    public async Task Applying_twice_keeps_the_first_backup()
    {
        RegistryHelper.Write(RegistryHive.CurrentUser, TestKey, "V", RegistryValueKind.DWord, 7);
        var tweak = Make("t.twice", new RegValue(RegistryHive.CurrentUser, TestKey, "V", RegistryValueKind.DWord, 0, 1));

        await tweak.ApplyAsync();
        await tweak.ApplyAsync();
        await tweak.RevertAsync();

        Assert.Equal(7, RegistryHelper.Read(RegistryHive.CurrentUser, TestKey, "V", out _));
    }

    [Fact]
    public async Task Revert_without_backup_uses_windows_defaults()
    {
        var tweak = Make("t.defaults",
            new RegValue(RegistryHive.CurrentUser, TestKey, "A", RegistryValueKind.DWord, 0, 1),
            new RegValue(RegistryHive.CurrentUser, TestKey, "B", RegistryValueKind.DWord, 0, null));
        RegistryHelper.Write(RegistryHive.CurrentUser, TestKey, "A", RegistryValueKind.DWord, 0);
        RegistryHelper.Write(RegistryHive.CurrentUser, TestKey, "B", RegistryValueKind.DWord, 0);

        await tweak.RevertAsync();

        Assert.Equal(1, RegistryHelper.Read(RegistryHive.CurrentUser, TestKey, "A", out _));
        Assert.Null(RegistryHelper.Read(RegistryHive.CurrentUser, TestKey, "B", out _));
    }

    [Fact]
    public async Task Owned_keys_are_removed_on_revert_when_they_did_not_exist()
    {
        var owned = TestKey + @"\CLSID\{test}";
        var tweak = new RegistryTweak
        {
            Id = "t.owned", Name = "owned", Description = "", Category = TweakCategory.Explorer,
            Values = [new RegValue(RegistryHive.CurrentUser, owned + @"\InprocServer32", "", RegistryValueKind.String, "", null)],
            OwnedKeys = [(RegistryHive.CurrentUser, owned)],
        };

        await tweak.ApplyAsync();
        Assert.True(tweak.IsApplied());
        await tweak.RevertAsync();
        Assert.False(RegistryHelper.KeyExists(RegistryHive.CurrentUser, owned));
    }

    [Fact]
    public async Task Binary_and_large_dword_values_round_trip()
    {
        var original = new byte[] { 0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 };
        RegistryHelper.Write(RegistryHive.CurrentUser, TestKey, "Mask", RegistryValueKind.Binary, original);
        RegistryHelper.Write(RegistryHive.CurrentUser, TestKey, "Big", RegistryValueKind.DWord, 0xFFFFFFFFu);

        var tweak = Make("t.binary",
            new RegValue(RegistryHive.CurrentUser, TestKey, "Mask", RegistryValueKind.Binary, new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0, 0, 0 }, null),
            new RegValue(RegistryHive.CurrentUser, TestKey, "Big", RegistryValueKind.DWord, 10, null));

        await tweak.ApplyAsync();
        Assert.True(tweak.IsApplied());
        await tweak.RevertAsync();

        Assert.Equal(original, (byte[])RegistryHelper.Read(RegistryHive.CurrentUser, TestKey, "Mask", out _)!);
        Assert.True(RegistryHelper.ValuesEqual(RegistryHelper.Read(RegistryHive.CurrentUser, TestKey, "Big", out _), 0xFFFFFFFFu));
    }

    [Fact]
    public void Catalog_ids_are_unique_and_every_tweak_has_values()
    {
        var tweaks = TweakCatalog.Build();
        Assert.Equal(tweaks.Count, tweaks.Select(t => t.Id).Distinct().Count());
        foreach (var t in tweaks.OfType<RegistryTweak>()) Assert.NotEmpty(t.Values);
    }
}
