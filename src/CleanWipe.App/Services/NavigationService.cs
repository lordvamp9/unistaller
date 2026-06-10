using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanWipe.App.Services;

/// <summary>
/// Servicio de navegación simple. El shell enlaza a <see cref="CurrentViewModel"/> y
/// los ViewModels llaman a <see cref="Navigate"/> pasando la siguiente instancia
/// (inyectando así los datos necesarios entre vistas).
/// </summary>
public partial class NavigationService : ObservableObject
{
    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    public void Navigate(ObservableObject viewModel) => CurrentViewModel = viewModel;
}
