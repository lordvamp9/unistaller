namespace CleanWipe.Core.Models;

/// <summary>
/// Resultado de un análisis pre-desinstalación: todos los rastros encontrados para un programa.
/// </summary>
public class ScanResult
{
    public InstalledProgram Program { get; set; } = new();

    public List<TraceItem> Traces { get; set; } = new();

    public IEnumerable<TraceItem> Files => Traces.Where(t => t.Type == TraceType.File);
    public IEnumerable<TraceItem> Folders => Traces.Where(t => t.Type == TraceType.Folder);
    public IEnumerable<TraceItem> RegistryKeys => Traces.Where(t => t.Type == TraceType.RegistryKey);
    public IEnumerable<TraceItem> Shortcuts => Traces.Where(t => t.Type == TraceType.Shortcut);
    public IEnumerable<TraceItem> Tasks => Traces.Where(t => t.Type == TraceType.ScheduledTask);
    public IEnumerable<TraceItem> Services => Traces.Where(t => t.Type == TraceType.Service);

    public int IncludedCount => Traces.Count(t => t.IsIncluded && !t.IsSystemProtected);
    public long IncludedBytes => Traces.Where(t => t.IsIncluded && !t.IsSystemProtected).Sum(t => t.SizeBytes);
}
