using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CleanWipe.App.Services;
using CleanWipe.Core.Models;
using CleanWipe.Core.Services;

namespace CleanWipe.App.ViewModels;

/// <summary>Vista 5 — Orphan Scanner: detecta y limpia carpetas residuales de programas ya desinstalados.</summary>
public partial class OrphanScanViewModel : ObservableObject
{
    private readonly OrphanScanner _scanner = new();
    private readonly FileCleaner _cleaner = new();

    public ObservableCollection<TraceItem> Orphans { get; } = new();

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _status = "Pulsa «Escanear» para buscar carpetas huérfanas.";
    [ObservableProperty] private bool _hasScanned;

    public long SelectedBytes => Orphans.Where(o => o.IsIncluded).Sum(o => o.SizeBytes);

    [RelayCommand]
    private async Task Scan()
    {
        IsScanning = true;
        HasScanned = false;
        Orphans.Clear();
        var progress = new Progress<string>(s => Status = s);
        try
        {
            var found = await _scanner.ScanAsync(progress);
            foreach (var o in found)
            {
                o.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TraceItem.IsIncluded)) OnPropertyChanged(nameof(SelectedBytes)); };
                Orphans.Add(o);
            }
            Status = found.Count == 0
                ? "No se encontraron carpetas huérfanas. ¡Tu sistema está limpio!"
                : $"{found.Count} carpetas huérfanas encontradas. Marca las que quieras eliminar.";
        }
        finally
        {
            IsScanning = false;
            HasScanned = true;
        }
    }

    [RelayCommand]
    private async Task CleanSelected()
    {
        var toClean = Orphans.Where(o => o.IsIncluded).ToList();
        if (toClean.Count == 0) { Status = "No hay carpetas seleccionadas."; return; }

        int removed = 0;
        await Task.Run(() =>
        {
            foreach (var o in toClean)
            {
                var outcome = _cleaner.DeleteFolder(o.Path, AppSettings.Current.UseRecycleBin, out _);
                if (outcome is FileCleaner.DeleteOutcome.Deleted or FileCleaner.DeleteOutcome.ScheduledOnReboot)
                    removed++;
            }
        });

        foreach (var o in toClean) Orphans.Remove(o);
        OnPropertyChanged(nameof(SelectedBytes));
        Status = $"{removed} carpetas eliminadas.";
    }
}
