using System.Windows;
using System.Windows.Threading;
using CleanWipe.Core.Services;

namespace CleanWipe.App;

/// <summary>Punto de entrada de la aplicación CleanWipe.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLogger.Info("CleanWipe iniciado.");

        // Captura excepciones no controladas en el hilo de UI para no perder datos ni cerrar abruptamente.
        DispatcherUnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Excepción no controlada en la UI", e.Exception);
        MessageBox.Show($"Ha ocurrido un error inesperado:\n\n{e.Exception.Message}",
            "CleanWipe", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Info("CleanWipe cerrado.");
        AppLogger.CloseAndFlush();
        base.OnExit(e);
    }
}
