using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanWipe.App.Services;

/// <summary>Configuración de la app (en memoria; ampliable a persistencia JSON).</summary>
public partial class AppSettings : ObservableObject
{
    public static AppSettings Current { get; } = new();

    /// <summary>Enviar archivos/carpetas a la Papelera en lugar de borrado directo.</summary>
    [ObservableProperty]
    private bool _useRecycleBin = true;

    /// <summary>Ofrecer crear un punto de restauración antes de tocar el registro.</summary>
    [ObservableProperty]
    private bool _createRestorePoint;

    /// <summary>Ejecutar el desinstalador nativo antes de la limpieza.</summary>
    [ObservableProperty]
    private bool _runNativeUninstaller = true;
}
