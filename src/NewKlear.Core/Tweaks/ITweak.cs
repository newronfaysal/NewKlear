using NewKlear.Core.Cleaning;

namespace NewKlear.Core.Tweaks;

public enum TweakCategory
{
    Performance,
    Privacy,
    Explorer,
    Services,
}

public enum ApplyEffect
{
    Immediate,
    RestartExplorer,
    SignOut,
    Restart,
}

public interface ITweak
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    TweakCategory Category { get; }
    Risk Risk { get; }
    ApplyEffect Effect { get; }
    bool RequiresAdmin { get; }
    /// <summary>False when the tweak doesn't apply to this PC (e.g. service not installed).</summary>
    bool IsAvailable { get; }

    bool IsApplied();
    Task ApplyAsync();
    Task RevertAsync();
}
