using System.Diagnostics;
using Microsoft.Win32;
using CleanWipe.Core.Helpers;
using CleanWipe.Core.Models;

namespace CleanWipe.Core.Services;

/// <summary>
/// Busca rastros residuales de un programa en sistema de archivos, registro, accesos directos,
/// servicios y tareas programadas. Cada candidato se valida con <see cref="SafetyValidator"/>;
/// los protegidos se incluyen en el resultado marcados como no eliminables.
/// </summary>
public class TraceScanner
{
    /// <summary>Realiza el análisis completo de rastros para un programa.</summary>
    public Task<ScanResult> ScanAsync(InstalledProgram program, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var result = new ScanResult { Program = program };
            var tokens = PathHelper.Tokenize(program.Name)
                .Concat(PathHelper.Tokenize(program.Publisher))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Sin tokens significativos no podemos emparejar con seguridad.
            if (tokens.Count == 0)
            {
                AppLogger.Warn($"TraceScanner: '{program.Name}' no produjo tokens; sólo se listará la clave de desinstalación.");
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ScanFileSystem(program, tokens, result, seen, ct);
            ScanRegistry(program, tokens, result, seen, ct);
            ScanShortcuts(program, tokens, result, seen, ct);
            ScanServices(program, tokens, result, seen, ct);
            ScanScheduledTasks(program, tokens, result, seen, ct);

            program.HasResidualFiles = result.Traces.Any(t => !t.IsSystemProtected);
            return result;
        }, ct);
    }

    // ---------------- Sistema de archivos ----------------

    private static void ScanFileSystem(InstalledProgram program, List<string> tokens,
        ScanResult result, HashSet<string> seen, CancellationToken ct)
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var parents = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),      // Roaming
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), // Local
            Path.Combine(profile, "AppData", "LocalLow"),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), // ProgramData
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.Personal),             // Documents
        };

        foreach (var parent in parents)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) continue;

            IEnumerable<string> subdirs;
            try { subdirs = Directory.EnumerateDirectories(parent); }
            catch { continue; }

            foreach (var dir in subdirs)
            {
                ct.ThrowIfCancellationRequested();
                string nameOnly = Path.GetFileName(dir);
                if (!PathHelper.LooksLikeMatch(nameOnly, tokens)) continue;
                AddFolder(dir, $"Carpeta residual en {Path.GetFileName(parent)}", result, seen);
            }
        }

        // La propia carpeta de instalación (si quedó tras el desinstalador nativo).
        if (!string.IsNullOrWhiteSpace(program.InstallLocation) && Directory.Exists(program.InstallLocation))
            AddFolder(program.InstallLocation, "Carpeta de instalación", result, seen);
    }

    // ---------------- Registro ----------------

    private static void ScanRegistry(InstalledProgram program, List<string> tokens,
        ScanResult result, HashSet<string> seen, CancellationToken ct)
    {
        // La clave de desinstalación concreta del programa.
        if (!string.IsNullOrEmpty(program.Id) && program.Id.Contains(':'))
        {
            string source = program.Id[..program.Id.IndexOf(':')];
            string keyName = program.Id[(program.Id.IndexOf(':') + 1)..];
            string? uninstallCanonical = source switch
            {
                "HKLM" => $@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{keyName}",
                "HKLM-WOW6432" => $@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{keyName}",
                "HKCU" => $@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{keyName}",
                _ => null
            };
            if (uninstallCanonical != null && RegistryHelper.KeyExists(uninstallCanonical))
                AddRegistry(uninstallCanonical, "Entrada del desinstalador (Uninstall)", result, seen);
        }

        var softwareRoots = new[]
        {
            @"HKCU\SOFTWARE",
            @"HKLM\SOFTWARE",
            @"HKLM\SOFTWARE\WOW6432Node",
        };

        foreach (var rootPath in softwareRoots)
        {
            ct.ThrowIfCancellationRequested();
            using var root = RegistryHelper.OpenKey(rootPath);
            if (root == null) continue;

            string[] subKeys;
            try { subKeys = root.GetSubKeyNames(); }
            catch { continue; }

            foreach (var sub in subKeys)
            {
                ct.ThrowIfCancellationRequested();
                if (!PathHelper.LooksLikeMatch(sub, tokens)) continue;
                AddRegistry($@"{rootPath}\{sub}", "Clave de registro del programa", result, seen);
            }
        }
    }

    // ---------------- Accesos directos ----------------

    private static void ScanShortcuts(InstalledProgram program, List<string> tokens,
        ScanResult result, HashSet<string> seen, CancellationToken ct)
    {
        var shortcutDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };

        foreach (var dir in shortcutDirs)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

            IEnumerable<string> links;
            try { links = Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories); }
            catch { continue; }

            foreach (var lnk in links)
            {
                ct.ThrowIfCancellationRequested();
                string nameOnly = Path.GetFileNameWithoutExtension(lnk);
                if (!PathHelper.LooksLikeMatch(nameOnly, tokens)) continue;
                AddTrace(new TraceItem
                {
                    Type = TraceType.Shortcut,
                    Path = lnk,
                    SizeBytes = PathHelper.GetFileSize(lnk),
                    Description = "Acceso directo",
                }, result, seen);
            }
        }
    }

    // ---------------- Servicios (sólo lectura) ----------------

    private static void ScanServices(InstalledProgram program, List<string> tokens,
        ScanResult result, HashSet<string> seen, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(program.InstallLocation)) return;
        string installNorm;
        try { installNorm = SafetyValidator.NormalizePath(program.InstallLocation); }
        catch { return; }

        using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        if (services == null) return;

        string[] names;
        try { names = services.GetSubKeyNames(); }
        catch { return; }

        foreach (var svcName in names)
        {
            ct.ThrowIfCancellationRequested();
            using var svc = services.OpenSubKey(svcName);
            string? imagePath = svc?.GetValue("ImagePath") as string;
            if (string.IsNullOrWhiteSpace(imagePath)) continue;

            string expanded = PathHelper.Expand(imagePath.Trim('"'));
            if (expanded.Contains(installNorm, StringComparison.OrdinalIgnoreCase))
            {
                AddTrace(new TraceItem
                {
                    Type = TraceType.Service,
                    Path = svcName,
                    Description = $"Servicio de Windows ({svcName})",
                }, result, seen);
            }
        }
    }

    // ---------------- Tareas programadas ----------------

    private static void ScanScheduledTasks(InstalledProgram program, List<string> tokens,
        ScanResult result, HashSet<string> seen, CancellationToken ct)
    {
        if (tokens.Count == 0) return;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = "/Query /FO LIST",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(30_000);

            foreach (var line in output.Split('\n'))
            {
                ct.ThrowIfCancellationRequested();
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("TaskName:", StringComparison.OrdinalIgnoreCase)) continue;
                string taskName = trimmed["TaskName:".Length..].Trim();
                string leaf = taskName.TrimEnd('\\');
                leaf = leaf.Contains('\\') ? leaf[(leaf.LastIndexOf('\\') + 1)..] : leaf;
                if (!PathHelper.LooksLikeMatch(leaf, tokens)) continue;
                // Ignora tareas del sistema de Microsoft.
                if (taskName.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase)) continue;

                AddTrace(new TraceItem
                {
                    Type = TraceType.ScheduledTask,
                    Path = taskName,
                    Description = "Tarea programada",
                }, result, seen);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"ScanScheduledTasks falló: {ex.Message}");
        }
    }

    // ---------------- Helpers de adición ----------------

    private static void AddFolder(string path, string description, ScanResult result, HashSet<string> seen)
    {
        bool safe = SafetyValidator.IsPathSafe(path, out string reason);
        AddTrace(new TraceItem
        {
            Type = TraceType.Folder,
            Path = path,
            Description = description,
            SizeBytes = safe ? PathHelper.GetDirectorySize(path) : 0,
            IsSystemProtected = !safe,
            IsIncluded = safe,
            SkipReason = safe ? null : reason,
        }, result, seen);
    }

    private static void AddRegistry(string canonical, string description, ScanResult result, HashSet<string> seen)
    {
        bool safe = SafetyValidator.IsRegistryKeySafe(canonical, out string reason);
        AddTrace(new TraceItem
        {
            Type = TraceType.RegistryKey,
            Path = canonical,
            Description = description,
            IsSystemProtected = !safe,
            IsIncluded = safe,
            SkipReason = safe ? null : reason,
        }, result, seen);
    }

    private static void AddTrace(TraceItem item, ScanResult result, HashSet<string> seen)
    {
        string key = $"{item.Type}|{item.Path}";
        if (!seen.Add(key)) return;
        result.Traces.Add(item);
    }
}
