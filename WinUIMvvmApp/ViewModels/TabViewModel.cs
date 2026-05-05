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
/// ViewModel per la gestione di una singola scheda con navigazione file system
/// Supporta modalità split (due pannelli affiancati) e navigazione sincronizzata
/// </summary>
public partial class TabViewModel : ObservableObject, INotifyPropertyChanged
{
    #region Fields

    private readonly FileSystemService _fileSystemService;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    private readonly Stack<string> _backStack = new();
    private CancellationTokenSource? _currentEnumerationCts;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Titolo della scheda (legato alla directory corrente)
    /// </summary>
    [ObservableProperty]
    private string _title = "Nuova Scheda";

    /// <summary>
    /// Percorso corrente visualizzato nella scheda
    /// </summary>
    [ObservableProperty]
    private string _currentPath = string.Empty;

    /// <summary>
    /// Collezione degli elementi (file e cartelle) nella directory corrente
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FileItem> _items = new();

    /// <summary>
    /// Indica se la scheda sta caricando dati
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Messaggio di stato/errore
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Pronto";

    /// <summary>
    /// Indica se è possibile tornare indietro
    /// </summary>
    [ObservableProperty]
    private bool _canGoBack;

    /// <summary>
    /// Indica se la modalità split è attiva (due pannelli)
    /// </summary>
    [ObservableProperty]
    private bool _isSplitMode;

    /// <summary>
    /// Indica se la navigazione deve essere sincronizzata tra i pannelli
    /// </summary>
    [ObservableProperty]
    private bool _isSyncEnabled;

    /// <summary>
    /// Orientamento dello split: true = verticale (colonne), false = orizzontale (righe)
    /// </summary>
    [ObservableProperty]
    private bool _isVerticalSplit = true;

    /// <summary>
    /// Pannello sinistro (o superiore) nello split
    /// </summary>
    [ObservableProperty]
    private SplitPaneViewModel? _leftPane;

    /// <summary>
    /// Pannello destro (o inferiore) nello split
    /// </summary>
    [ObservableProperty]
    private SplitPaneViewModel? _rightPane;

    #endregion

    #region Constructor

    /// <summary>
    /// Crea una nuova scheda con percorso iniziale opzionale
    /// </summary>
    /// <param name="initialPath">Percorso iniziale (default: C:\)</param>
    public TabViewModel(string? initialPath = null)
    {
        _fileSystemService = new FileSystemService();
        CurrentPath = initialPath ?? "C:\\";
        Title = $"Scheda: {CurrentPath}";

        // Avvia il caricamento iniziale in background
        _ = LoadCurrentDirectoryAsync();
    }

    /// <summary>
    /// Costruttore per design-time (Visual Studio/XAML designer)
    /// </summary>
    public TabViewModel() : this("C:\\")
    {
    }

    #endregion

    #region Relay Commands

    /// <summary>
    /// Comando per navigare in una directory
    /// </summary>
    [RelayCommand]
    private async Task NavigateAsync(FileItem? item)
    {
        if (item == null || !item.IsDirectory)
            return;

        await NavigateToPathAsync(item.FullPath);
    }

    /// <summary>
    /// Comando per tornare alla directory precedente
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
    /// Comando per navigare alla directory padre
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
    /// Comando per ricaricare la directory corrente
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsSplitMode)
        {
            // Ricarica entrambi i pannelli se in modalità split
            if (LeftPane != null)
                await LeftPane.RefreshAsync();
            if (RightPane != null)
                await RightPane.RefreshAsync();
        }
        else
        {
            await LoadCurrentDirectoryAsync();
        }
    }

    /// <summary>
    /// Comando per navigare a un percorso specifico
    /// </summary>
    [RelayCommand]
    private async Task NavigateToPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        await NavigateToPathAsync(path);
    }

    /// <summary>
    /// Attiva o disattiva la modalità split
    /// </summary>
    [RelayCommand]
    private void ToggleSplitModeAsync()
    {
        IsSplitMode = !IsSplitMode;

        if (IsSplitMode)
        {
            // Crea i due pannelli con lo stesso percorso iniziale
            LeftPane = new SplitPaneViewModel(CurrentPath, "Sinistra");
            RightPane = new SplitPaneViewModel(CurrentPath, "Destra");
            Title = $"SPLIT: {Path.GetFileName(CurrentPath) ?? CurrentPath}";
        }
        else
        {
            // Pulisce i pannelli
            LeftPane?.Cleanup();
            RightPane?.Cleanup();
            LeftPane = null;
            RightPane = null;
            Title = $"Scheda: {Path.GetFileName(CurrentPath) ?? CurrentPath}";
        }
    }

    /// <summary>
    /// Alterna l'orientamento dello split (verticale/orizzontale)
    /// </summary>
    [RelayCommand]
    private void ToggleSplitOrientationAsync()
    {
        IsVerticalSplit = !IsVerticalSplit;
    }

    /// <summary>
    /// Sincronizza i percorsi dei due pannelli
    /// </summary>
    [RelayCommand]
    private async Task SyncPanesAsync()
    {
        if (!IsSplitMode || LeftPane == null || RightPane == null)
            return;

        // Sincronizza il pannello destro con quello sinistro
        await RightPane.SyncWithAsync(LeftPane);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Naviga a un percorso specifico con gestione dello stack di navigazione
    /// </summary>
    private async Task NavigateToPathAsync(string path, bool isBackNavigation = false)
    {
        if (!Directory.Exists(path))
        {
            StatusMessage = $"Directory non trovata: {path}";
            return;
        }

        // Aggiungi il percorso corrente allo stack solo se non è navigazione indietro
        if (!isBackNavigation && !string.IsNullOrEmpty(CurrentPath) && CurrentPath != path)
        {
            _backStack.Push(CurrentPath);
        }

        CurrentPath = path;
        Title = $"Scheda: {Path.GetFileName(path) ?? path}";
        CanGoBack = _backStack.Count > 0;

        await LoadCurrentDirectoryAsync();
    }

    /// <summary>
    /// Carica gli elementi della directory corrente
    /// </summary>
    private async Task LoadCurrentDirectoryAsync()
    {
        // Evita caricamenti concorrenti
        if (!await _navigationLock.WaitAsync(0))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Caricamento: {CurrentPath}...";

            // Annulla l'enumerazione precedente
            _currentEnumerationCts?.Cancel();
            _currentEnumerationCts = new CancellationTokenSource();
            var ct = _currentEnumerationCts.Token;

            // Svuota la collezione corrente
            Items.Clear();

            // Aggiungi elemento ".." per navigare alla directory padre
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null && parent.Exists)
            {
                Items.Add(new FileItem
                {
                    Name = "..",
                    FullPath = parent.FullName,
                    IsDirectory = true,
                    Size = 0,
                    LastModified = DateTime.Now,
                    Icon = "⬆️"
                });
            }

            // Enumerazione asincrona con cancellazione
            await foreach (var item in _fileSystemService.EnumerateAsync(CurrentPath, ct).ConfigureAwait(true))
            {
                Items.Add(item);
            }

            StatusMessage = Items.Count > 0
                ? $"{Items.Count} elementi trovati"
                : "Directory vuota";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Operazione annullata";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = $"Accesso negato: {ex.Message}";
            Items.Clear();
        }
        catch (DirectoryNotFoundException ex)
        {
            StatusMessage = $"Directory non trovata: {ex.Message}";
            Items.Clear();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore: {ex.Message}";
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
    /// Pulisce le risorse quando la scheda viene chiusa
    /// </summary>
    public void Cleanup()
    {
        _currentEnumerationCts?.Cancel();
        _currentEnumerationCts?.Dispose();
        _navigationLock.Dispose();
        LeftPane?.Cleanup();
        RightPane?.Cleanup();
    }

    #endregion
}


    /// <summary>
    /// Costruttore per design-time (Visual Studio/XAML designer)
    /// </summary>
    public TabViewModel() : this("C:\\")
    {
    }

    #endregion

    #region Relay Commands

    /// <summary>
    /// Comando per navigare in una directory
    /// </summary>
    [RelayCommand]
    private async Task NavigateAsync(FileItem? item)
    {
        if (item == null || !item.IsDirectory)
            return;

        await NavigateToPathAsync(item.FullPath);
    }

    /// <summary>
    /// Comando per tornare alla directory precedente
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
    /// Comando per navigare alla directory padre
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
    /// Comando per ricaricare la directory corrente
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadCurrentDirectoryAsync();
    }

    /// <summary>
    /// Comando per navigare a un percorso specifico
    /// </summary>
    [RelayCommand]
    private async Task NavigateToPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        await NavigateToPathAsync(path);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Naviga a un percorso specifico con gestione dello stack di navigazione
    /// </summary>
    private async Task NavigateToPathAsync(string path, bool isBackNavigation = false)
    {
        if (!Directory.Exists(path))
        {
            StatusMessage = $"Directory non trovata: {path}";
            return;
        }

        // Aggiungi il percorso corrente allo stack solo se non è navigazione indietro
        if (!isBackNavigation && !string.IsNullOrEmpty(CurrentPath) && CurrentPath != path)
        {
            _backStack.Push(CurrentPath);
        }

        CurrentPath = path;
        Title = $"Scheda: {Path.GetFileName(path) ?? path}";
        CanGoBack = _backStack.Count > 0;

        await LoadCurrentDirectoryAsync();
    }

    /// <summary>
    /// Carica gli elementi della directory corrente
    /// </summary>
    private async Task LoadCurrentDirectoryAsync()
    {
        // Evita caricamenti concorrenti
        if (!await _navigationLock.WaitAsync(0))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Caricamento: {CurrentPath}...";

            // Annulla l'enumerazione precedente
            _currentEnumerationCts?.Cancel();
            _currentEnumerationCts = new CancellationTokenSource();
            var ct = _currentEnumerationCts.Token;

            // Svuota la collezione corrente
            Items.Clear();

            // Aggiungi elemento ".." per navigare alla directory padre (se non siamo già nella root)
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null && parent.Exists)
            {
                Items.Add(new FileItem
                {
                    Name = "..",
                    FullPath = parent.FullName,
                    IsDirectory = true,
                    Size = 0,
                    LastModified = DateTime.Now,
                    Icon = "⬆️"
                });
            }

            // Enumerazione asincrona con cancellazione
            await foreach (var item in _fileSystemService.EnumerateAsync(CurrentPath, ct).ConfigureAwait(true))
            {
                Items.Add(item);
            }

            StatusMessage = Items.Count > 0 
                ? $"{Items.Count} elementi trovati" 
                : "Directory vuota";
        }
        catch (OperationCanceledException)
        {
            // Operazione annullata, ignorare
            StatusMessage = "Operazione annullata";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = $"Accesso negato: {ex.Message}";
            Items.Clear();
        }
        catch (DirectoryNotFoundException ex)
        {
            StatusMessage = $"Directory non trovata: {ex.Message}";
            Items.Clear();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore: {ex.Message}";
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
    /// Pulisce le risorse quando la scheda viene chiusa
    /// </summary>
    public void Cleanup()
    {
        _currentEnumerationCts?.Cancel();
        _currentEnumerationCts?.Dispose();
        _navigationLock.Dispose();
    }

    #endregion
}
