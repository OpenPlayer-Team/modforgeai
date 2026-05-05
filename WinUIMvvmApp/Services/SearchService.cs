using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace WinUIMvvmApp.Services;

/// <summary>
/// Servizio per la ricerca ricorsiva asincrona di file nel file system
/// Implementa pattern di cancellazione, timeout e progress reporting
/// </summary>
public class SearchService
{
    #region Fields

    private readonly FileSystemService _fileSystemService;
    private CancellationTokenSource? _searchCts;
    private readonly SemaphoreSlim _searchLock = new(1, 1);

    #endregion

    #region Constructor

    /// <summary>
    /// Crea un'istanza del servizio di ricerca
    /// </summary>
    public SearchService()
    {
        _fileSystemService = new FileSystemService();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Ricerca ricorsiva asincrona di file che corrispondono alla query
    /// </summary>
    /// <param name="rootPath">Percorso radice da cui iniziare la ricerca</param>
    /// <param name="query">Stringa di ricerca (cerca nei nomi dei file)</param>
    /// <param name="ct">Token di cancellazione per interrompere la ricerca</param>
    /// <param name="progress">Interfaccia per reportare il progresso (percentuale directory esplorate)</param>
    /// <param name="timeoutMs">Timeout in millisecondi (default: 30 secondi)</param>
    /// <returns>Enumerazione di FileItem che corrispondono alla query</returns>
    /// <remarks>
    /// FLUSSO ASYNC:
    /// 1. Validazione input (percorso esistente, query non vuota)
    /// 2. Creazione CTS per timeout se non fornito
    /// 3. Enumerazione ricorsiva delle directory con try-catch
    /// 4. Per ogni directory, enumerazione file con filtro case-insensitive
    /// 5. Report progresso ogni N directory (per non saturare la UI)
    /// 6. Restituzione risultati via IAsyncEnumerable (streaming)
    /// 
    /// PERCHÉ IL DEBOUNCE:
    /// Senza debounce, ogni tasto digitato scatena una nuova ricerca.
    /// Questo causerebbe:
    /// - Molteplici ricerche concorrenti (waste di CPU/IO)
    /// - Risultati "ballerini" (la ricerca 3 termina dopo la 4, sovrascrivendo)
    /// - Sovraccarico del thread UI con aggiornamenti continui
    /// - Timeout frequenti per ricerche interrotte
    /// Con debounce 500ms aspettiamo che l'utente smetta di digitare,
    /// poi lanciamo UNA sola ricerca per la query finale.
    /// </remarks>
    public async IAsyncEnumerable<FileItem> SearchAsync(
        string rootPath,
        string query,
        [EnumeratorCancellation] CancellationToken ct,
        IProgress<int>? progress = null,
        int timeoutMs = 30000)
    {
        // Validazione input
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            yield break;
        }

        // Normalizza la query per ricerca case-insensitive
        var normalizedQuery = query.Trim().ToLowerInvariant();

        // Setup timeout se non fornito un CTS esterno
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        // Contatori per il progresso
        var totalDirectories = 0;
        var processedDirectories = 0;
        var resultsFound = 0;
        var lastProgressReport = 0;

        try
        {
            // Conta le directory totali per il progresso (operazione veloce)
            // Usiamo un limite per non impiegare troppo tempo solo per il conteggio
            try
            {
                totalDirectories = await Task.Run(() =>
                {
                    try
                    {
                        return Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)
                            .Take(1000) // Limitiamo il conteggio per performance
                            .Count();
                    }
                    catch
                    {
                        return 100; // Valore di default se non possiamo contare
                    }
                }, timeoutCts.Token).ConfigureAwait(false);

                totalDirectories = Math.Max(totalDirectories, 1); // Evita divisione per zero
            }
            catch
            {
                totalDirectories = 100; // Default se fallisce il conteggio
            }

            // Enumerazione ricorsiva delle directory
            await foreach (var directory in EnumerateDirectoriesSafeAsync(rootPath, timeoutCts.Token).ConfigureAwait(false))
            {
                timeoutCts.Token.ThrowIfCancellationRequested();

                try
                {
                    // Enumerazione file nella directory corrente
                    var files = await Task.Run(() =>
                    {
                        try
                        {
                            return Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                                .Where(f => Path.GetFileName(f).ToLowerInvariant().Contains(normalizedQuery))
                                .ToList();
                        }
                        catch (UnauthorizedAccessException)
                        {
                            return new List<string>();
                        }
                        catch (DirectoryNotFoundException)
                        {
                            return new List<string>();
                        }
                    }, timeoutCts.Token).ConfigureAwait(false);

                    // Converti i file trovati in FileItem e restituiscili
                    foreach (var file in files)
                    {
                        timeoutCts.Token.ThrowIfCancellationRequested();

                        try
                        {
                            var fileInfo = new FileInfo(file);
                            yield return new FileItem
                            {
                                Name = fileInfo.Name,
                                FullPath = fileInfo.FullName,
                                IsDirectory = false,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime
                            };

                            resultsFound++;
                        }
                        catch (Exception ex) when (ex is FileNotFoundException || ex is IOException)
                        {
                            // File diventato inaccessibile durante la ricerca, ignorare
                            Debug.WriteLine($"File inaccessibile: {file} - {ex.Message}");
                            continue;
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Directory non accessibile, continuare con la prossima
                    Debug.WriteLine($"Accesso negato alla directory: {directory}");
                }
                catch (DirectoryNotFoundException)
                {
                    // Directory eliminata durante la ricerca
                    Debug.WriteLine($"Directory non trovata: {directory}");
                }

                // Incrementa e reporta il progresso
                processedDirectories++;
                var currentProgress = (int)((double)processedDirectories / totalDirectories * 100);

                // Report progresso solo se cambia significativamente (almeno 5% o ultimo)
                if (currentProgress != lastProgressReport && (currentProgress % 5 == 0 || processedDirectories == totalDirectories))
                {
                    progress?.Report(Math.Min(currentProgress, 100));
                    lastProgressReport = currentProgress;
                }
            }

            Debug.WriteLine($"Ricerca completata. Trovati {resultsFound} file in {processedDirectories} directory.");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            Debug.WriteLine($"Ricerca timeout dopo {timeoutMs}ms");
            throw new TimeoutException($"La ricerca ha superato il timeout di {timeoutMs / 1000} secondi.");
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Ricerca cancellata dall'utente");
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Errore durante la ricerca: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Annulla la ricerca corrente
    /// </summary>
    public void CancelSearch()
    {
        _searchCts?.Cancel();
    }

    /// <summary>
    /// Enumerazione sicura delle directory con gestione errori
    /// </summary>
    private async IAsyncEnumerable<string> EnumerateDirectoriesSafeAsync(
        string rootPath,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var directoriesQueue = new ConcurrentQueue<string>();
        directoriesQueue.Enqueue(rootPath);

        while (directoriesQueue.TryDequeue(out var currentDir))
        {
            ct.ThrowIfCancellationRequested();

            yield return currentDir;

            // Ottieni le sottodirectory in modo sicuro
            var subDirs = await Task.Run(() =>
            {
                try
                {
                    return Directory.EnumerateDirectories(currentDir, "*", SearchOption.TopDirectoryOnly).ToList();
                }
                catch (UnauthorizedAccessException)
                {
                    return new List<string>();
                }
                catch (DirectoryNotFoundException)
                {
                    return new List<string>();
                }
            }, ct).ConfigureAwait(false);

            foreach (var subDir in subDirs)
            {
                directoriesQueue.Enqueue(subDir);
            }

            // Yield per permettere ad altre operazioni di procedere
            await Task.Yield();
        }
    }

    #endregion
}
