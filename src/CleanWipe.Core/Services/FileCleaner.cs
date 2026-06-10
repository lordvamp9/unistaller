using System.Runtime.InteropServices;

namespace CleanWipe.Core.Services;

/// <summary>
/// Elimina archivos y carpetas residuales. Opcionalmente envía a la Papelera de Reciclaje.
/// Si un archivo está bloqueado, lo programa para eliminación al reiniciar (MoveFileEx)
/// en lugar de forzar. Re-valida con SafetyValidator antes de CADA borrado (defensa en profundidad).
/// </summary>
public class FileCleaner
{
    /// <summary>Resultado de una operación de borrado.</summary>
    public enum DeleteOutcome { Deleted, ScheduledOnReboot, Blocked, NotFound, Failed }

    public DeleteOutcome DeleteFolder(string path, bool useRecycleBin, out string message)
    {
        message = string.Empty;

        if (!SafetyValidator.IsPathSafe(path, out string reason))
        {
            message = $"Bloqueado por seguridad: {reason}";
            AppLogger.Warn($"FileCleaner rechazó carpeta '{path}': {reason}");
            return DeleteOutcome.Blocked;
        }

        if (!Directory.Exists(path))
        {
            message = "No existe.";
            return DeleteOutcome.NotFound;
        }

        try
        {
            if (useRecycleBin)
            {
                if (SendToRecycleBin(path)) { AppLogger.Info($"Carpeta a papelera: {path}"); return DeleteOutcome.Deleted; }
            }
            Directory.Delete(path, recursive: true);
            AppLogger.Info($"Carpeta eliminada: {path}");
            return DeleteOutcome.Deleted;
        }
        catch (UnauthorizedAccessException)
        {
            return TryScheduleReboot(path, isFolder: true, out message);
        }
        catch (IOException)
        {
            return TryScheduleReboot(path, isFolder: true, out message);
        }
        catch (Exception ex)
        {
            message = ex.Message;
            AppLogger.Error($"Error eliminando carpeta '{path}'", ex);
            return DeleteOutcome.Failed;
        }
    }

    public DeleteOutcome DeleteFile(string path, bool useRecycleBin, out string message)
    {
        message = string.Empty;

        if (!SafetyValidator.IsPathSafe(path, out string reason))
        {
            message = $"Bloqueado por seguridad: {reason}";
            AppLogger.Warn($"FileCleaner rechazó archivo '{path}': {reason}");
            return DeleteOutcome.Blocked;
        }

        if (!File.Exists(path))
        {
            message = "No existe.";
            return DeleteOutcome.NotFound;
        }

        try
        {
            if (useRecycleBin)
            {
                if (SendToRecycleBin(path)) { AppLogger.Info($"Archivo a papelera: {path}"); return DeleteOutcome.Deleted; }
            }
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
            AppLogger.Info($"Archivo eliminado: {path}");
            return DeleteOutcome.Deleted;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return TryScheduleReboot(path, isFolder: false, out message);
        }
        catch (Exception ex)
        {
            message = ex.Message;
            AppLogger.Error($"Error eliminando archivo '{path}'", ex);
            return DeleteOutcome.Failed;
        }
    }

    private DeleteOutcome TryScheduleReboot(string path, bool isFolder, out string message)
    {
        // No forzamos sobre archivos en uso: los marcamos para eliminación al reiniciar.
        try
        {
            bool ok = MoveFileEx(path, null, MOVEFILE_DELAY_UNTIL_REBOOT);
            if (ok)
            {
                message = "En uso: se eliminará al reiniciar.";
                AppLogger.Info($"Programado para eliminación en reinicio: {path}");
                return DeleteOutcome.ScheduledOnReboot;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"MoveFileEx falló para '{path}': {ex.Message}");
        }
        message = "En uso y no se pudo programar su eliminación.";
        return DeleteOutcome.Failed;
    }

    // ---------------- Interop ----------------

    private static bool SendToRecycleBin(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI),
        };
        int result = SHFileOperation(ref op);
        return result == 0 && op.fAnyOperationsAborted == 0;
    }

    private const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, int dwFlags);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
