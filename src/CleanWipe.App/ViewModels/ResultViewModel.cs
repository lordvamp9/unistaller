using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using CleanWipe.App.Services;
using CleanWipe.Core.Models;
using CleanWipe.Core.Services;

namespace CleanWipe.App.ViewModels;

/// <summary>Vista 4 — Resultado: reporte final con acordeón de detalles y exportación.</summary>
public partial class ResultViewModel : ObservableObject
{
    private readonly NavigationService _nav;
    private readonly ReportGenerator _reportGenerator = new();

    public UninstallReport Report { get; }

    public ObservableCollection<TraceItem> DeletedItems { get; } = new();
    public ObservableCollection<TraceItem> SkippedItems { get; } = new();

    public string Title => $"{Report.ProgramName} eliminado";
    public string Subtitle => $"{Report.DeletedItems.Count} ítems eliminados · {Report.TotalReadable} recuperados";
    public bool HasSkipped => SkippedItems.Count > 0;

    [ObservableProperty] private string _saveStatus = string.Empty;

    public ResultViewModel(NavigationService nav, UninstallReport report)
    {
        _nav = nav;
        Report = report;
        foreach (var i in report.DeletedItems) DeletedItems.Add(i);
        foreach (var i in report.SkippedItems) SkippedItems.Add(i);
    }

    [RelayCommand]
    private async Task SaveReport()
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"CleanWipe_{Report.ProgramName}_{Report.Timestamp:yyyyMMdd_HHmmss}",
            DefaultExt = ".json",
            Filter = "Reporte JSON (*.json)|*.json|Texto (*.txt)|*.txt",
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _reportGenerator.SaveAsync(Report, dialog.FileName);
                SaveStatus = $"Reporte guardado en {dialog.FileName}";
            }
            catch (Exception ex)
            {
                SaveStatus = $"No se pudo guardar: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void BackToHome() => _nav.Navigate(new DashboardViewModel(_nav));
}
