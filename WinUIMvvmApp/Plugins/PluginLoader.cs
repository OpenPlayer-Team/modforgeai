using System.Reflection;
using System.Runtime.Loader;
using Microsoft.UI.Xaml.Controls;
using WinUIMvvmApp.Views;

namespace WinUIMvvmApp.Plugins;

/// <summary>
/// Loader per plugin con isolamento tramite AssemblyLoadContext
/// Carica assembly in un contesto separato per evitare contaminazione
/// </summary>
public class PluginLoader : IDisposable
{
    #region Fields

    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, PluginLoadContext> _loadContexts;
    private readonly List<PluginLoadResult> _loadResults;
    private bool _disposed;

    #endregion

    #region Events

    /// <summary>
    /// Evento scatenato quando un plugin viene caricato
    /// </summary>
    public event EventHandler<PluginLoadResult>? PluginLoaded;

    /// <summary>
    /// Evento scatenato quando un plugin viene scaricato
    /// </summary>
    public event EventHandler<string>? PluginUnloaded;

    #endregion

    #region Constructor

    /// <summary>
    /// Crea un nuovo PluginLoader
    /// </summary>
    /// <param name="serviceProvider">Provider di servizi dell'applicazione</param>
    public PluginLoader(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _loadContexts = new Dictionary<string, PluginLoadContext>(StringComparer.OrdinalIgnoreCase);
        _loadResults = new List<PluginLoadResult>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Carica tutti i plugin dalla cartella ./plugins
    /// </summary>
    /// <returns>Risultati del caricamento</returns>
    public async Task<IEnumerable<PluginLoadResult>> LoadAllPluginsAsync()
    {
        var pluginsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        
        try
        {
            return await LoadPluginsFromFolderAsync(pluginsFolder).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore caricamento plugin: {ex}");
            return new List<PluginLoadResult>
            {
                new()
                {
                    PluginName = "PluginLoader",
                    Success = false,
                    ErrorMessage = $"Errore accesso cartella plugin: {ex.Message}",
                    ExceptionDetails = ex.ToString()
                }
            };
        }
    }

    /// <summary>
    /// Carica plugin da una cartella specifica
    /// </summary>
    /// <param name="folderPath">Percorso cartella</param>
    /// <returns>Risultati del caricamento</returns>
    public async Task<IEnumerable<PluginLoadResult>> LoadPluginsFromFolderAsync(string folderPath)
    {
        var results = new List<PluginLoadResult>();

        if (!Directory.Exists(folderPath))
        {
            System.Diagnostics.Debug.WriteLine($"Cartella plugin non esistente: {folderPath}");
            Directory.CreateDirectory(folderPath);
            System.Diagnostics.Debug.WriteLine($"Cartella plugin creata: {folderPath}");
            return results;
        }

        var dllFiles = Directory.GetFiles(folderPath, "*.dll", SearchOption.TopDirectoryOnly);
        
        if (dllFiles.Length == 0)
        {
            System.Diagnostics.Debug.WriteLine("Nessun plugin DLL trovato");
            return results;
        }

        System.Diagnostics.Debug.WriteLine($"Trovate {dllFiles.Length} DLL da caricare");

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var result = await LoadPluginAsync(dllPath).ConfigureAwait(false);
                results.Add(result);
                
                PluginLoaded?.Invoke(this, result);
                
                System.Diagnostics.Debug.WriteLine(
                    result.Success 
                        ? $"Plugin caricato: {result.PluginName} v{result.Plugin.Version}"
                        : $"Errore caricamento {Path.GetFileName(dllPath)}: {result.ErrorMessage}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore critico caricamento {dllPath}: {ex}");
                results.Add(new PluginLoadResult
                {
                    PluginName = Path.GetFileNameWithoutExtension(dllPath),
                    AssemblyPath = dllPath,
                    Success = false,
                    ErrorMessage = $"Errore critico: {ex.Message}",
                    ExceptionDetails = ex.ToString()
                });
            }
        }

        _loadResults.AddRange(results);
        return results;
    }

    /// <summary>
    /// Carica un singolo plugin
    /// </summary>
    /// <param name="dllPath">Percorso DLL</param>
    /// <returns>Risultato del caricamento</returns>
    public async Task<PluginLoadResult> LoadPluginAsync(string dllPath)
    {
        if (!File.Exists(dllPath))
        {
            return new PluginLoadResult
            {
                PluginName = Path.GetFileNameWithoutExtension(dllPath),
                AssemblyPath = dllPath,
                Success = false,
                ErrorMessage = "File non trovato"
            };
        }

        var pluginName = Path.GetFileNameWithoutExtension(dllPath);

        try
        {
            // Verifica se già caricato
            if (_loadContexts.ContainsKey(pluginName))
            {
                System.Diagnostics.Debug.WriteLine($"Plugin già caricato: {pluginName}");
                return new PluginLoadResult
                {
                    PluginName = pluginName,
                    AssemblyPath = dllPath,
                    Success = false,
                    ErrorMessage = "Plugin già caricato"
                };
            }

            // Crea contesto di caricamento isolato
            var loadContext = new PluginLoadContext(dllPath);
            _loadContexts[pluginName] = loadContext;

            System.Diagnostics.Debug.WriteLine($"Caricamento assembly: {dllPath}");

            // Carica l'assembly nel contesto isolato
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);

            // Cerca tipi che implementano IPlugin
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            if (pluginTypes.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"Nessun plugin trovato in {pluginName}");
                return new PluginLoadResult
                {
                    PluginName = pluginName,
                    AssemblyPath = dllPath,
                    Success = false,
                    ErrorMessage = "Nessuna implementazione di IPlugin trovata"
                };
            }

            var result = new PluginLoadResult
            {
                PluginName = pluginName,
                AssemblyPath = dllPath,
                Success = true
            };

            // Inizializza ogni plugin trovato
            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    var plugin = await InitializePluginAsync(pluginType, loadContext, result).ConfigureAwait(false);
                    if (plugin != null)
                    {
                        result.Plugin = plugin;
                        // Se ci sono più plugin nella stessa DLL, usiamo l'ultimo
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore inizializzazione {pluginType.Name}: {ex}");
                    result.Success = false;
                    result.ErrorMessage = $"Errore inizializzazione: {ex.Message}";
                    result.ExceptionDetails = ex.ToString();
                }
            }

            return result;
        }
        catch (BadImageFormatException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Formato assembly non valido {pluginName}: {ex}");
            return new PluginLoadResult
            {
                PluginName = pluginName,
                AssemblyPath = dllPath,
                Success = false,
                ErrorMessage = "Formato file non valido (probabilmente non è un assembly .NET)",
                ExceptionDetails = ex.ToString()
            };
        }
        catch (FileLoadException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore caricamento dipendenze {pluginName}: {ex}");
            return new PluginLoadResult
            {
                PluginName = pluginName,
                AssemblyPath = dllPath,
                Success = false,
                ErrorMessage = $"Dipendenza mancante: {ex.Message}",
                ExceptionDetails = ex.ToString()
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore caricamento {pluginName}: {ex}");
            return new PluginLoadResult
            {
                PluginName = pluginName,
                AssemblyPath = dllPath,
                Success = false,
                ErrorMessage = $"Errore caricamento: {ex.Message}",
                ExceptionDetails = ex.ToString()
            };
        }
    }

    /// <summary>
    /// Scarica un plugin
    /// </summary>
    /// <param name="pluginName">Nome del plugin</param>
    public void UnloadPlugin(string pluginName)
    {
        if (_loadContexts.TryGetValue(pluginName, out var context))
        {
            try
            {
                context.Unload();
                _loadContexts.Remove(pluginName);
                PluginUnloaded?.Invoke(this, pluginName);
                System.Diagnostics.Debug.WriteLine($"Plugin scaricato: {pluginName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore scaricamento {pluginName}: {ex}");
            }
        }
    }

    /// <summary>
    /// Ottiene tutti i plugin caricati
    /// </summary>
    public IEnumerable<IPlugin> GetLoadedPlugins()
    {
        return _loadResults
            .Where(r => r.Success && r.Plugin != null)
            .Select(r => r.Plugin!);
    }

    /// <summary>
    /// Ottiene tutti i comandi dei plugin
    /// </summary>
    public IEnumerable<PluginCommand> GetAllCommands()
    {
        return GetLoadedPlugins()
            .SelectMany(p => p.GetCommands())
            .Where(c => c.IsEnabled);
    }

    #endregion

    #region Private Methods

    private async Task<IPlugin?> InitializePluginAsync(
        Type pluginType,
        PluginLoadContext loadContext,
        PluginLoadResult result)
    {
        try
        {
            var plugin = Activator.CreateInstance(pluginType) as IPlugin;
            
            if (plugin == null)
            {
                System.Diagnostics.Debug.WriteLine($"Impossibile creare istanza di {pluginType.Name}");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"Inizializzazione plugin: {plugin.Name} v{plugin.Version}");

            var initialized = plugin.Initialize(_serviceProvider);
            
            if (!initialized)
            {
                System.Diagnostics.Debug.WriteLine($"Inizializzazione fallita per {plugin.Name}");
                result.Success = false;
                result.ErrorMessage = $"Inizializzazione fallita per {plugin.Name}";
                return null;
            }

            var commands = plugin.GetCommands().ToList();
            result.Commands.AddRange(commands);

            System.Diagnostics.Debug.WriteLine($"Plugin inizializzato: {plugin.Name} ({commands.Count} comandi)");

            return plugin;
        }
        catch (MissingMethodException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Costruttore mancante: {ex}");
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore inizializzazione plugin: {ex}");
            throw;
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            foreach (var context in _loadContexts.Values)
            {
                try
                {
                    context.Unload();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore scaricamento contesto: {ex}");
                }
            }
            _loadContexts.Clear();
            _loadResults.Clear();
        }

        _disposed = true;
    }

    ~PluginLoader()
    {
        Dispose(false);
    }

    #endregion
}

/// <summary>
/// Contesto di caricamento assembly isolato
/// Permette di scaricare assembly senza bloccare il dominio principale
/// </summary>
internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly string _assemblyPath;
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string assemblyPath)
        : base(isCollectible: true)
    {
        _assemblyPath = assemblyPath;
        _resolver = new AssemblyDependencyResolver(assemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Prova a risolvere le dipendenze
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fallback alle assembly di sistema
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
