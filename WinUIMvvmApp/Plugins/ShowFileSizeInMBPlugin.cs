using System.Diagnostics;
using WinUIMvvmApp.Models;
using WinUIMvvmApp.Services;

namespace WinUIMvvmApp.Plugins;

/// <summary>
/// Plugin di esempio: converte dimensioni file in MB e logga
/// </summary>
public class ShowFileSizeInMBPlugin : IPlugin
{
    private IServiceProvider? _serviceProvider;
    private FileSystemService? _fileSystemService;
    private bool _initialized;

    public string Name => "ShowFileSizeInMB";
    public Version Version => new Version(1, 0, 0);
    public string Description => "Converte le dimensioni dei file in Megabyte e li registra nel log di debug";

    public bool Initialize(IServiceProvider serviceProvider)
    {
        try
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            
            // Recupera i servizi necessari
            _fileSystemService = serviceProvider.GetService(typeof(FileSystemService)) as FileSystemService;
            
            if (_fileSystemService == null)
            {
                Debug.WriteLine($"[{Name}] Warning: FileSystemService non disponibile");
            }

            _initialized = true;
            Debug.WriteLine($"[{Name}] Plugin inizializzato correttamente v{Version}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Name}] Errore inizializzazione: {ex.Message}");
            _initialized = false;
            return false;
        }
    }

    public IEnumerable<PluginCommand> GetCommands()
    {
        if (!_initialized)
        {
            Debug.WriteLine($"[{Name}] Plugin non inizializzato, nessun comando disponibile");
            yield break;
        }

        yield return new PluginCommand
        {
            Name = "Converti Dimensioni in MB",
            Description = "Converte le dimensioni di tutti i file nella cartella corrente in Megabyte e li registra",
            Icon = "📊",
            IsEnabled = true,
            Owner = this,
            ExecuteAsync = async () => await ExecuteConvertToMBAsync()
        };

        yield return new PluginCommand
        {
            Name = "Analizza Cartella",
            Description = "Analizza la cartella corrente e mostra statistiche sulle dimensioni",
            Icon = "📈",
            IsEnabled = true,
            Owner = this,
            ExecuteAsync = async () => await ExecuteAnalyzeFolderAsync()
        };

        yield return new PluginCommand
        {
            Name = "Pulisci Log",
            Description = "Pulisce il log di output",
            Icon = "🧹",
            IsEnabled = true,
            Owner = this,
            ExecuteAsync = async () => await ExecuteClearLogAsync()
        };
    }

    private async Task ExecuteConvertToMBAsync()
    {
        try
        {
            Debug.WriteLine($"\n{'=',60}");
            Debug.WriteLine($"[{Name}] Conversione dimensioni in MB - {DateTime.Now:HH:mm:ss}");
            Debug.WriteLine($"{'=' ,60}");

            // Simula elaborazione
            await Task.Delay(100);

            // Esempio di conversione
            var testSizes = new long[] 
            { 
                1024,           // 1 KB
                1048576,        // 1 MB
                10485760,       // 10 MB
                1073741824,     // 1 GB
                5368709120      // 5 GB
            };

            foreach (var size in testSizes)
            {
                var mb = ConvertToMB(size);
                Debug.WriteLine($"  {size,15:N0} byte = {mb,10:F2} MB");
            }

            Debug.WriteLine($"{'=' ,60}\n");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Name}] Errore conversione: {ex.Message}");
        }
    }

    private async Task ExecuteAnalyzeFolderAsync()
    {
        try
        {
            Debug.WriteLine($"\n{'=' ,60}");
            Debug.WriteLine($"[{Name}] Analisi Cartella - {DateTime.Now:HH:mm:ss}");
            Debug.WriteLine($"{'=' ,60}");

            await Task.Delay(100); // Simula elaborazione

            // Statistiche di esempio
            var stats = new FolderStatistics
            {
                TotalFiles = 1234,
                TotalFolders = 56,
                TotalSizeBytes = 5368709120, // 5 GB
                LargestFile = new FileItemInfo { Name = "video.mp4", Size = 2147483648 }, // 2 GB
                SmallestFile = new FileItemInfo { Name = "note.txt", Size = 1024 } // 1 KB
            };

            Debug.WriteLine($"\n  Statistiche Cartella:");
            Debug.WriteLine($"  ├─ File totali: {stats.TotalFiles:N0}");
            Debug.WriteLine($"  ├─ Cartelle: {stats.TotalFolders:N0}");
            Debug.WriteLine($"  ├─ Dimensione totale: {ConvertToMB(stats.TotalSizeBytes):F2} MB ({ConvertToGB(stats.TotalSizeBytes):F2} GB)");
            Debug.WriteLine($"  ├─ File più grande: {stats.LargestFile.Name} ({ConvertToMB(stats.LargestFile.Size):F2} MB)");
            Debug.WriteLine($"  └─ File più piccolo: {stats.SmallestFile.Name} ({ConvertToMB(stats.SmallestFile.Size):F2} MB)");

            Debug.WriteLine($"\n  Media per file: {ConvertToMB(stats.TotalSizeBytes / stats.TotalFiles):F2} MB");
            Debug.WriteLine($"{'=' ,60}\n");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Name}] Errore analisi: {ex.Message}");
        }
    }

    private async Task ExecuteClearLogAsync()
    {
        try
        {
            // In un'app reale, pulirebbe il log
            // Qui simuliamo solo
            await Task.Delay(50);
            Debug.WriteLine($"\n[{Name}] Log pulito - {DateTime.Now:HH:mm:ss}\n");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Name}] Errore pulizia log: {ex.Message}");
        }
    }

    private double ConvertToMB(long bytes) => bytes / 1048576.0;
    private double ConvertToGB(long bytes) => bytes / 1073741824.0;

    public void Cleanup()
    {
        _initialized = false;
        _fileSystemService = null;
        _serviceProvider = null;
        Debug.WriteLine($"[{Name}] Plugin pulito");
    }

    // Classi di supporto
    private class FolderStatistics
    {
        public int TotalFiles { get; set; }
        public int TotalFolders { get; set; }
        public long TotalSizeBytes { get; set; }
        public FileItemInfo LargestFile { get; set; } = new();
        public FileItemInfo SmallestFile { get; set; } = new();
    }

    private class FileItemInfo
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}
