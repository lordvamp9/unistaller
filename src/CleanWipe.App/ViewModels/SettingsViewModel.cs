using CommunityToolkit.Mvvm.ComponentModel;
using CleanWipe.App.Services;

namespace CleanWipe.App.ViewModels;

/// <summary>Ajustes: expone la configuración global de la app a la vista.</summary>
public partial class SettingsViewModel : ObservableObject
{
    public AppSettings Settings => AppSettings.Current;

    public string Version => "1.1.0";
    public string Author => "vamp9 · Andrés Loyola";
}
