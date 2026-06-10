using CleanWipe.Core.Helpers;

namespace CleanWipe.Core.Services;

/// <summary>
/// Elimina claves de registro residuales. Re-valida con SafetyValidator antes de CADA borrado.
/// </summary>
public class RegistryCleaner
{
    public enum DeleteOutcome { Deleted, Blocked, NotFound, Failed }

    public DeleteOutcome DeleteKey(string canonicalPath, out string message)
    {
        message = string.Empty;

        if (!SafetyValidator.IsRegistryKeySafe(canonicalPath, out string reason))
        {
            message = $"Bloqueado por seguridad: {reason}";
            AppLogger.Warn($"RegistryCleaner rechazó '{canonicalPath}': {reason}");
            return DeleteOutcome.Blocked;
        }

        if (!RegistryHelper.KeyExists(canonicalPath))
        {
            message = "No existe.";
            return DeleteOutcome.NotFound;
        }

        try
        {
            bool ok = RegistryHelper.DeleteKeyTree(canonicalPath);
            if (ok)
            {
                AppLogger.Info($"Clave de registro eliminada: {canonicalPath}");
                return DeleteOutcome.Deleted;
            }
            message = "No se pudo eliminar (acceso o permisos).";
            return DeleteOutcome.Failed;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            AppLogger.Error($"Error eliminando clave '{canonicalPath}'", ex);
            return DeleteOutcome.Failed;
        }
    }
}
