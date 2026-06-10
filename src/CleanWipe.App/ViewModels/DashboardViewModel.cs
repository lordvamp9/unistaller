using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CleanWipe.App.Services;
using CleanWipe.Core.Models;
using CleanWipe.Core.Services;

namespace CleanWipe.App.ViewModels;

/// <summary>Vista 1 — Dashboard: lista de programas instalados con búsqueda y filtros.</summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly NavigationService _nav;
    private readonly ProgramDetector _detector = new();

    public ObservableCollection<InstalledProgram> Programs { get; } = new();
    public ICollectionView ProgramsView { get; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _activeFilter = "Todos";
    [ObservableProperty] private int _resultCount;

    public DashboardViewModel(NavigationService nav)
    {
        _nav = nav;
        ProgramsView = CollectionViewSource.GetDefaultView(Programs);
        ProgramsView.Filter = FilterProgram;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => RefreshView();
    partial void OnActiveFilterChanged(string value) => RefreshView();

    private void RefreshView()
    {
        using (ProgramsView.DeferRefresh())
        {
            ProgramsView.SortDescriptions.Clear();
            ProgramsView.SortDescriptions.Add(ActiveFilter == "Por tamaño"
                ? new SortDescription(nameof(InstalledProgram.EstimatedSizeMB), ListSortDirection.Descending)
                : new SortDescription(nameof(InstalledProgram.Name), ListSortDirection.Ascending));
        }
        ResultCount = ProgramsView.Cast<object>().Count();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Programs.Clear();
            var list = await _detector.DetectAsync();
            foreach (var p in list) Programs.Add(p);
            RefreshView();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SetFilter(string filter) => ActiveFilter = filter;

    [RelayCommand]
    private void OpenProgram(InstalledProgram? program)
    {
        if (program == null) return;
        _nav.Navigate(new PreScanViewModel(_nav, program));
    }

    private bool FilterProgram(object obj)
    {
        if (obj is not InstalledProgram p) return false;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string s = SearchText.Trim();
            bool match = p.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                         || p.Publisher.Contains(s, StringComparison.OrdinalIgnoreCase);
            if (!match) return false;
        }

        return ActiveFilter switch
        {
            "Con residuos" => p.HasResidualFiles,
            "Recientes" => p.InstallDate >= DateTime.Now.AddDays(-30),
            "Por tamaño" => p.EstimatedSizeMB > 0,
            _ => true
        };
    }
}
