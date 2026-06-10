using Microsoft.Win32;
using CleanWipe.Core.Helpers;
using CleanWipe.Core.Models;

namespace CleanWipe.Core.Services;

/// <summary>
/// Escanea el sistema buscando carpetas residuales (huérfanas) de programas que ya
/// no figuran como instalados. Compara subcarpetas de AppData/ProgramData contra la
/// lista de programas instalados; lo que no coincide y no está protegido se reporta.
/// </summary>
public class OrphanScanner
{
    private readonly ProgramDetector _detector = new();

    public async Task<List<TraceItem>> ScanAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        progress?.Report("Detectando programas instalados...");
        var installed = await _detector.DetectAsync(ct);

        // Conjunto de tokens de todos los programas instalados (para no marcar lo vigente).
        var installedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in installed)
        {
            foreach (var t in PathHelper.Tokenize(p.Name)) installedTokens.Add(t);
            foreach (var t in PathHelper.Tokenize(p.Publisher)) installedTokens.Add(t);
        }

        // Carpetas conocidas de fabricantes vigentes para descartar falsos positivos comunes.
        var knownPublishers = await GetUninstallInstallLocationsAsync(ct);

        return await Task.Run(() =>
        {
            var orphans = new List<TraceItem>();
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var parents = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.Combine(profile, "AppData", "LocalLow"),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            };

            foreach (var parent in parents)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) continue;
                progress?.Report($"Analizando {Path.GetFileName(parent)}...");

                IEnumerable<string> subdirs;
                try { subdirs = Directory.EnumerateDirectories(parent); }
                catch { continue; }

                foreach (var dir in subdirs)
                {
                    ct.ThrowIfCancellationRequested();
                    string nameOnly = Path.GetFileName(dir);

                    // Salta carpetas comunes del sistema/Microsoft y vigentes.
                    if (IsCommonSystemFolder(nameOnly)) continue;
                    var folderTokens = PathHelper.Tokenize(nameOnly).ToList();
                    if (folderTokens.Count == 0) continue;
                    if (folderTokens.Any(t => installedTokens.Contains(t))) continue;
                    if (knownPublishers.Any(loc => loc.Contains(nameOnly, StringComparison.OrdinalIgnoreCase))) continue;

                    if (!SafetyValidator.IsPathSafe(dir, out _)) continue;

                    orphans.Add(new TraceItem
                    {
                        Type = TraceType.Folder,
                        Path = dir,
                        Description = $"Huérfano en {Path.GetFileName(parent)}",
                        SizeBytes = PathHelper.GetDirectorySize(dir),
                        IsIncluded = false, // por seguridad, el usuario debe marcar explícitamente
                    });
                }
            }

            return orphans.OrderByDescending(o => o.SizeBytes).ToList();
        }, ct);
    }

    private static async Task<List<string>> GetUninstallInstallLocationsAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var locations = new List<string>();
            var roots = new (RegistryKey hive, string path)[]
            {
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            };
            foreach (var (hive, path) in roots)
            {
                using var root = hive.OpenSubKey(path);
                if (root == null) continue;
                foreach (var sub in root.GetSubKeyNames())
                {
                    ct.ThrowIfCancellationRequested();
                    using var key = root.OpenSubKey(sub);
                    if (key?.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
                        locations.Add(loc);
                }
            }
            return locations;
        }, ct);
    }

    private static bool IsCommonSystemFolder(string name)
    {
        var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft", "Windows", "Packages", "Temp", "Comms", "ConnectedDevicesPlatform",
            "D3DSCache", "ElevatedDiagnostics", "PackageStaging", "Programs", "VirtualStore",
            "CrashDumps", "Publishers", "INetCache", "INetCookies", "History", "WER",
            "Application Data", "GroupPolicy", "Caches", "CLR_v4.0", "deployment", "IsolatedStorage",
        };
        return common.Contains(name);
    }
}
