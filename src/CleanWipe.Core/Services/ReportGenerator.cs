using System.Text;
using CleanWipe.Core.Models;
using Newtonsoft.Json;

namespace CleanWipe.Core.Services;

/// <summary>Genera y guarda el reporte de desinstalación en JSON y TXT.</summary>
public class ReportGenerator
{
    public static string ReportDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CleanWipe", "logs");

    /// <summary>Guarda el reporte como JSON en %APPDATA%\CleanWipe\logs y devuelve la ruta.</summary>
    public async Task<string> SaveJsonAsync(UninstallReport report)
    {
        Directory.CreateDirectory(ReportDirectory);
        string safeName = MakeSafeFileName(report.ProgramName);
        string file = Path.Combine(ReportDirectory, $"{report.Timestamp:yyyyMMdd_HHmmss}_{safeName}.json");
        string json = JsonConvert.SerializeObject(report, Formatting.Indented);
        await File.WriteAllTextAsync(file, json, Encoding.UTF8);
        return file;
    }

    /// <summary>Serializa el reporte a JSON (para "Guardar reporte" desde la UI).</summary>
    public string ToJson(UninstallReport report) => JsonConvert.SerializeObject(report, Formatting.Indented);

    /// <summary>Genera un reporte legible en texto plano.</summary>
    public string ToText(UninstallReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("════════════════════════════════════════════");
        sb.AppendLine("  CleanWipe — Reporte de desinstalación");
        sb.AppendLine("════════════════════════════════════════════");
        sb.AppendLine($"Programa : {report.ProgramName} {report.ProgramVersion}");
        sb.AppendLine($"Publisher: {report.Publisher}");
        sb.AppendLine($"Fecha    : {report.Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duración : {report.Duration:mm\\:ss}");
        sb.AppendLine($"Espacio recuperado: {report.TotalReadable}");
        sb.AppendLine($"Desinstalador nativo: {(report.NativeUninstallerSuccess ? "OK" : "incompleto")} (código {report.NativeUninstallerExitCode})");
        sb.AppendLine();

        sb.AppendLine($"✅ Eliminados ({report.DeletedItems.Count}):");
        foreach (var item in report.DeletedItems)
            sb.AppendLine($"   [{item.Type}] {item.Path}");
        sb.AppendLine();

        if (report.SkippedItems.Count > 0)
        {
            sb.AppendLine($"⚠️ Omitidos ({report.SkippedItems.Count}):");
            foreach (var item in report.SkippedItems)
                sb.AppendLine($"   [{item.Type}] {item.Path} — {item.SkipReason}");
            sb.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("Avisos:");
            foreach (var w in report.Warnings)
                sb.AppendLine($"   - {w}");
        }

        return sb.ToString();
    }

    public async Task SaveAsync(UninstallReport report, string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        string content = ext == ".txt" ? ToText(report) : ToJson(report);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8);
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Length > 60 ? name[..60] : name;
    }
}
