using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using CleanWipe.Core.Models;

namespace CleanWipe.Core.Services;

/// <summary>
/// Detecta los programas instalados. Fuente principal: las tres ramas "Uninstall" del registro.
/// Fuentes opcionales (más lentas): WMI Win32_Product y paquetes Appx vía PowerShell.
/// </summary>
public class ProgramDetector
{
    private static readonly (RegistryKey Hive, string Path, string Source)[] UninstallRoots =
    {
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM-WOW6432"),
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKCU"),
    };

    /// <summary>Enumera los programas instalados desde el registro (rápido).</summary>
    public Task<List<InstalledProgram>> DetectAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var byKey = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);

            foreach (var (hive, path, source) in UninstallRoots)
            {
                ct.ThrowIfCancellationRequested();
                using var root = hive.OpenSubKey(path);
                if (root == null) continue;

                foreach (var subName in root.GetSubKeyNames())
                {
                    ct.ThrowIfCancellationRequested();
                    using var sub = root.OpenSubKey(subName);
                    if (sub == null) continue;

                    var prog = ReadProgram(sub, subName, source, hive == Registry.LocalMachine);
                    if (prog == null) continue;

                    // Deduplica por nombre+versión preferenciando la entrada con UninstallString.
                    string dedup = $"{prog.Name}|{prog.Version}";
                    if (byKey.TryGetValue(dedup, out var existing))
                    {
                        if (string.IsNullOrEmpty(existing.UninstallString) && !string.IsNullOrEmpty(prog.UninstallString))
                            byKey[dedup] = prog;
                    }
                    else
                    {
                        byKey[dedup] = prog;
                    }
                }
            }

            return byKey.Values
                .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, ct);
    }

    private static InstalledProgram? ReadProgram(RegistryKey key, string keyName, string source, bool isMachine)
    {
        string? name = key.GetValue("DisplayName") as string;
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Filtra componentes del sistema, parches y actualizaciones.
        if (ToInt(key.GetValue("SystemComponent")) == 1) return null;
        if (key.GetValue("ParentKeyName") is string parent && !string.IsNullOrEmpty(parent)) return null;
        if (key.GetValue("ReleaseType") is string rt &&
            (rt.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
             rt.Contains("Hotfix", StringComparison.OrdinalIgnoreCase) ||
             rt.Contains("Security", StringComparison.OrdinalIgnoreCase)))
            return null;

        string uninstall = (key.GetValue("UninstallString") as string) ?? string.Empty;
        string quiet = (key.GetValue("QuietUninstallString") as string) ?? string.Empty;

        var prog = new InstalledProgram
        {
            Name = name.Trim(),
            Publisher = (key.GetValue("Publisher") as string)?.Trim() ?? string.Empty,
            Version = (key.GetValue("DisplayVersion") as string)?.Trim() ?? string.Empty,
            InstallLocation = (key.GetValue("InstallLocation") as string)?.Trim() ?? string.Empty,
            UninstallString = uninstall,
            QuietUninstallString = quiet,
            IconPath = (key.GetValue("DisplayIcon") as string)?.Trim() ?? string.Empty,
            EstimatedSizeMB = ToLong(key.GetValue("EstimatedSize")) / 1024, // valor en KB → MB
            InstallDate = ParseInstallDate(key.GetValue("InstallDate") as string),
            Source = source,
            Id = $"{source}:{keyName}",
        };

        return prog;
    }

    private static DateTime? ParseInstallDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        if (DateTime.TryParse(raw, out var d2)) return d2;
        return null;
    }

    private static int ToInt(object? v) => v is int i ? i : 0;

    private static long ToLong(object? v) => v switch
    {
        int i => i,
        long l => l,
        _ => 0
    };

    /// <summary>
    /// Enumera paquetes de Microsoft Store (Appx) vía PowerShell. Lento; se invoca aparte y opcionalmente.
    /// </summary>
    public Task<List<InstalledProgram>> DetectAppxAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var list = new List<InstalledProgram>();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -Command " +
                                "\"Get-AppxPackage | Where-Object { -not $_.IsFramework } | " +
                                "ForEach-Object { \\\"$($_.Name)|$($_.Publisher)|$($_.Version)|$($_.PackageFullName)|$($_.InstallLocation)\\\" }\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return list;
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(60_000);

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    ct.ThrowIfCancellationRequested();
                    var parts = line.Trim().Split('|');
                    if (parts.Length < 4 || string.IsNullOrWhiteSpace(parts[0])) continue;
                    list.Add(new InstalledProgram
                    {
                        Name = parts[0],
                        Publisher = parts[1],
                        Version = parts[2],
                        Id = parts[3],
                        InstallLocation = parts.Length > 4 ? parts[4] : string.Empty,
                        IsAppxPackage = true,
                        Source = "Appx",
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"DetectAppx falló: {ex.Message}");
            }
            return list;
        }, ct);
    }
}
