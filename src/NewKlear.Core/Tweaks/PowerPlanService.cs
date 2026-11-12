using System.Text.RegularExpressions;
using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Tweaks;

public sealed record PowerPlan(Guid Id, string Name, bool IsActive);

public static partial class PowerPlanService
{
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public static readonly Guid UltimatePerformance = new("e9a42b02-d5df-448d-aa00-03f14749eb61");
    public static readonly Guid PowerSaver = new("a1841308-3541-4fab-bc81-f71556f20b4a");

    private static string Powercfg => ProcessRunner.System32("powercfg.exe");

    [GeneratedRegex(@"([0-9a-fA-F\-]{36})\s+\((.+?)\)\s*(\*)?")]
    private static partial Regex PlanLine();

    public static async Task<IReadOnlyList<PowerPlan>> ListAsync()
    {
        var r = await ProcessRunner.RunAsync(Powercfg, "/list");
        var plans = new List<PowerPlan>();
        foreach (Match m in PlanLine().Matches(r.StdOut))
            plans.Add(new PowerPlan(Guid.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim(), m.Groups[3].Success));
        return plans;
    }

    public static async Task<PowerPlan?> GetActiveAsync() => (await ListAsync()).FirstOrDefault(p => p.IsActive);

    /// <summary>
    /// Activates a built-in scheme. High/Ultimate performance are hidden on many laptops, so they are
    /// created from the template when missing.
    /// </summary>
    public static async Task<bool> ActivateBuiltInAsync(Guid template)
    {
        var plans = await ListAsync();
        var target = plans.FirstOrDefault(p => p.Id == template);

        if (target is null && template == UltimatePerformance)
            target = plans.FirstOrDefault(p => p.Name.Contains("Ultimate", StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            var dup = await ProcessRunner.RunAsync(Powercfg, $"-duplicatescheme {template}");
            var m = PlanLine().Match(dup.StdOut);
            if (!dup.Success || !m.Success)
            {
                Log.Warn($"Could not create power plan {template}: {dup.StdOut}{dup.StdErr}");
                return false;
            }
            target = new PowerPlan(Guid.Parse(m.Groups[1].Value), m.Groups[2].Value, false);
        }
        return await ActivateAsync(target.Id);
    }

    public static async Task<bool> ActivateAsync(Guid id)
    {
        var r = await ProcessRunner.RunAsync(Powercfg, $"/setactive {id}");
        Log.Info($"Power plan set to {id}: {(r.Success ? "OK" : r.StdErr.Trim())}");
        return r.Success;
    }
}
