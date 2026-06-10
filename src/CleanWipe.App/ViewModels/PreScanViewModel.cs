using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CleanWipe.App.Services;
using CleanWipe.Core.Models;
using CleanWipe.Core.Services;

namespace CleanWipe.App.ViewModels;

/// <summary>Vista 2 — Pre-Scan: árbol de rastros a eliminar con checkboxes y resumen.</summary>
public partial class PreScanViewModel : ObservableObject
{
    private readonly NavigationService _nav;
    private readonly TraceScanner _scanner = new();
    private ScanResult? _scan;

    public InstalledProgram Program { get; }

    public ObservableCollection<TraceItem> Files { get; } = new();
    public ObservableCollection<TraceItem> Folders { get; } = new();
    public ObservableCollection<TraceItem> RegistryKeys { get; } = new();
    public ObservableCollection<TraceItem> Shortcuts { get; } = new();
    public ObservableCollection<TraceItem> Tasks { get; } = new();
    public ObservableCollection<TraceItem> Services { get; } = new();

    [ObservableProperty] private bool _isScanning = true;
    [ObservableProperty] private int _includedCount;
    [ObservableProperty] private string _summary = "Analizando...";

    public PreScanViewModel(NavigationService nav, InstalledProgram program)
    {
        _nav = nav;
        Program = program;
        _ = ScanAsync();
    }

    private async Task ScanAsync()
    {
        IsScanning = true;
        _scan = await _scanner.ScanAsync(Program);

        Fill(Files, _scan.Files);
        Fill(Folders, _scan.Folders);
        Fill(RegistryKeys, _scan.RegistryKeys);
        Fill(Shortcuts, _scan.Shortcuts);
        Fill(Tasks, _scan.Tasks);
        Fill(Services, _scan.Services);

        foreach (var t in _scan.Traces)
            t.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TraceItem.IsIncluded)) UpdateSummary(); };

        IsScanning = false;
        UpdateSummary();
    }

    private static void Fill(ObservableCollection<TraceItem> target, IEnumerable<TraceItem> src)
    {
        target.Clear();
        foreach (var item in src) target.Add(item);
    }

    private void UpdateSummary()
    {
        if (_scan == null) return;
        IncludedCount = _scan.IncludedCount;
        long bytes = _scan.IncludedBytes;
        Summary = $"Se eliminarán {IncludedCount} ítems · {FormatSize(bytes)}";
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (_scan == null) return;
        foreach (var t in _scan.Traces.Where(t => !t.IsSystemProtected)) t.IsIncluded = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        if (_scan == null) return;
        foreach (var t in _scan.Traces.Where(t => !t.IsSystemProtected)) t.IsIncluded = false;
    }

    [RelayCommand]
    private void Back() => _nav.Navigate(new DashboardViewModel(_nav));

    [RelayCommand]
    private void StartUninstall()
    {
        if (_scan == null) return;
        var options = new UninstallOptions
        {
            UseRecycleBin = AppSettings.Current.UseRecycleBin,
            RunNativeUninstaller = AppSettings.Current.RunNativeUninstaller,
        };
        _nav.Navigate(new ProgressViewModel(_nav, _scan, options));
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }
}
