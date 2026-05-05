using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using WinUIMvvmApp.ViewModels;
using WinUIMvvmApp.Views;

namespace WinUIMvvmApp;

/// <summary>
/// Classe applicazione - configura DI e avvia la finestra principale
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Istanza del locator per accesso globale ai ViewModel
    /// </summary>
    public ViewModelLocator ViewModelLocator { get; }

    /// <summary>
    /// Costruttore - inizializza servizi e DI
    /// </summary>
    public App()
    {
        InitializeComponent();
        
        // Inizializza il locator (configura DI)
        ViewModelLocator = new ViewModelLocator();
        
        // Configura i converter globali
        Resources.Add("BytesToStringConverter", new BytesToStringConverter());
        Resources.Add("DateTimeToStringConverter", new DateTimeToStringConverter());
        Resources.Add("IntToVisibilityConverter", new IntToVisibilityConverter());
        Resources.Add("InverseBoolConverter", new InverseBoolConverter());
        Resources.Add("BoolToFontWeightConverter", new BoolToFontWeightConverter());

        System.Diagnostics.Debug.WriteLine("App inizializzata - DI configurato");
    }

    /// <summary>
    /// Avvio dell'applicazione
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"App lanciata - Args: {args.Arguments}");

        // Crea e mostra la finestra principale
        var mainWindow = new MainWindow();
        
        // Attiva la finestra
        mainWindow.Activate();

        System.Diagnostics.Debug.WriteLine("MainWindow attivata");
    }
}
