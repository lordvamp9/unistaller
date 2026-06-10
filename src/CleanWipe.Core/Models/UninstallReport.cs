namespace CleanWipe.Core.Models;

/// <summary>
/// Reporte final de una operación de desinstalación, serializable a JSON/TXT.
/// </summary>
public class UninstallReport
{
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramVersion { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public List<TraceItem> DeletedItems { get; set; } = new();
    public List<TraceItem> SkippedItems { get; set; } = new();

    public long TotalBytesReclaimed { get; set; }
    public bool NativeUninstallerSuccess { get; set; }
    public string NativeUninstallerExitCode { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }

    /// <summary>Errores no fatales encontrados durante la operación.</summary>
    public List<string> Warnings { get; set; } = new();

    public string TotalReadable => FormatSize(TotalBytesReclaimed);

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
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
