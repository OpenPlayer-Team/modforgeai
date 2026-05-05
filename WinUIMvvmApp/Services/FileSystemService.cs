using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace WinUIMvvmApp.Services;

/// <summary>
/// Servizio per l'enumerazione del file system con gestione errori
/// </summary>
public class FileSystemService
{
    /// <summary>
    /// Enumerazione asincrona dei file e cartelle in un percorso
    /// </summary>
    /// <param name="path">Percorso da enumerare</param>
    /// <param name="ct">Token di cancellazione</param>
    /// <returns>IAsyncEnumerable di FileItem</returns>
    public async IAsyncEnumerable<FileItem> EnumerateAsync(string path, [EnumeratorCancellation] CancellationToken ct)
    {
        // Validazione input
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        // Se il percorso non esiste, restituisci lista vuota
        if (!Directory.Exists(path))
        {
            yield break;
        }

        // Usa una coda thread-safe per gestire l'enumerazione parallela se necessario
        var items = new ConcurrentBag<FileItem>();

        try
        {
            // Enumerazione asincrona delle cartelle
            await foreach (var dir in TaskAsyncEnumerable(path, ct).ConfigureAwait(false))
            {
                items.Add(dir);
            }

            // Enumerazione asincrona dei file
            await foreach (var file in TaskAsyncEnumerableFiles(path, ct).ConfigureAwait(false))
            {
                items.Add(file);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            // Log dell'errore di accesso negato
            System.Diagnostics.Debug.WriteLine($"Accesso negato al percorso: {path}. Errore: {ex.Message}");
            yield break;
        }
        catch (DirectoryNotFoundException ex)
        {
            // Log dell'errore di directory non trovata
            System.Diagnostics.Debug.WriteLine($"Directory non trovata: {path}. Errore: {ex.Message}");
            yield break;
        }
        catch (Exception ex)
        {
            // Log di altri errori imprevisti
            System.Diagnostics.Debug.WriteLine($"Errore imprevisto durante l'enumerazione di {path}: {ex.Message}");
            yield break;
        }

        // Restituisce gli elementi ordinati (cartelle prima, poi file, entrambi per nome)
        foreach (var item in items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name))
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    /// <summary>
    /// Enumerazione asincrona delle cartelle
    /// </summary>
    private async IAsyncEnumerable<FileItem> TaskAsyncEnumerable(string path, [EnumeratorCancellation] CancellationToken ct)
    {
        // Usa Task.Run per evitare blocchi sull'UI thread
        var directories = await Task.Run(() =>
        {
            try
            {
                return Directory.GetDirectories(path);
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
            catch (DirectoryNotFoundException)
            {
                return Array.Empty<string>();
            }
        }, ct).ConfigureAwait(false);

        foreach (var dir in directories)
        {
            ct.ThrowIfCancellationRequested();

            var dirInfo = new DirectoryInfo(dir);
            yield return new FileItem
            {
                Name = dirInfo.Name,
                FullPath = dirInfo.FullName,
                IsDirectory = true,
                Size = 0,
                LastModified = dirInfo.LastWriteTime
            };

            // Piccolo delay per simulare elaborazione asincrona (utile per UI)
            await Task.Yield();
        }
    }

    /// <summary>
    /// Enumerazione asincrona dei file
    /// </summary>
    private async IAsyncEnumerable<FileItem> TaskAsyncEnumerableFiles(string path, [EnumeratorCancellation] CancellationToken ct)
    {
        // Usa Task.Run per evitare blocchi sull'UI thread
        var files = await Task.Run(() =>
        {
            try
            {
                return Directory.GetFiles(path);
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
            catch (DirectoryNotFoundException)
            {
                return Array.Empty<string>();
            }
        }, ct).ConfigureAwait(false);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(file);
            yield return new FileItem
            {
                Name = fileInfo.Name,
                FullPath = fileInfo.FullName,
                IsDirectory = false,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime
            };

            // Piccolo delay per simulare elaborazione asincrona
            await Task.Yield();
        }
    }
}
