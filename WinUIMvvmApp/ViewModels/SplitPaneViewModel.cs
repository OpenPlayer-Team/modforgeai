using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUIMvvmApp.Models;
using WinUIMvvmApp.Services;

namespace WinUIMvvmApp.ViewModels;

/// <summary>
/// ViewModel per un singolo pannello (sinistro o destro) all'interno di una scheda splittata
/// Gestisce la navigazione indipendente per ogni pannello
/// </summary>
public partial class SplitPaneViewModel : ObservableObject, INotifyPropertyChanged
{
    #region Fields

    private readonly FileSystemService _fileSystemService;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    private readonly Stack<string> _backStack = new();
    private CancellationTokenSource? _currentEnumerationCts;
    private string _paneName;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Nome identificativo del pannello (es. "Sinistra", "Destra", "Alto", "Basso")
    /// </summary>
    [ObservableProperty]
    private string _paneTitle = "Pannello";

    /// <summary>
    /// Percorso corrente nel pannello
    /// </summary>
    [ObservableProperty]
    private string _currentPath = string.Empty;

    /// <summary>
    /// Elementi (file e cartelle) nel pannello corrente
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FileItem> _items = new();

    /// <summary>
    /// Indica se il pannello sta caricando dati
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Messaggio di stato per il pannello
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Pronto";

    /// <summary>
    /// Indica se è possibile tornare indietro in questo pannello
    /// </summary>
    [ObservableProperty]
    private bool _canGoBack;

    /// <summary>
    /// Elemento attualmente selezionato (per binding con ListView)
    /// </summary>
    [ObservableProperty]
    private FileItem? _selectedItem;

    #endregion

    #region Constructor

    /// <summary>
    /// Crea un nuovo pannello splittato con percorso iniziale
    /// </summary>
    /// <param name="initialPath">Percorso iniziale (default: C:\)</param>
    /// <param name="paneName">Nome identificativo del pannello</param>
    public SplitPaneViewModel(string? initialPath = null, string paneName = "Pannello")
    {
        _fileSystemService = new FileSystemService();
        _paneName = paneName;
        CurrentPath = initialPath ?? "C:\\";
        PaneTitle = $"{paneName}: {Path.GetFileName(CurrentPath.TrimEnd('\\')) ?? CurrentPath}";

        // Avvia caricamento iniziale asincrono
        _ = LoadCurrentDirectoryAsync();
    }

    /// <summary>
    /// Costruttore per design-time (Visual Studio designer)
    /// </summary>
    public SplitPaneViewModel() : this("C:\\", "Design")
    {
    }

    #endregion

    #region Relay Commands

    /// <summary>
    /// Naviga a una directory selezionata
    /// </summary>
    [RelayCommand]
    private async Task NavigateAsync(FileItem? item)
    {
        if (item == null || !item.IsDirectory)
            return;

        await NavigateToPathAsync(item.FullPath);
    }

    /// <summary>
    /// Torna alla directory precedente nello stack
    /// </summary>
    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (!CanGoBack || _backStack.Count == 0)
            return;

        var previousPath = _backStack.Pop();
        await NavigateToPathAsync(previousPath, isBackNavigation: true);
    }

    /// <summary>
    /// Naviga alla directory superiore (parent)
    /// </summary>
    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        try
        {
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null && parent.Exists)
            {
                await NavigateToPathAsync(parent.FullName);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore navigazione: {ex.Message}";
        }
    }

    /// <summary>
    /// Ricarica la directory corrente
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadCurrentDirectoryAsync();
    }

    /// <summary>
    /// Naviga a un percorso specifico
    /// </summary>
    [RelayCommand]
    private async Task NavigateToPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        await NavigateToPathAsync(path);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sincronizza questo pannello con un altro pannello (copia il percorso)
    /// Chiamato quando la sincronizzazione è attiva e l'altro pannello naviga
    /// </summary>
    public async Task SyncWithAsync(SplitPaneViewModel sourcePane)
    {
        if (sourcePane == null || string.IsNullOrEmpty(sourcePane.CurrentPath))
            return;

        // Evita loop di sincronizzazione infiniti
        if (CurrentPath == sourcePane.CurrentPath)
            return;

        await NavigateToPathAsync(sourcePane.CurrentPath, isSyncNavigation: true);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Naviga a un percorso specifico con gestione stack e sincronizzazione
    /// </summary>
    /// <param name="path">Percorso di destinazione</param>
    /// <param name="isBackNavigation">Indica se è navigazione indietro</param>
    /// <param name="isSyncNavigation">Indica se è navigazione da sincronizzazione</param>
    private async Task NavigateToPathAsync(string path, bool isBackNavigation = false, bool isSyncNavigation = false)
    {
        if (!Directory.Exists(path))
        {
            StatusMessage = $"Directory non trovata: {path}";
            return;
        }

        // Aggiungi il percorso corrente allo stack solo se:
        // - Non è navigazione indietro
        // - Non è navigazione da sincronizzazione (per evitare loop)
        // - Il percorso è diverso da quello corrente
        if (!isBackNavigation && !isSyncNavigation && !string.IsNullOrEmpty(CurrentPath) && CurrentPath != path)
        {
            _backStack.Push(CurrentPath);
        }

        CurrentPath = path;
        PaneTitle = $"{_paneName}: {Path.GetFileName(path.TrimEnd('\\')) ?? path}";
        CanGoBack = _backStack.Count > 0;

        await LoadCurrentDirectoryAsync();
    }

    /// <summary>
    /// Carica gli elementi della directory corrente nel pannello
    /// </summary>
    private async Task LoadCurrentDirectoryAsync()
    {
        // Evita caricamenti concorrenti nello stesso pannello
        if (!await _navigationLock.WaitAsync(0))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Caricamento {_paneName}: {CurrentPath}...";

            // Annulla l'enumerazione precedente se ancora in corso
            _currentEnumerationCts?.Cancel();
            _currentEnumerationCts = new CancellationTokenSource();
            var ct = _currentEnumerationCts.Token;

            // Svuota la collezione corrente
            Items.Clear();

            // Aggiungi elemento ".." per navigare alla directory padre
            // Solo se non siamo già nella root
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null && parent.Exists)
            {
                Items.Add(new FileItem
                {
                    Name = ".. (Cartella superiore)",
                    FullPath = parent.FullName,
                    IsDirectory = true,
                    Size = 0,
                    LastModified = DateTime.Now,
                    Icon = "⬆️"
                });
            }

            // Enumerazione asincrona con supporto cancellazione
            // Usa ConfigureAwait(true) per tornare sul thread UI dopo ogni elemento
            await foreach (var item in _fileSystemService.EnumerateAsync(CurrentPath, ct).ConfigureAwait(true))
            {
                Items.Add(item);
            }

            StatusMessage = Items.Count > 0
                ? $"{Items.Count} elementi in {_paneName}"
                : "Directory vuota";
        }
        catch (OperationCanceledException)
        {
            // Operazione annullata normalmente, ignorare
            StatusMessage = "Operazione annullata";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = $"Accesso negato a {_paneName}: {ex.Message}";
            Items.Clear();
        }
        catch (DirectoryNotFoundException ex)
        {
            StatusMessage = $"Directory non trovata in {_paneName}: {ex.Message}";
            Items.Clear();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore in {_paneName}: {ex.Message}";
            Items.Clear();
        }
        finally
        {
            IsLoading = false;
            _navigationLock.Release();
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Pulisce le risorse quando il pannello viene distrutto
    /// </summary>
    public void Cleanup()
    {
        _currentEnumerationCts?.Cancel();
        _currentEnumerationCts?.Dispose();
        _navigationLock.Dispose();
    }

    #endregion
}
