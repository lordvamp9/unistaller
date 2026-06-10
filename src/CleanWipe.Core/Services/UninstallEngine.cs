using System.Diagnostics;
using CleanWipe.Core.Models;

namespace CleanWipe.Core.Services;

/// <summary>Opciones de una operación de desinstalación.</summary>
public class UninstallOptions
{
    /// <summary>Enviar archivos/carpetas a la Papelera en lugar de borrado directo.</summary>
    public bool UseRecycleBin { get; set; }

    /// <summary>Ejecutar primero el desinstalador nativo de Windows.</summary>
    public bool RunNativeUninstaller { get; set; } = true;

    /// <summary>Timeout para el desinstalador nativo.</summary>
    public TimeSpan NativeTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>Reporte de progreso emitido por el motor hacia la UI.</summary>
public class UninstallProgress
{
    public int Percent { get; set; }
    public string Status { get; set; } = string.Empty;
    public TraceItem? CompletedItem { get; set; }
}

/// <summary>
/// Orquesta la desinstalación completa en 7 pasos: snapshot → desinstalador nativo →
/// archivos → registro → accesos/servicios/tareas → verificación → log/reporte.
/// Reporta progreso vía IProgress y respeta CancellationToken.
/// </summary>
public class UninstallEngine
{
    private readonly FileCleaner _fileCleaner = new();
    private readonly RegistryCleaner _registryCleaner = new();
    private readonly ReportGenerator _reportGenerator = new();

    public async Task<UninstallReport> ExecuteAsync(
        ScanResult scan,
        UninstallOptions options,
        IProgress<UninstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var report = new UninstallReport
        {
            ProgramName = scan.Program.Name,
            ProgramVersion = scan.Program.Version,
            Publisher = scan.Program.Publisher,
        };

        AppLogger.Info($"=== Inicio desinstalación: {scan.Program.Name} {scan.Program.Version} ===");

        // Ítems que el usuario decidió incluir y que no están protegidos.
        var items = scan.Traces.Where(t => t.IsIncluded && !t.IsSystemProtected).ToList();
        int totalSteps = items.Count + 2; // +nativo +verificación
        int done = 0;

        void Report(string status, TraceItem? item = null)
        {
            int pct = totalSteps == 0 ? 100 : (int)Math.Min(100, done * 100.0 / totalSteps);
            progress?.Report(new UninstallProgress { Percent = pct, Status = status, CompletedItem = item });
        }

        // PASO 2: desinstalador nativo (el snapshot/paso 1 es el ScanResult recibido).
        Report("Ejecutando desinstalador nativo...");
        if (options.RunNativeUninstaller && !scan.Program.IsAppxPackage)
        {
            var (success, code) = await RunNativeUninstallerAsync(scan.Program, options.NativeTimeout, ct);
            report.NativeUninstallerSuccess = success;
            report.NativeUninstallerExitCode = code;
            if (!success)
                report.Warnings.Add($"El desinstalador nativo no completó correctamente (código {code}). Se continúa con limpieza manual.");
        }
        else if (scan.Program.IsAppxPackage)
        {
            var (success, code) = await RemoveAppxAsync(scan.Program, ct);
            report.NativeUninstallerSuccess = success;
            report.NativeUninstallerExitCode = code;
        }
        else
        {
            report.NativeUninstallerExitCode = "omitido";
        }
        done++;
        Report("Desinstalador nativo finalizado.");

        // PASOS 3-5: limpieza de rastros.
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            Report($"Eliminando: {item.Path}", null);

            bool removed = item.Type switch
            {
                TraceType.Folder => Handle(_fileCleaner.DeleteFolder(item.Path, options.UseRecycleBin, out var m1), item, m1),
                TraceType.File => Handle(_fileCleaner.DeleteFile(item.Path, options.UseRecycleBin, out var m2), item, m2),
                TraceType.Shortcut => Handle(_fileCleaner.DeleteFile(item.Path, options.UseRecycleBin, out var m3), item, m3),
                TraceType.RegistryKey => HandleReg(_registryCleaner.DeleteKey(item.Path, out var m4), item, m4),
                TraceType.Service => RemoveService(item),
                TraceType.ScheduledTask => RemoveTask(item),
                _ => false
            };

            if (removed)
            {
                report.DeletedItems.Add(item);
                report.TotalBytesReclaimed += item.SizeBytes;
            }
            else
            {
                report.SkippedItems.Add(item);
            }

            done++;
            Report($"Procesado: {item.Path}", item);
        }

        // PASO 6: verificación post-limpieza.
        Report("Verificando rastros restantes...");
        VerifyResiduals(report);
        done++;

        sw.Stop();
        report.Duration = sw.Elapsed;
        Report("Completado.");

        // PASO 7: log/reporte en disco.
        try
        {
            string saved = await _reportGenerator.SaveJsonAsync(report);
            AppLogger.Info($"Reporte guardado: {saved}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo guardar el reporte JSON", ex);
        }

        AppLogger.Info($"=== Fin desinstalación: {scan.Program.Name} — {report.DeletedItems.Count} eliminados, {report.SkippedItems.Count} omitidos ===");
        return report;
    }

    private static bool Handle(FileCleaner.DeleteOutcome outcome, TraceItem item, string message)
    {
        if (outcome is FileCleaner.DeleteOutcome.Deleted or FileCleaner.DeleteOutcome.ScheduledOnReboot or FileCleaner.DeleteOutcome.NotFound)
        {
            if (outcome == FileCleaner.DeleteOutcome.ScheduledOnReboot) item.SkipReason = message;
            return outcome != FileCleaner.DeleteOutcome.NotFound; // "no existe" no cuenta como eliminado real
        }
        item.SkipReason = message;
        return false;
    }

    private static bool HandleReg(RegistryCleaner.DeleteOutcome outcome, TraceItem item, string message)
    {
        if (outcome == RegistryCleaner.DeleteOutcome.Deleted) return true;
        item.SkipReason = message;
        return false;
    }

    // ---------------- Desinstalador nativo ----------------

    private static async Task<(bool success, string exitCode)> RunNativeUninstallerAsync(
        InstalledProgram program, TimeSpan timeout, CancellationToken ct)
    {
        string command = !string.IsNullOrWhiteSpace(program.QuietUninstallString)
            ? program.QuietUninstallString
            : program.UninstallString;

        if (string.IsNullOrWhiteSpace(command))
            return (false, "sin-uninstall-string");

        var (file, args) = SplitCommand(command);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = true, // permite que el desinstalador muestre su UI/elevación propia
            };
            using var proc = Process.Start(psi);
            if (proc == null) return (false, "no-iniciado");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!ct.IsCancellationRequested)
                {
                    AppLogger.Warn($"Desinstalador nativo de '{program.Name}' superó el timeout.");
                    return (false, "timeout");
                }
                throw;
            }

            return (proc.ExitCode == 0, proc.ExitCode.ToString());
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Fallo al ejecutar desinstalador nativo de '{program.Name}'", ex);
            return (false, $"error: {ex.Message}");
        }
    }

    private static async Task<(bool success, string exitCode)> RemoveAppxAsync(InstalledProgram program, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"Remove-AppxPackage -Package '{program.Id}'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return (false, "no-iniciado");
            await proc.WaitForExitAsync(ct);
            return (proc.ExitCode == 0, proc.ExitCode.ToString());
        }
        catch (Exception ex)
        {
            return (false, $"error: {ex.Message}");
        }
    }

    /// <summary>Separa "C:\ruta con espacios\setup.exe" /args en (archivo, argumentos).</summary>
    private static (string file, string args) SplitCommand(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end > 0)
                return (command[1..end], command[(end + 1)..].Trim());
        }
        int space = command.IndexOf(' ');
        return space < 0 ? (command, string.Empty) : (command[..space], command[(space + 1)..].Trim());
    }

    // ---------------- Servicios y tareas ----------------

    private static bool RemoveService(TraceItem item)
    {
        try
        {
            RunSilent("sc.exe", $"stop \"{item.Path}\"");
            var (ok, _) = RunSilent("sc.exe", $"delete \"{item.Path}\"");
            if (ok) { AppLogger.Info($"Servicio eliminado: {item.Path}"); return true; }
            item.SkipReason = "No se pudo eliminar el servicio (¿requiere privilegios?).";
            return false;
        }
        catch (Exception ex)
        {
            item.SkipReason = ex.Message;
            return false;
        }
    }

    private static bool RemoveTask(TraceItem item)
    {
        try
        {
            var (ok, _) = RunSilent("schtasks.exe", $"/Delete /TN \"{item.Path}\" /F");
            if (ok) { AppLogger.Info($"Tarea programada eliminada: {item.Path}"); return true; }
            item.SkipReason = "No se pudo eliminar la tarea programada.";
            return false;
        }
        catch (Exception ex)
        {
            item.SkipReason = ex.Message;
            return false;
        }
    }

    private static (bool ok, int code) RunSilent(string file, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = Process.Start(psi);
        if (proc == null) return (false, -1);
        proc.WaitForExit(30_000);
        return (proc.ExitCode == 0, proc.ExitCode);
    }

    // ---------------- Verificación ----------------

    private static void VerifyResiduals(UninstallReport report)
    {
        foreach (var item in report.DeletedItems.ToList())
        {
            bool stillThere = item.Type switch
            {
                TraceType.Folder => Directory.Exists(item.Path),
                TraceType.File or TraceType.Shortcut => File.Exists(item.Path),
                TraceType.RegistryKey => Helpers.RegistryHelper.KeyExists(item.Path),
                _ => false
            };
            if (stillThere && string.IsNullOrEmpty(item.SkipReason))
            {
                report.Warnings.Add($"Aún presente tras la limpieza: {item.Path}");
            }
        }
    }
}
