using System.Collections.Concurrent;

namespace WinUIMvvmApp.Cloud;

/// <summary>
/// Provider mock che simula un servizio cloud reale
/// Genera dati realistici per test e sviluppo
/// </summary>
public class MockProvider : ICloudProvider
{
    #region Fields

    private readonly string _providerName;
    private bool _isConnected;
    private readonly Random _random;
    private readonly ConcurrentDictionary<string, List<CloudItem>> _mockData;

    #endregion

    #region Constructor

    /// <summary>
    /// Crea un nuovo provider mock
    /// </summary>
    /// <param name="providerName">Nome del provider (es. "OneDrive", "GoogleDrive")</param>
    public MockProvider(string providerName)
    {
        _providerName = providerName;
        _random = new Random(DateTime.Now.Millisecond);
        _mockData = new ConcurrentDictionary<string, List<CloudItem>>();
        InitializeMockData();
    }

    #endregion

    #region Properties

    public string ProviderName => _providerName;
    public bool IsConnected => _isConnected;

    #endregion

    #region Private Methods - Mock Data Initialization

    /// <summary>
    /// Inizializza i dati mock con struttura realistica
    /// </summary>
    private void InitializeMockData()
    {
        // Struttura base per OneDrive
        if (_providerName == "OneDrive")
        {
            AddMockFolder("/root", "Documenti");
            AddMockFolder("/root", "Immagini");
            AddMockFolder("/root", "Musica");
            AddMockFolder("/root", "Video");
            AddMockFolder("/root", "Desktop");
            AddMockFolder("/root", "Download");

            // Sottocartelle Documenti
            AddMockFolder("/root/Documenti", "Lavoro");
            AddMockFolder("/root/Documenti", "Personale");
            AddMockFolder("/root/Documenti", "Fatture");
            AddMockFolder("/root/Documenti", "Progetti");

            // File in Documenti
            AddMockFile("/root/Documenti", "Relazione_Annuale_2025.pdf", 2457600);
            AddMockFile("/root/Documenti", "Budget_2026.xlsx", 1048576);
            AddMockFile("/root/Documenti", "Presentazione.pptx", 5242880);
            AddMockFile("/root/Documenti", "Note_Riunione.txt", 4096);

            // File in Lavoro
            AddMockFile("/root/Documenti/Lavoro", "Progetto_Alpha.docx", 81920);
            AddMockFile("/root/Documenti/Lavoro", "Specifiche_Tecniche.pdf", 1536000);
            AddMockFile("/root/Documenti/Lavoro", "Timeline_Progetto.xlsx", 204800);

            // File in Immagini
            AddMockFile("/root/Immagini", "Vacanza_Estate_2024.jpg", 3145728);
            AddMockFile("/root/Immagini", "Logo_Aziendale.png", 512000);
            AddMockFile("/root/Immagini", "Screenshot_App.png", 256000);
            AddMockFile("/root/Immagini", "Diagramma_Architettura.jpg", 1048576);

            // File in Musica
            AddMockFile("/root/Musica", "Playlist_Preferiti.mp3", 52428800);
            AddMockFile("/root/Musica", "Colonna_Sonora.wav", 104857600);

            // File in Video
            AddMockFile("/root/Video", "Presentazione_Prodotto.mp4", 209715200);
            AddMockFile("/root/Video", "Tutorial_Software.mkv", 524288000);
        }
        // Struttura base per Google Drive
        else if (_providerName == "GoogleDrive")
        {
            AddMockFolder("/root", "My Drive");
            AddMockFolder("/root", "Shared with me");
            AddMockFolder("/root", "Starred");
            AddMockFolder("/root", "Recent");

            // My Drive
            AddMockFolder("/root/My Drive", "Work");
            AddMockFolder("/root/My Drive", "Personal");
            AddMockFolder("/root/My Drive", "School");
            AddMockFolder("/root/My Drive", "Projects");

            // File in My Drive
            AddMockFile("/root/My Drive", "Resume_2026.pdf", 512000);
            AddMockFile("/root/My Drive", "Cover_Letter.docx", 20480);
            AddMockFile("/root/My Drive", "Portfolio.pdf", 10485760);

            // File in Work
            AddMockFile("/root/My Drive/Work", "Team_Meeting_Notes.txt", 8192);
            AddMockFile("/root/My Drive/Work", "Project_Plan.gdoc", 16384);
            AddMockFile("/root/My Drive/Work", "Client_List.gsheet", 32768);

            // File in Personal
            AddMockFile("/root/My Drive/Personal", "Recipe_Collection.pdf", 204800);
            AddMockFile("/root/My Drive/Personal", "Travel_Plans_2026.gdoc", 10240);

            // File in Projects
            AddMockFile("/root/My Drive/Projects", "Website_Mockup.fig", 2097152);
            AddMockFile("/root/My Drive/Projects", "Database_Design.drawio", 512000);
            AddMockFile("/root/My Drive/Projects", "API_Documentation.md", 40960);
        }

        // Aggiungi cartella speciale per test
        AddMockFolder("/root", "Shared");
        AddMockFile("/root/Shared", "Team_Photo.jpg", 2097152);
        AddMockFile("/root/Shared", "Announcement.pdf", 102400);
    }

    private void AddMockFolder(string parentPath, string folderName)
    {
        var fullPath = $"{parentPath}/{folderName}".Replace("//", "/");
        var item = new CloudItem
        {
            Name = folderName,
            CloudPath = fullPath,
            IsFolder = true,
            Size = 0,
            LastModified = DateTime.Now.AddDays(-_random.Next(1, 365)),
            Provider = _providerName
        };

        var path = parentPath == "/root" ? "/root" : parentPath;
        if (!_mockData.ContainsKey(path))
        {
            _mockData[path] = new List<CloudItem>();
        }
        _mockData[path].Add(item);

        // Crea la cartella vuota per i figli
        if (!_mockData.ContainsKey(fullPath))
        {
            _mockData[fullPath] = new List<CloudItem>();
        }
    }

    private void AddMockFile(string folderPath, string fileName, long size)
    {
        var fullPath = $"{folderPath}/{fileName}".Replace("//", "/");
        var item = new CloudItem
        {
            Name = fileName,
            CloudPath = fullPath,
            IsFolder = false,
            Size = size,
            LastModified = DateTime.Now.AddDays(-_random.Next(1, 90)),
            Provider = _providerName
        };

        var path = folderPath == "/root" ? "/root" : folderPath;
        if (!_mockData.ContainsKey(path))
        {
            _mockData[path] = new List<CloudItem>();
        }
        _mockData[path].Add(item);
    }

    #endregion

    #region ICloudProvider Implementation

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Debug.WriteLine($"Connessione a {_providerName} in corso...");

        // Simula ritardo di rete
        await Task.Delay(_random.Next(500, 1500), cancellationToken).ConfigureAwait(false);

        // PLACEHOLDER OAUTH REALE:
        // ================================================
        // Qui va implementata la logica OAuth reale:
        //
        // Per OneDrive (MSAL):
        //   - Registrare app su Azure Portal
        //   - Configurare redirect URI
        //   - Richiedere scope: Files.Read, Files.ReadWrite
        //   - Salvare token in SecureStorage
        //   - Implementare refresh token
        //
        // Per Google Drive:
        //   - Creare progetto su Google Cloud Console
        //   - Abilitare Google Drive API
        //   - Creare OAuth 2.0 credentials
        //   - Richiedere scope: https://www.googleapis.com/auth/drive.readonly
        //   - Usare GoogleWebAuthorizationBroker
        //
        // Sicurezza:
        //   - MAI loggare token o client secret
        //   - Usare HTTPS per tutte le chiamate API
        //   - Implementare gestione errori 401/403
        //   - Aggiornare token prima della scadenza
        // ================================================

        _isConnected = true;
        System.Diagnostics.Debug.WriteLine($"Connesso a {_providerName}");
        return true;
    }

    public async Task<IEnumerable<CloudItem>> ListFoldersAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Provider non connesso. Chiamare ConnectAsync prima.");

        // Normalizza il percorso
        var normalizedPath = NormalizePath(path);

        // Simula ritardo di rete
        await Task.Delay(_random.Next(100, 500), cancellationToken).ConfigureAwait(false);

        if (_mockData.TryGetValue(normalizedPath, out var items))
        {
            return items.Where(i => i.IsFolder).ToList();
        }

        return Enumerable.Empty<CloudItem>();
    }

    public async Task<IEnumerable<CloudItem>> ListFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Provider non connesso. Chiamare ConnectAsync prima.");

        // Normalizza il percorso
        var normalizedPath = NormalizePath(path);

        // Simula ritardo di rete
        await Task.Delay(_random.Next(100, 500), cancellationToken).ConfigureAwait(false);

        if (_mockData.TryGetValue(normalizedPath, out var items))
        {
            return items.Where(i => !i.IsFolder).ToList();
        }

        return Enumerable.Empty<CloudItem>();
    }

    public async Task DownloadAsync(string cloudPath, string localPath, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Provider non connesso. Chiamare ConnectAsync prima.");

        System.Diagnostics.Debug.WriteLine($"Download: {cloudPath} -> {localPath}");

        // Simula download con progresso
        var file = await GetFileItemAsync(cloudPath, cancellationToken).ConfigureAwait(false);
        if (file == null)
            throw new FileNotFoundException("File non trovato nel cloud", cloudPath);

        var totalBytes = file.Size;
        var downloaded = 0L;
        var bufferSize = 81920; // 80KB chunks

        // Crea directory locale se non esiste
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Simula download in chunks
        using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write))
        {
            while (downloaded < totalBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var chunkSize = (int)Math.Min(bufferSize, totalBytes - downloaded);
                var buffer = new byte[chunkSize];
                _random.NextBytes(buffer);

                await fileStream.WriteAsync(buffer, 0, chunkSize, cancellationToken).ConfigureAwait(false);
                downloaded += chunkSize;

                var percentComplete = (int)((double)downloaded / totalBytes * 100);
                progress?.Report(percentComplete);

                // Simula latenza di rete
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        System.Diagnostics.Debug.WriteLine($"Download completato: {localPath}");
    }

    public void Disconnect()
    {
        _isConnected = false;
        System.Diagnostics.Debug.WriteLine($"Disconnesso da {_providerName}");
    }

    #endregion

    #region Private Helper Methods

    private async Task<CloudItem?> GetFileItemAsync(string cloudPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(cloudPath)?.Replace("\\", "/") ?? "/root";
        var fileName = Path.GetFileName(cloudPath);

        var normalizedPath = NormalizePath(directory);

        if (_mockData.TryGetValue(normalizedPath, out var items))
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            return items.FirstOrDefault(i => !i.IsFolder && i.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            return "/root";

        return path.Replace("\\", "/").TrimEnd('/');
    }

    #endregion
}
