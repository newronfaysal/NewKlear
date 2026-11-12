using System.Text.Json;
using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Bloat;

public sealed class InstalledApp
{
    public required string PackageName { get; init; }
    public required string PackageFullName { get; init; }
    public required string PackageFamilyName { get; init; }
    public string Version { get; init; } = "";
    public bool IsProvisioned { get; init; }
    public required string FriendlyName { get; init; }
    public BloatTag Tag { get; init; }
    public string Description { get; init; } = "";
    public bool IsCopilot { get; init; }
}

public sealed record RemovalResult(bool Success, string? Error);

/// <summary>Lists and removes Store (Appx) packages through Windows PowerShell's Appx module.</summary>
public static class AppxService
{
    private sealed class PsPackage
    {
        public string? Name { get; set; }
        public string? PackageFullName { get; set; }
        public string? PackageFamilyName { get; set; }
        public string? Version { get; set; }
        public string? Sig { get; set; }
    }

    public static async Task<IReadOnlyList<InstalledApp>> ListAsync(CancellationToken ct = default)
    {
        const string script = """
            $pk = @(Get-AppxPackage | Where-Object { -not $_.IsFramework -and -not $_.NonRemovable -and -not $_.IsResourcePackage } |
                Select-Object Name, PackageFullName, PackageFamilyName, @{n='Version';e={"$($_.Version)"}}, @{n='Sig';e={"$($_.SignatureKind)"}})
            $pv = @()
            try { $pv = @(Get-AppxProvisionedPackage -Online -ErrorAction Stop | ForEach-Object { $_.DisplayName }) } catch {}
            [pscustomobject]@{ Packages = $pk; Provisioned = $pv } | ConvertTo-Json -Depth 4 -Compress
            """;
        var result = await ProcessRunner.PowerShellAsync(script, ct, TimeSpan.FromMinutes(3));
        if (string.IsNullOrWhiteSpace(result.StdOut))
        {
            Log.Error($"Get-AppxPackage failed: {result.StdErr}");
            throw new InvalidOperationException("Could not list installed apps. " + result.StdErr.Trim());
        }

        using var doc = JsonDocument.Parse(result.StdOut);
        var provisioned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.TryGetProperty("Provisioned", out var pv) && pv.ValueKind == JsonValueKind.Array)
            foreach (var p in pv.EnumerateArray())
                if (p.GetString() is { } s) provisioned.Add(s);

        var packages = new List<PsPackage>();
        if (doc.RootElement.TryGetProperty("Packages", out var pk))
        {
            if (pk.ValueKind == JsonValueKind.Array)
                packages.AddRange(pk.EnumerateArray().Select(e => e.Deserialize<PsPackage>()!));
            else if (pk.ValueKind == JsonValueKind.Object)
                packages.Add(pk.Deserialize<PsPackage>()!);
        }

        var apps = new List<InstalledApp>();
        foreach (var p in packages)
        {
            if (string.IsNullOrEmpty(p.Name) || string.IsNullOrEmpty(p.PackageFullName)) continue;
            var entry = BloatCatalog.Match(p.Name);
            // System-signed packages we don't know about are Windows internals — never offer them.
            if (entry is null && string.Equals(p.Sig, "System", StringComparison.OrdinalIgnoreCase)) continue;

            apps.Add(new InstalledApp
            {
                PackageName = p.Name,
                PackageFullName = p.PackageFullName,
                PackageFamilyName = p.PackageFamilyName ?? p.Name,
                Version = p.Version ?? "",
                IsProvisioned = provisioned.Contains(p.Name),
                FriendlyName = entry?.FriendlyName ?? BloatCatalog.Prettify(p.Name),
                Tag = entry?.Tag ?? BloatTag.Unlisted,
                Description = entry?.Description ?? "Not in NewKlear's catalog. Remove only if you know what it is.",
                IsCopilot = entry?.IsCopilot ?? false,
            });
        }

        // Same package may be installed in several versions/architectures; show one row per name.
        return apps.GroupBy(a => a.PackageName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(a => a.Tag).ThenBy(a => a.FriendlyName)
            .ToList();
    }

    public static async Task<RemovalResult> RemoveAsync(InstalledApp app, bool deprovision, CancellationToken ct = default)
    {
        var name = ProcessRunner.PsQuote(app.PackageName);
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            try {
                Get-AppxPackage -Name {{name}} | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName }
                if (${{(deprovision ? "true" : "false")}}) {
                    Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq {{name}} } |
                        ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName | Out-Null }
                }
                'OK'
            } catch { 'ERR:' + $_.Exception.Message }
            """;
        var r = await ProcessRunner.PowerShellAsync(script, ct, TimeSpan.FromMinutes(5));
        var output = r.StdOut.Trim();
        if (output.EndsWith("OK", StringComparison.Ordinal))
        {
            Log.Info($"Removed app {app.PackageName}{(deprovision ? " (and deprovisioned)" : "")}");
            return new RemovalResult(true, null);
        }
        var err = output.Contains("ERR:") ? output[(output.IndexOf("ERR:", StringComparison.Ordinal) + 4)..] : (r.StdErr.Trim() is { Length: > 0 } e ? e : "Unknown error");
        Log.Warn($"Failed to remove {app.PackageName}: {err}");
        return new RemovalResult(false, err);
    }

    /// <summary>
    /// Tries to re-register a removed app from the copy Windows still keeps. Returns false if the
    /// package is gone and must be reinstalled from the Microsoft Store.
    /// </summary>
    public static async Task<bool> TryReinstallAsync(string packageName, CancellationToken ct = default)
    {
        var name = ProcessRunner.PsQuote(packageName);
        var script = $$"""
            $p = Get-AppxPackage -AllUsers -Name {{name}} -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $p) { 'MISSING'; exit }
            try { Add-AppxPackage -DisableDevelopmentMode -Register (Join-Path $p.InstallLocation 'AppxManifest.xml') -ErrorAction Stop; 'OK' }
            catch { 'ERR:' + $_.Exception.Message }
            """;
        var r = await ProcessRunner.PowerShellAsync(script, ct, TimeSpan.FromMinutes(5));
        var ok = r.StdOut.Trim().EndsWith("OK", StringComparison.Ordinal);
        Log.Info($"Reinstall {packageName}: {(ok ? "OK" : r.StdOut.Trim())}");
        return ok;
    }

    public static string StoreSearchUri(string friendlyName) =>
        $"ms-windows-store://search/?query={Uri.EscapeDataString(friendlyName)}";
}
