using Microsoft.Win32;

namespace CleanWipe.Core.Helpers;

/// <summary>
/// Utilidades para abrir/enumerar/eliminar claves de registro a partir de una ruta
/// canónica del tipo "HKLM\SOFTWARE\App". Sólo opera sobre HKLM y HKCU (las colmenas
/// relevantes para rastros de aplicaciones de usuario).
/// </summary>
public static class RegistryHelper
{
    /// <summary>Abre una clave a partir de su ruta canónica. Devuelve null si no existe.</summary>
    public static RegistryKey? OpenKey(string canonicalPath, bool writable = false)
    {
        var (root, subPath) = Split(canonicalPath);
        if (root == null || subPath == null) return null;
        try
        {
            return root.OpenSubKey(subPath, writable);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True si la clave existe.</summary>
    public static bool KeyExists(string canonicalPath)
    {
        using var key = OpenKey(canonicalPath);
        return key != null;
    }

    /// <summary>
    /// Elimina la clave (y su subárbol). Devuelve true si se eliminó.
    /// NO valida seguridad: el llamador DEBE haber pasado por SafetyValidator primero.
    /// </summary>
    public static bool DeleteKeyTree(string canonicalPath)
    {
        var (root, subPath) = Split(canonicalPath);
        if (root == null || string.IsNullOrEmpty(subPath)) return false;
        try
        {
            root.DeleteSubKeyTree(subPath, throwOnMissingSubKey: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Divide una ruta canónica en (hive base, subclave).</summary>
    public static (RegistryKey? root, string? subPath) Split(string canonicalPath)
    {
        if (string.IsNullOrWhiteSpace(canonicalPath)) return (null, null);
        string p = canonicalPath.Trim().Trim('\\');
        int idx = p.IndexOf('\\');
        string hive = idx < 0 ? p : p[..idx];
        string sub = idx < 0 ? string.Empty : p[(idx + 1)..];

        RegistryKey? root = hive.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKU" or "HKEY_USERS" => Registry.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => null
        };
        return (root, sub);
    }

    /// <summary>Lee un valor string de una clave canónica, o null.</summary>
    public static string? ReadString(string canonicalPath, string valueName)
    {
        using var key = OpenKey(canonicalPath);
        return key?.GetValue(valueName) as string;
    }
}
