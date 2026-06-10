using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CleanWipe.App;

/// <summary>Lógica de la ventana principal: chrome personalizado (min/max/close, animación de entrada).</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        // E923 = Restaurar, E922 = Maximizar (Segoe MDL2 Assets).
        StateChanged += (_, _) => MaxButton.Content =
            WindowState == WindowState.Maximized ? char.ConvertFromUtf32(0xE923) : char.ConvertFromUtf32(0xE922);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Animación de entrada: fade (en la ventana) + scale (en el borde raíz; la Window
        // no admite RenderTransform de escala, sólo su contenido).
        var scale = new ScaleTransform(0.96, 0.96);
        RootBorder.RenderTransform = scale;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
