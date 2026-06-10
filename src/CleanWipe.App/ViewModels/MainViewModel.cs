using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CleanWipe.App.Services;

namespace CleanWipe.App.ViewModels;

/// <summary>Shell de la aplicación: barra de navegación y hospedaje de la vista actual.</summary>
public partial class MainViewModel : ObservableObject
{
    public NavigationService Navigation { get; } = new();

    [ObservableProperty] private string _selectedPage = "Dashboard";

    public MainViewModel()
    {
        Navigation.Navigate(new DashboardViewModel(Navigation));
        Navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(NavigationService.CurrentViewModel)) return;
            // Sincroniza el resaltado del menú con la vista activa.
            SelectedPage = Navigation.CurrentViewModel switch
            {
                OrphanScanViewModel => "Orphans",
                SettingsViewModel => "Settings",
                _ => Navigation.CurrentViewModel is DashboardViewModel ? "Dashboard" : SelectedPage
            };
        };
    }

    [RelayCommand]
    private void GoDashboard()
    {
        SelectedPage = "Dashboard";
        Navigation.Navigate(new DashboardViewModel(Navigation));
    }

    [RelayCommand]
    private void GoOrphans()
    {
        SelectedPage = "Orphans";
        Navigation.Navigate(new OrphanScanViewModel());
    }

    [RelayCommand]
    private void GoSettings()
    {
        SelectedPage = "Settings";
        Navigation.Navigate(new SettingsViewModel());
    }
}
