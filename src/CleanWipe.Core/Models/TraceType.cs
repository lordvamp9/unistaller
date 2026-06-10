namespace CleanWipe.Core.Models;

/// <summary>
/// Categoría de un rastro residual detectado en el sistema.
/// </summary>
public enum TraceType
{
    File,
    Folder,
    RegistryKey,
    Shortcut,
    ScheduledTask,
    Service
}
