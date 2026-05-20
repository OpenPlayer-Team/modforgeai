using System.Collections.Concurrent;

namespace WinUIMvvmApp.Cloud;

/// <summary>
/// Factory for the creation and management of cloud providers
/// Implements pattern Singleton for access to global
/// </summary>
public class CloudServiceFactory
{
    #region Singleton Implementation

    private static readonly Lazy<CloudServiceFactory> _instance =
        new(() => new CloudServiceFactory());

    /// <summary>
    /// Singleton instance of the factory
    /// </summary>
    public static CloudServiceFactory Instance => _instance.Value;

    #endregion

    #region Fields

    private readonly ConcurrentDictionary<string, ICloudProvider> _providers;
    private readonly ConcurrentDictionary<string, ICloudProvider> _connectedProviders;

    #endregion

    #region Constructor

    /// <summary>
    /// Private constructor for pattern Singleton
    /// Registers the real cloud providers
    /// </summary>
    private CloudServiceFactory()
    {
        _providers = new ConcurrentDictionary<string, ICloudProvider>(StringComparer.OrdinalIgnoreCase);
        _connectedProviders = new ConcurrentDictionary<string, ICloudProvider>();

        // Register the REAL providers (not mocks)
        RegisterProvider("OneDrive", new OneDriveProvider());
        RegisterProvider("GoogleDrive", new GoogleDriveProvider());

        System.Diagnostics.Debug.WriteLine("CloudServiceFactory initialized with REAL providers");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Registers a new cloud provider
    /// </summary>
    /// <param name="key">Unique key for the provider</param>
    /// <param name="provider">Provider instance</param>
    /// <returns>True if registration successful</returns>
    public bool RegisterProvider(string key, ICloudProvider provider)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Provider key cannot be empty", nameof(key));

        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        var result = _providers.TryAdd(key, provider);
        if (result)
        {
            System.Diagnostics.Debug.WriteLine($"Provider registered: {key} -> {provider.ProviderName}");
        }
        return result;
    }

    /// <summary>
    /// Removes a registered provider
    /// </summary>
    /// <param name="key">Provider key</param>
    /// <returns>True if removal successful</returns>
    public bool UnregisterProvider(string key)
    {
        if (_providers.TryRemove(key, out var provider))
        {
            // Disconnect if connected
            if (provider.IsConnected)
            {
                provider.Disconnect();
                _connectedProviders.TryRemove(key, out _);
            }

            System.Diagnostics.Debug.WriteLine($"Provider unregistered: {key}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a registered provider
    /// </summary>
    /// <param name="key">Provider key</param>
    /// <returns>Provider instance or null if not found</returns>
    public ICloudProvider? GetProvider(string key)
    {
        _providers.TryGetValue(key, out var provider);
        return provider;
    }

    /// <summary>
    /// Gets all registered providers
    /// </summary>
    /// <returns>Read-only dictionary of providers</returns>
    public IReadOnlyDictionary<string, ICloudProvider> GetAllProviders()
    {
        return new Dictionary<string, ICloudProvider>(_providers);
    }

    /// <summary>
    /// Connects a specific provider
    /// </summary>
    /// <param name="key">Provider key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection successful</returns>
    public async Task<bool> ConnectProviderAsync(string key, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(key);
        if (provider == null)
            return false;

        try
        {
            var connected = await provider.ConnectAsync(cancellationToken).ConfigureAwait(false);
            if (connected)
            {
                _connectedProviders.AddOrUpdate(key, provider, (_, _) => provider);
                System.Diagnostics.Debug.WriteLine($"Provider connected: {key}");
            }
            return connected;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error connecting provider {key}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnects a specific provider
    /// </summary>
    /// <param name="key">Provider key</param>
    public void DisconnectProvider(string key)
    {
        var provider = GetProvider(key);
        if (provider != null && provider.IsConnected)
        {
            provider.Disconnect();
            _connectedProviders.TryRemove(key, out _);
            System.Diagnostics.Debug.WriteLine($"Provider disconnected: {key}");
        }
    }

    /// <summary>
    /// Disconnects all providers
    /// </summary>
    public void DisconnectAll()
    {
        foreach (var key in _connectedProviders.Keys)
        {
            DisconnectProvider(key);
        }
    }

    /// <summary>
    /// Gets all connected providers
    /// </summary>
    /// <returns>Read-only dictionary of connected providers</returns>
    public IReadOnlyDictionary<string, ICloudProvider> GetConnectedProviders()
    {
        return new Dictionary<string, ICloudProvider>(_connectedProviders);
    }

     /// <summary>
     /// Checks if a provider is connected
     /// </summary>
     /// <param name="key">Provider key</param>
     /// <returns>True if connected</returns>
     public bool IsProviderConnected(string key)
     {
         return _connectedProviders.ContainsKey(key);
     }

     #endregion

     #region Public Methods

    /// <summary>
    /// Registra un nuovo provider cloud
    /// </summary>
    /// <param name="key">Chiave univoca per il provider</param>
    /// <param name="provider">Istanza del provider</param>
    /// <returns>True se registrazione riuscita</returns>
    public bool RegisterProvider(string key, ICloudProvider provider)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("La chiave del provider non può essere vuota", nameof(key));

        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        var result = _providers.TryAdd(key, provider);
        if (result)
        {
            System.Diagnostics.Debug.WriteLine($"Provider registrato: {key} -> {provider.ProviderName}");
        }
        return result;
    }

    /// <summary>
    /// Rimuove un provider registrato
    /// </summary>
    /// <param name="key">Chiave del provider</param>
    /// <returns>True se rimozione riuscita</returns>
    public bool UnregisterProvider(string key)
    {
        if (_providers.TryRemove(key, out var provider))
        {
            // Disconnetti se connesso
            if (provider.IsConnected)
            {
                provider.Disconnect();
                _connectedProviders.TryRemove(key, out _);
            }

            System.Diagnostics.Debug.WriteLine($"Provider rimosso: {key}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ottiene un provider registrato
    /// </summary>
    /// <param name="key">Chiave del provider</param>
    /// <returns>Istanza del provider o null se non trovato</returns>
    public ICloudProvider? GetProvider(string key)
    {
        _providers.TryGetValue(key, out var provider);
        return provider;
    }

    /// <summary>
    /// Ottiene tutti i provider registrati
    /// </summary>
    /// <returns>Dizionario dei provider</returns>
    public IReadOnlyDictionary<string, ICloudProvider> GetAllProviders()
    {
        return new Dictionary<string, ICloudProvider>(_providers);
    }

    /// <summary>
    /// Connette un provider specifico
    /// </summary>
    /// <param name="key">Chiave del provider</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>True se connessione riuscita</returns>
    public async Task<bool> ConnectProviderAsync(string key, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(key);
        if (provider == null)
            return false;

        try
        {
            var connected = await provider.ConnectAsync(cancellationToken).ConfigureAwait(false);
            if (connected)
            {
                _connectedProviders.AddOrUpdate(key, provider, (_, _) => provider);
                System.Diagnostics.Debug.WriteLine($"Provider connesso: {key}");
            }
            return connected;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore connessione provider {key}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnette un provider specifico
    /// </summary>
    /// <param name="key">Chiave del provider</param>
    public void DisconnectProvider(string key)
    {
        var provider = GetProvider(key);
        if (provider != null && provider.IsConnected)
        {
            provider.Disconnect();
            _connectedProviders.TryRemove(key, out _);
            System.Diagnostics.Debug.WriteLine($"Provider disconnesso: {key}");
        }
    }

    /// <summary>
    /// Disconnette tutti i provider
    /// </summary>
    public void DisconnectAll()
    {
        foreach (var key in _connectedProviders.Keys)
        {
            DisconnectProvider(key);
        }
    }

    /// <summary>
    /// Ottiene i provider attualmente connessi
    /// </summary>
    /// <returns>Dizionario dei provider connessi</returns>
    public IReadOnlyDictionary<string, ICloudProvider> GetConnectedProviders()
    {
        return new Dictionary<string, ICloudProvider>(_connectedProviders);
    }

    /// <summary>
    /// Verifica se un provider è connesso
    /// </summary>
    /// <param name="key">Chiave del provider</param>
    /// <returns>True se connesso</returns>
    public bool IsProviderConnected(string key)
    {
        return _connectedProviders.ContainsKey(key);
    }

    #endregion
}
