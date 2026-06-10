using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanWipe.Core.Models;

/// <summary>
/// Un rastro residual concreto (archivo, carpeta, clave de registro, acceso directo, tarea o servicio).
/// Es observable para que el usuario pueda marcar/desmarcar su inclusión en la limpieza desde la UI.
/// </summary>
public partial class TraceItem : ObservableObject
{
    public TraceType Type { get; set; }

    public string Path { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>Descripción legible mostrada en el árbol de pre-scan.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// True si la ruta/clave está protegida por <see cref="Services.SafetyValidator"/>.
    /// Estos ítems se muestran pero NUNCA pueden incluirse en la limpieza.
    /// </summary>
    public bool IsSystemProtected { get; set; }

    /// <summary>El usuario puede desmarcar para excluir de la limpieza.</summary>
    [ObservableProperty]
    private bool _isIncluded = true;

    /// <summary>Motivo por el que un ítem fue omitido (rellenado por el motor durante la limpieza).</summary>
    public string? SkipReason { get; set; }

    public string SizeReadable => FormatSize(SizeBytes);

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "—";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }
}
