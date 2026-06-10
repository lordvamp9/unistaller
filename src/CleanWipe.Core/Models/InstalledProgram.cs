namespace CleanWipe.Core.Models;

/// <summary>
/// Representa un programa instalado detectado por <see cref="Services.ProgramDetector"/>.
/// </summary>
public class InstalledProgram
{
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string UninstallString { get; set; } = string.Empty;
    public string QuietUninstallString { get; set; } = string.Empty;
    public DateTime? InstallDate { get; set; }
    public long EstimatedSizeMB { get; set; }
    public string IconPath { get; set; } = string.Empty;
    public bool HasResidualFiles { get; set; }

    /// <summary>Origen del registro/WMI desde donde se detectó (para diagnóstico).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Identificador único (clave de desinstalación o PackageFullName).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>True para paquetes de Microsoft Store (Appx) que usan otro mecanismo de desinstalación.</summary>
    public bool IsAppxPackage { get; set; }
}
