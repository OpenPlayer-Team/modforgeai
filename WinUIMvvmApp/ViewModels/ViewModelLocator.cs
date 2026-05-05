using System;
using Microsoft.Extensions.DependencyInjection;
using WinUIMvvmApp.ViewModels;

namespace WinUIMvvmApp.ViewModels;

/// <summary>
/// Locator per i ViewModel - gestisce la risoluzione delle dipendenze tramite DI
/// </summary>
public class ViewModelLocator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Istanza statica per accesso globale (pattern Singleton)
    /// </summary>
    public static ViewModelLocator Instance { get; } = new();

    /// <summary>
    /// ViewModel principale
    /// </summary>
    public MainViewModel MainViewModel => _serviceProvider.GetRequiredService<MainViewModel>();

    /// <summary>
    /// Costruttore privato per pattern Singleton
    /// </summary>
    private ViewModelLocator()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Configura il contenitore DI
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // Registrazione servizi
        services.AddSingleton<FileSystemService>();

        // Registrazione ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<TabViewModel>();
    }

    /// <summary>
    /// Crea un nuovo TabViewModel tramite DI
    /// </summary>
    public TabViewModel CreateTabViewModel(string? initialPath = null) =>
        ActivatorUtilities.CreateInstance<TabViewModel>(_serviceProvider, initialPath);
}
