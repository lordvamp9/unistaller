using System.Text;

namespace CleanWipe.Core.Services;

/// <summary>
/// Capa de seguridad obligatoria. TODA operación de borrado (archivo, carpeta o registro)
/// debe pasar por <see cref="IsPathSafe(string)"/> o <see cref="IsRegistryKeySafe(string)"/>
/// antes de ejecutarse. Si devuelve false, la operación se cancela y se registra.
///
/// Reglas:
///  - Las rutas/claves del sistema y sus descendientes están totalmente prohibidas.
///  - Las carpetas/claves "raíz" del usuario (AppData, Documents, ProgramFiles, ...Uninstall)
///    están protegidas como tal, pero sus SUBcarpetas/SUBclaves sí pueden eliminarse
///    (ahí viven los rastros de un programa concreto).
///  - Las rutas se normalizan con GetFullPath para neutralizar trucos de traversal (..\..).
/// </summary>
public static class SafetyValidator
{
    // Directorio + TODOS sus descendientes están prohibidos.
    private static readonly string[] ForbiddenPathTrees;

    // SOLO la carpeta exacta está prohibida; sus hijos directos sí se permiten.
    private static readonly string[] ForbiddenPathExact;

    // Clave + TODOS sus descendientes están prohibidos (forma canónica HKxx\...).
    private static readonly string[] ForbiddenRegistryTrees;

    // SOLO la clave exacta está prohibida; sus hijos sí se permiten.
    private static readonly string[] ForbiddenRegistryExact;

    static SafetyValidator()
    {
        string Win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string Sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string SysX86 = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string winDrive = Path.GetPathRoot(Win) ?? "C:\\";

        var trees = new List<string?>
        {
            Win,
            Sys,
            SysX86,
            Path.Combine(Win, "WinSxS"),
            Path.Combine(Win, "System32"),
            Path.Combine(Win, "SysWOW64"),
            Path.Combine(winDrive, "Boot"),
            Path.Combine(winDrive, "$Recycle.Bin"),
            Path.Combine(winDrive, "$WinREAgent"),
            Path.Combine(winDrive, "System Volume Information"),
            Path.Combine(winDrive, "Recovery"),
            Path.Combine(winDrive, "ProgramData", "Microsoft"),
            Path.Combine(winDrive, "ProgramData", "Windows"),
            Path.Combine(winDrive, "ProgramData", "Package Cache"),
        };

        var exact = new List<string?>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), // C:\ProgramData
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),       // %APPDATA% (Roaming)
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),  // %LOCALAPPDATA%
            Path.Combine(profile, "AppData"),
            Path.Combine(profile, "AppData", "LocalLow"),
            profile,
            Path.GetDirectoryName(profile),                                             // C:\Users
            Environment.GetFolderPath(Environment.SpecialFolder.Personal),              // Documents
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(profile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),              // Start Menu\Programs
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.Templates),
            Environment.GetFolderPath(Environment.SpecialFolder.SendTo),
            Environment.GetFolderPath(Environment.SpecialFolder.Recent),
            Environment.GetFolderPath(Environment.SpecialFolder.Cookies),
            Environment.GetFolderPath(Environment.SpecialFolder.History),
        };

        ForbiddenPathTrees = trees
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => NormalizePath(p!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ForbiddenPathExact = exact
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => NormalizePath(p!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ForbiddenRegistryTrees = new[]
        {
            @"HKLM\SYSTEM",
            @"HKLM\HARDWARE",
            @"HKLM\SAM",
            @"HKLM\SECURITY",
            @"HKLM\SOFTWARE\Microsoft\Windows NT",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate",
            @"HKLM\SOFTWARE\Microsoft\.NETFramework",
            @"HKLM\SOFTWARE\Microsoft\NET Framework Setup",
            @"HKLM\SOFTWARE\Classes",
            @"HKCU\SOFTWARE\Classes",
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders",
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders",
        }.Select(NormalizeRegistry).ToArray();

        ForbiddenRegistryExact = new[]
        {
            @"HKLM",
            @"HKCU",
            @"HKCR",
            @"HKU",
            @"HKCC",
            @"HKLM\SOFTWARE",
            @"HKLM\SOFTWARE\WOW6432Node",
            @"HKLM\SOFTWARE\Microsoft",
            @"HKCU\SOFTWARE",
            @"HKCU\SOFTWARE\Microsoft",
            @"HKLM\SOFTWARE\Microsoft\Windows",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion",
            @"HKCU\SOFTWARE\Microsoft\Windows",
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        }.Select(NormalizeRegistry).ToArray();
    }

    /// <summary>Devuelve true solo si es seguro borrar la ruta indicada.</summary>
    public static bool IsPathSafe(string path) => IsPathSafe(path, out _);

    /// <summary>Versión con motivo de rechazo, útil para logging.</summary>
    public static bool IsPathSafe(string path, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "Ruta vacía.";
            return false;
        }

        string norm;
        try
        {
            norm = NormalizePath(path);
        }
        catch (Exception ex)
        {
            reason = $"Ruta inválida: {ex.Message}";
            return false;
        }

        // Raíz de unidad (C:\, D:\) — nunca.
        string? root = null;
        try { root = NormalizePath(Path.GetPathRoot(norm) ?? string.Empty); } catch { }
        if (string.IsNullOrEmpty(norm) || string.Equals(norm, root, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Es la raíz de la unidad.";
            return false;
        }

        // Carpetas raíz protegidas: la carpeta exacta no se toca, pero sus hijos sí.
        // (Las rutas de Windows son case-insensitive → comparación OrdinalIgnoreCase.)
        foreach (var exact in ForbiddenPathExact)
        {
            if (string.Equals(norm, exact, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Carpeta de sistema/usuario protegida: {exact}";
                return false;
            }
        }

        foreach (var tree in ForbiddenPathTrees)
        {
            if (string.Equals(norm, tree, StringComparison.OrdinalIgnoreCase) ||
                norm.StartsWith(tree + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Dentro de un árbol del sistema protegido: {tree}";
                return false;
            }
        }

        return true;
    }

    /// <summary>Devuelve true solo si es seguro borrar la clave de registro indicada.</summary>
    public static bool IsRegistryKeySafe(string keyPath) => IsRegistryKeySafe(keyPath, out _);

    /// <summary>Versión con motivo de rechazo, útil para logging.</summary>
    public static bool IsRegistryKeySafe(string keyPath, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(keyPath))
        {
            reason = "Clave vacía.";
            return false;
        }

        string norm = NormalizeRegistry(keyPath);
        var segments = norm.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        // Demasiado cerca de la raíz de una colmena (p.ej. HKLM\SOFTWARE).
        if (segments.Length < 3)
        {
            reason = "Clave demasiado cercana a la raíz de la colmena.";
            return false;
        }

        foreach (var exact in ForbiddenRegistryExact)
        {
            if (string.Equals(norm, exact, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Clave protegida: {exact}";
                return false;
            }
        }

        foreach (var tree in ForbiddenRegistryTrees)
        {
            if (string.Equals(norm, tree, StringComparison.OrdinalIgnoreCase) ||
                norm.StartsWith(tree + "\\", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Dentro de un árbol de registro protegido: {tree}";
                return false;
            }
        }

        return true;
    }

    /// <summary>Normaliza una ruta de archivo: GetFullPath + sin separador final + recorte.</summary>
    public static string NormalizePath(string path)
    {
        string full = Path.GetFullPath(path.Trim());
        if (full.Length > 3)
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return full;
    }

    /// <summary>
    /// Normaliza una clave de registro a la forma canónica "HKLM\Sub\Key".
    /// Acepta abreviaturas y nombres completos de colmena.
    /// </summary>
    public static string NormalizeRegistry(string keyPath)
    {
        string key = keyPath.Trim().Replace('/', '\\').Trim('\\');
        var parts = key.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return string.Empty;

        string hive = parts[0].ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => "HKLM",
            "HKCU" or "HKEY_CURRENT_USER" => "HKCU",
            "HKCR" or "HKEY_CLASSES_ROOT" => "HKCR",
            "HKU" or "HKEY_USERS" => "HKU",
            "HKCC" or "HKEY_CURRENT_CONFIG" => "HKCC",
            _ => parts[0].ToUpperInvariant()
        };

        var sb = new StringBuilder(hive);
        for (int i = 1; i < parts.Length; i++)
            sb.Append('\\').Append(parts[i]);
        return sb.ToString();
    }
}
