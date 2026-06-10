using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CleanWipe.App.Services;
using CleanWipe.Core.Models;
using CleanWipe.Core.Services;

namespace CleanWipe.App.ViewModels;

/// <summary>Vista 3 — Progreso: ejecuta el motor de desinstalación y reporta avance en vivo.</summary>
public partial class ProgressViewModel : ObservableObject
{
    private readonly NavigationService _nav;
    private readonly ScanResult _scan;
    private readonly UninstallOptions _options;
    private readonly UninstallEngine _engine = new();
    private readonly CancellationTokenSource _cts = new();

    public string ProgramName => _scan.Program.Name;

    [ObservableProperty] private int _percent;
    [ObservableProperty] private string _statusText = "Preparando...";
    [ObservableProperty] private bool _isRunning = true;

    public ObservableCollection<string> CompletedSteps { get; } = new();

    public ProgressViewModel(NavigationService nav, ScanResult scan, UninstallOptions options)
    {
        _nav = nav;
        _scan = scan;
        _options = options;
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        var progress = new Progress<UninstallProgress>(OnProgress);
        try
        {
            var report = await _engine.ExecuteAsync(_scan, _options, progress, _cts.Token);
            IsRunning = false;
            _nav.Navigate(new ResultViewModel(_nav, report));
        }
        catch (OperationCanceledException)
        {
            IsRunning = false;
            StatusText = "Operación cancelada.";
            _nav.Navigate(new DashboardViewModel(_nav));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Fallo en la desinstalación", ex);
            IsRunning = false;
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void OnProgress(UninstallProgress p)
    {
        Percent = p.Percent;
        StatusText = p.Status;
        if (p.CompletedItem != null)
        {
            string mark = string.IsNullOrEmpty(p.CompletedItem.SkipReason) ? "✓" : "⚠";
            CompletedSteps.Insert(0, $"{mark}  {p.CompletedItem.Path}");
            if (CompletedSteps.Count > 200) CompletedSteps.RemoveAt(CompletedSteps.Count - 1);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsRunning) _cts.Cancel();
    }
}
