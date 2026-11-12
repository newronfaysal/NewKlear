using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Tweaks;

public enum RestorePointStatus
{
    Created,
    ProtectionDisabled,
    Failed,
}

public sealed record RestorePointResult(RestorePointStatus Status, string Message);

public static class RestorePointService
{
    /// <summary>
    /// Creates a System Restore point. Windows normally allows only one per 24 hours; the frequency
    /// limit is lifted for this call and then restored to its previous value.
    /// </summary>
    public static async Task<RestorePointResult> CreateAsync(string description)
    {
        var desc = ProcessRunner.PsQuote(description);
        var script = $$"""
            $k = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore'
            $old = (Get-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue).SystemRestorePointCreationFrequency
            try {
                Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord -ErrorAction SilentlyContinue
                Checkpoint-Computer -Description {{desc}} -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
                'OK'
            } catch {
                'ERR:' + $_.Exception.Message
            } finally {
                if ($null -eq $old) { Remove-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue }
                else { Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value $old -ErrorAction SilentlyContinue }
            }
            """;
        var r = await ProcessRunner.PowerShellAsync(script, timeout: TimeSpan.FromMinutes(10));
        var output = (r.StdOut + r.StdErr).Trim();
        if (output.Contains("OK") && !output.Contains("ERR:"))
        {
            Log.Info($"Restore point created: {description}");
            return new RestorePointResult(RestorePointStatus.Created, "Restore point created.");
        }

        Log.Warn($"Restore point failed: {output}");
        var disabled = output.Contains("disabled", StringComparison.OrdinalIgnoreCase)
                       || output.Contains("turned off", StringComparison.OrdinalIgnoreCase)
                       || output.Contains("0x80042306") || output.Contains("not enabled", StringComparison.OrdinalIgnoreCase);
        return disabled
            ? new RestorePointResult(RestorePointStatus.ProtectionDisabled, "System Protection is turned off for the system drive.")
            : new RestorePointResult(RestorePointStatus.Failed, output.Replace("ERR:", "").Trim());
    }

    /// <summary>Turns on System Protection for the system drive.</summary>
    public static async Task<bool> EnableProtectionAsync()
    {
        var r = await ProcessRunner.PowerShellAsync(
            "try { Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" -ErrorAction Stop; 'OK' } catch { 'ERR:' + $_.Exception.Message }");
        var ok = r.StdOut.Trim().EndsWith("OK");
        Log.Info($"Enable System Protection: {(ok ? "OK" : r.StdOut.Trim())}");
        return ok;
    }
}
