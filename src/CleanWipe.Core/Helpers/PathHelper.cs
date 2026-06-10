using System.Text.RegularExpressions;

namespace CleanWipe.Core.Helpers;

/// <summary>Utilidades de sistema de archivos: tamaño de carpeta, expansión de variables y heurísticas de pertenencia.</summary>
public static class PathHelper
{
    /// <summary>Calcula el tamaño total (bytes) de un directorio. Tolerante a errores de acceso.</summary>
    public static long GetDirectorySize(string path)
    {
        long total = 0;
        try
        {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists) return 0;
            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { total += file.Length; } catch { /* archivo bloqueado / sin permiso */ }
            }
        }
        catch
        {
            // Acceso denegado a parte del árbol: devolvemos lo acumulado.
        }
        return total;
    }

    /// <summary>Tamaño de un único archivo, o 0 si no accesible.</summary>
    public static long GetFileSize(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            return fi.Exists ? fi.Length : 0;
        }
        catch { return 0; }
    }

    /// <summary>Expande %VAR% y devuelve la ruta completa.</summary>
    public static string Expand(string path) => Environment.ExpandEnvironmentVariables(path ?? string.Empty);

    /// <summary>
    /// Genera tokens significativos a partir de un nombre de programa/publisher
    /// (descarta palabras genéricas y sufijos de versión) para emparejar carpetas/claves.
    /// </summary>
    public static IEnumerable<string> Tokenize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) yield break;

        // Quita contenido entre paréntesis y números de versión.
        string cleaned = Regex.Replace(name, @"\(.*?\)", " ");
        cleaned = Regex.Replace(cleaned, @"\bv?\d+(\.\d+)+\b", " ");

        var generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "inc","llc","ltd","corp","corporation","company","co","gmbh","srl","sa",
            "the","and","for","software","technologies","technology","systems","group",
            "international","limited","studio","studios","app","application","windows",
            "x64","x86","32","64","bit","edition","version","setup","installer","update"
        };

        foreach (Match m in Regex.Matches(cleaned, @"[A-Za-z0-9]+"))
        {
            string token = m.Value;
            if (token.Length < 3) continue;
            if (generic.Contains(token)) continue;
            yield return token;
        }
    }

    /// <summary>
    /// Heurística: ¿el nombre de la carpeta/clave parece pertenecer al programa o publisher?
    /// Exige coincidencia de un token significativo (≥3 chars) para reducir falsos positivos.
    /// </summary>
    public static bool LooksLikeMatch(string candidateName, IReadOnlyCollection<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(candidateName) || tokens.Count == 0) return false;
        foreach (var token in tokens)
        {
            if (candidateName.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
