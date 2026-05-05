using System.Collections.Concurrent;

namespace WinUIMvvmApp.Cloud;

/// <summary>
/// Factory per la creazione e gestione dei provider cloud
/// Implementa pattern Singleton per accesso globale
/// </summary>
public class CloudServiceFactory
{
    #region Singleton Implementation

    private static readonly Lazy<CloudServiceFactory> _instance =
        new(() => new CloudServiceFactory());

    /// <summary>
    /// Istanza singleton della factory
    /// </summary>
    public static CloudServiceFactory Instance => _instance.Value;

    #endregion

    #region Fields

    private readonly ConcurrentDictionary<string, ICloudProvider> _providers;
    private readonly ConcurrentDictionary<string, ICloudProvider> _connectedProviders;

    #endregion

    #region Constructor

    /// <summary>
    /// Costruttore privato per pattern Singleton
    /// Registra i provider mock di default
    /// </summary>
    private CloudServiceFactory()
    {
        _providers = new ConcurrentDictionary<string, ICloudProvider>();
        _connectedProviders = new ConcurrentDictionary<string, ICloudProvider>();

        // Registra i provider mock di default
        RegisterProvider("OneDriveMock", new MockProvider("OneDrive"));
        RegisterProvider("GoogleDriveMock", new MockProvider("GoogleDrive"));

        System.Diagnostics.Debug.WriteLine("CloudServiceFactory inizializzata con provider mock");
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
