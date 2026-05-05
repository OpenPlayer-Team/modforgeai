using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUIMvvmApp.Models;
using WinUIMvvmApp.Services;
using WinUIMvvmApp.Plugins;

namespace WinUIMvvmApp.ViewModels;

/// <summary>
/// ViewModel principale che gestisce schede, ricerca e plugin
/// </summary>
public partial class MainViewModel : ObservableObject, INotifyPropertyChanged
{
    #region Fields

    private readonly SearchService _searchService;
    private readonly PluginLoader _pluginLoader;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _searchActiveCts;
    private string _searchQuery = string.Empty;
    private bool _isSearching;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Collezione delle schede aperte
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TabViewModel> _tabs = new();

    /// <summary>
    /// Scheda attualmente selezionata
    /// </summary>
    [ObservableProperty]
    private TabViewModel? _selectedTab;

    /// <summary>
    /// Testo della query di ricerca
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Indica se una ricerca è in corso
    /// </summary>
    [ObservableProperty]
    private bool _isSearchActive;

    /// <summary>
    /// Progresso della ricerca (0-100)
    /// </summary>
    [ObservableProperty]
    private int _searchProgress;

    /// <summary>
    /// Risultati della ricerca corrente
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FileItem> _searchResults = new();

    /// <summary>
    /// Comandi dei plugin disponibili
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PluginCommand> _pluginCommands = new();

    /// <summary>
    /// Indica se i plugin sono stati caricati
    /// </summary>
    [ObservableProperty]
    private bool _arePluginsLoaded;

    /// <summary>
    /// Risultati del caricamento plugin
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PluginLoadResult> _pluginLoadResults = new();

    #endregion

    #region Constructor

    /// <summary>
    /// Crea il ViewModel principale
    /// </summary>
    public MainViewModel()
    {
        _searchService = new SearchService();
        _pluginLoader = new PluginLoader(CreateServiceProvider());
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Apre una scheda iniziale
        AddNewTab("C:\\");

        // Carica i plugin in background
        _ = LoadPluginsAsync();
    }

    /// <summary>
    /// Costruttore per design-mode
    /// </summary>
    public MainViewModel(bool designMode)
    {
        if (designMode)
        {
            var designTabs = new ObservableCollection<TabViewModel>
            {
                new TabViewModel("C:\\Windows") { Title = "Scheda: Windows" },
                new TabViewModel("C:\\Users") { Title = "Scheda: Users" }
            };
            Tabs = designTabs;
            SelectedTab = designTabs.FirstOrDefault();
        }

        _searchService = new SearchService();
        _pluginLoader = new PluginLoader(CreateServiceProvider());
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    #endregion

    #region Service Provider

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<FileSystemService>();
        services.AddSingleton<SearchService>();
        return services.BuildServiceProvider();
    }

    #endregion

    #region Plugin Management

    /// <summary>
    /// Carica tutti i plugin disponibili
    /// </summary>
    private async Task LoadPluginsAsync()
    {
        try
        {
            var results = await _pluginLoader.LoadAllPluginsAsync().ConfigureAwait(false);
            
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                PluginLoadResults.Clear();
                foreach (var result in results)
                {
                    PluginLoadResults.Add(result);
                }

                // Aggiorna comandi plugin
                UpdatePluginCommands();
                ArePluginsLoaded = true;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore caricamento plugin: {ex}");
        }
    }

    /// <summary>
    /// Aggiorna la lista comandi plugin
    /// </summary>
    private void UpdatePluginCommands()
    {
        PluginCommands.Clear();
        
        foreach (var command in _pluginLoader.GetAllCommands())
        {
            PluginCommands.Add(command);
        }
    }

    /// <summary>
    /// Esegue un comando plugin
    /// </summary>
    [RelayCommand]
    private async Task ExecutePluginCommandAsync(PluginCommand? command)
    {
        if (command == null || !command.IsEnabled)
            return;

        try
        {
            command.ExecuteAsync?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore esecuzione comando {command.Name}: {ex}");
        }
    }

    /// <summary>
    /// Ricarica tutti i plugin
    /// </summary>
    [RelayCommand]
    private async Task ReloadPluginsAsync()
    {
        // Scarica tutti i plugin
        foreach (var plugin in _pluginLoader.GetLoadedPlugins().ToList())
        {
            plugin.Cleanup();
        }

        // Ricarica
        await LoadPluginsAsync();
    }

    #endregion

    #region Existing Commands (Search, Tabs)

    // ... [Codice esistente per ricerca e gestione schede] ...
    // Mantenuto invariato per compatibilità

    [RelayCommand]
    private void AddNewTab(string? initialPath = null)
    {
        var newTab = new TabViewModel(initialPath ?? "C:\\");
        Tabs.Add(newTab);
        SelectedTab = newTab;
    }

    [RelayCommand]
    private void CloseTab(TabViewModel? tab)
    {
        if (tab == null || !Tabs.Contains(tab))
            return;

        tab.Cleanup();

        var tabIndex = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (Tabs.Count > 0)
        {
            SelectedTab = tabIndex > 0
                ? Tabs[Math.Min(tabIndex - 1, Tabs.Count - 1)]
                : Tabs.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void CloseOtherTabs(TabViewModel? keepTab)
    {
        if (keepTab == null || !Tabs.Contains(keepTab))
            return;

        var tabsToRemove = Tabs.Where(t => t != keepTab).ToList();
        foreach (var tab in tabsToRemove)
        {
            tab.Cleanup();
            Tabs.Remove(tab);
        }

        SelectedTab = keepTab;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var debounceToken = _searchDebounceCts.Token;

        try
        {
            await Task.Delay(500, debounceToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Length < 2)
            {
                await ClearSearchResultsAsync().ConfigureAwait(false);
                return;
            }

            await StartSearchInternalAsync(SearchText, debounceToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Debounce annullato
        }
    }

    [RelayCommand]
    private void CancelSearch()
    {
        _searchActiveCts?.Cancel();
        IsSearchActive = false;
    }

    [RelayCommand]
    private async Task ClearSearchAsync()
    {
        await ClearSearchResultsAsync().ConfigureAwait(false);
        SearchText = string.Empty;
    }

    #endregion

    #region Search Implementation

    private async Task StartSearchInternalAsync(string query, CancellationToken debounceToken)
    {
        _searchActiveCts?.Cancel();
        _searchActiveCts = new CancellationTokenSource();
        var searchToken = _searchActiveCts.Token;

        var linkedToken = CancellationTokenSource
            .CreateLinkedTokenSource(debounceToken, searchToken)
            .Token;

        try
        {
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = true;
                SearchProgress = 0;
                SearchResults.Clear();
            }).ConfigureAwait(false);

            var rootPath = SelectedTab?.CurrentPath ?? "C:\\";

            var progress = new Progress<int>(percent =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    SearchProgress = percent;
                });
            });

            await foreach (var fileItem in _searchService.SearchAsync(
                rootPath,
                query,
                linkedToken,
                progress,
                timeoutMs: 60000).ConfigureAwait(false))
            {
                await _dispatcherQueue.EnqueueAsync(() =>
                {
                    SearchResults.Add(fileItem);
                }).ConfigureAwait(false);
            }

            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = false;
                SearchProgress = 100;
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = false;
            }).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = false;
                SearchProgress = 0;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = false;
                SearchProgress = 0;
            }).ConfigureAwait(false);

            System.Diagnostics.Debug.WriteLine($"Errore ricerca: {ex.Message}");
        }
        finally
        {
            if (_searchActiveCts?.IsCancellationRequested == false)
            {
                _searchActiveCts.Cancel();
            }
        }
    }

    private async Task ClearSearchResultsAsync()
    {
        await _dispatcherQueue.EnqueueAsync(() =>
        {
            SearchResults.Clear();
            IsSearchActive = false;
            SearchProgress = 0;
        }).ConfigureAwait(false);
    }

    #endregion
}


    /// <summary>
    /// Costruttore per design-mode
    /// </summary>
    public MainViewModel(bool designMode)
    {
        if (designMode)
        {
            var designTabs = new ObservableCollection<TabViewModel>
            {
                new TabViewModel("C:\\Windows") { Title = "Scheda: Windows" },
                new TabViewModel("C:\\Users") { Title = "Scheda: Users" }
            };
            Tabs = designTabs;
            SelectedTab = designTabs.FirstOrDefault();
        }

        _searchService = new SearchService();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    #endregion

    #region Relay Commands

    /// <summary>
    /// Comando per aggiungere una nuova scheda
    /// </summary>
    [RelayCommand]
    private void AddNewTab(string? initialPath = null)
    {
        var newTab = new TabViewModel(initialPath ?? "C:\\");
        Tabs.Add(newTab);
        SelectedTab = newTab;
    }

    /// <summary>
    /// Comando per chiudere una scheda specifica
    /// </summary>
    [RelayCommand]
    private void CloseTab(TabViewModel? tab)
    {
        if (tab == null || !Tabs.Contains(tab))
            return;

        tab.Cleanup();

        var tabIndex = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (Tabs.Count > 0)
        {
            SelectedTab = tabIndex > 0
                ? Tabs[Math.Min(tabIndex - 1, Tabs.Count - 1)]
                : Tabs.FirstOrDefault();
        }
    }

    /// <summary>
    /// Comando per chiudere tutte le schede tranne quella specificata
    /// </summary>
    [RelayCommand]
    private void CloseOtherTabs(TabViewModel? keepTab)
    {
        if (keepTab == null || !Tabs.Contains(keepTab))
            return;

        var tabsToRemove = Tabs.Where(t => t != keepTab).ToList();
        foreach (var tab in tabsToRemove)
        {
            tab.Cleanup();
            Tabs.Remove(tab);
        }

        SelectedTab = keepTab;
    }

    /// <summary>
    /// Comando per avviare la ricerca con debounce
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        // Annulla il debounce precedente
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var debounceToken = _searchDebounceCts.Token;

        try
        {
            // Aspetta 500ms (debounce) prima di avviare la ricerca
            // PERCHÉ IL DEBOUNCE:
            // Evita di lanciare una ricerca per ogni tasto premuto.
            // Se l'utente digita "test" rapidamente:
            // - Senza debounce: partono 4 ricerche (t, te, tes, test)
            // - Con debounce: parte 1 ricerca (solo dopo 500ms di pausa)
            // Questo salva CPU, I/O disco e previene race condition.
            await Task.Delay(500, debounceToken).ConfigureAwait(false);

            // Se il testo è vuoto o troppo corto, pulisci i risultati
            if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Length < 2)
            {
                await ClearSearchResultsAsync().ConfigureAwait(false);
                return;
            }

            // Avvia la ricerca effettiva
            await StartSearchInternalAsync(SearchText, debounceToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Debounce annullato (utente ha digitato un altro tasto)
        }
    }

    /// <summary>
    /// Comando per annullare la ricerca corrente
    /// </summary>
    [RelayCommand]
    private void CancelSearch()
    {
        _searchActiveCts?.Cancel();
        IsSearchActive = false;
    }

    /// <summary>
    /// Comando per pulire i risultati della ricerca
    /// </summary>
    [RelayCommand]
    private async Task ClearSearchAsync()
    {
        await ClearSearchResultsAsync().ConfigureAwait(false);
        SearchText = string.Empty;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Avvia la ricerca interna con gestione UI
    /// </summary>
    private async Task StartSearchInternalAsync(string query, CancellationToken debounceToken)
    {
        // Annulla eventuale ricerca precedente
        _searchActiveCts?.Cancel();
        _searchActiveCts = new CancellationTokenSource();
        var searchToken = _searchActiveCts.Token;

        // Combina i token (debounce + search)
        var linkedToken = CancellationTokenSource
            .CreateLinkedTokenSource(debounceToken, searchToken)
            .Token;

        try
        {
            // Prepara l'UI (usa DispatcherQueue per thread safety)
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = true;
                SearchProgress = 0;
                SearchResults.Clear();
            }).ConfigureAwait(false);

            // Determina la directory radice dalla scheda selezionata
            var rootPath = SelectedTab?.CurrentPath ?? "C:\\";

            // Crea progress reporter per aggiornare la UI
            var progress = new Progress<int>(percent =>
            {
                // Usa DispatcherQueue per aggiornamenti UI thread-safe
                _dispatcherQueue.TryEnqueue(() =>
                {
                    SearchProgress = percent;
                });
            });

            // Esegui la ricerca asincrona
            // SearchAsync restituisce IAsyncEnumerable (streaming dei risultati)
            await foreach (var fileItem in _searchService.SearchAsync(
                rootPath,
                query,
                linkedToken,
                progress,
                timeoutMs: 60000).ConfigureAwait(false))
            {
                // Aggiungi ogni risultato alla collection in modo thread-safe
                await _dispatcherQueue.EnqueueAsync(() =>
                {
                    SearchResults.Add(fileItem);
                }).ConfigureAwait(false);
            }

            // Ricerca completata con successo
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = false;
                SearchProgress = 100;
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ricerca annullata
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = false;
            }).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Timeout della ricerca
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = false;
                SearchProgress = 0;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Errore generico
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                IsSearchActive = false;
                SearchProgress = 0;
            }).ConfigureAwait(false);

            Debug.WriteLine($"Errore ricerca: {ex.Message}");
        }
        finally
        {
            // Cleanup
            if (_searchActiveCts?.IsCancellationRequested == false)
            {
                _searchActiveCts.Cancel();
            }
        }
    }

    /// <summary>
    /// Pulisce i risultati della ricerca in modo thread-safe
    /// </summary>
    private async Task ClearSearchResultsAsync()
    {
        await _dispatcherQueue.EnqueueAsync(() =>
        {
            SearchResults.Clear();
            IsSearchActive = false;
            SearchProgress = 0;
        }).ConfigureAwait(false);
    }

    #endregion
}


    /// <summary>
    /// Costruttore per design-time
    /// </summary>
    public MainViewModel(bool designMode) 
    {
        if (designMode)
        {
            // Dati di esempio per il designer
            var designTabs = new ObservableCollection<TabViewModel>
            {
                new TabViewModel("C:\\Windows") { Title = "Scheda: Windows" },
                new TabViewModel("C:\\Users") { Title = "Scheda: Users" }
            };
            Tabs = designTabs;
            SelectedTab = designTabs.FirstOrDefault();
        }
    }

    #endregion

    #region Relay Commands

    /// <summary>
    /// Comando per aggiungere una nuova scheda
    /// </summary>
    [RelayCommand]
    private void AddNewTab(string? initialPath = null)
    {
        var newTab = new TabViewModel(initialPath ?? "C:\\");
        Tabs.Add(newTab);
        SelectedTab = newTab;
    }

    /// <summary>
    /// Comando per chiudere una scheda specifica
    /// </summary>
    [RelayCommand]
    private void CloseTab(TabViewModel? tab)
    {
        if (tab == null || !Tabs.Contains(tab))
            return;

        // Pulizia risorse della scheda
        tab.Cleanup();

        var tabIndex = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        // Seleziona la scheda precedente o successiva
        if (Tabs.Count > 0)
        {
            SelectedTab = tabIndex > 0 
                ? Tabs[Math.Min(tabIndex - 1, Tabs.Count - 1)] 
                : Tabs.FirstOrDefault();
        }
    }

    /// <summary>
    /// Comando per chiudere tutte le schede tranne quella specificata
    /// </summary>
    [RelayCommand]
    private void CloseOtherTabs(TabViewModel? keepTab)
    {
        if (keepTab == null || !Tabs.Contains(keepTab))
            return;

        var tabsToRemove = Tabs.Where(t => t != keepTab).ToList();
        foreach (var tab in tabsToRemove)
        {
            tab.Cleanup();
            Tabs.Remove(tab);
        }

        SelectedTab = keepTab;
    }

    #endregion
}
