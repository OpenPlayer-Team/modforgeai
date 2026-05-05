# 📁 Architettura Cloud - Design Document

## Panoramica
L'infrastruttura cloud è progettata con principi di **separazione netta tra UI e provider**, **modularità** e **sicurezza**. Il sistema supporta provider multipli ed è facilmente estendibile.

## Componenti

### 1. ICloudProvider (Contratto)
```csharp
public interface ICloudProvider
{
    string ProviderName { get; }
    bool IsConnected { get; }
    
    Task<bool> ConnectAsync(CancellationToken ct);
    Task<IEnumerable<CloudItem>> ListFoldersAsync(string path, CancellationToken ct);
    Task<IEnumerable<CloudItem>> ListFilesAsync(string path, CancellationToken ct);
    Task DownloadAsync(string cloudPath, string localPath, IProgress<int> progress, CancellationToken ct);
    void Disconnect();
}
```

**Responsabilità:**
- Definire il contratto per operazioni cloud
- Nascondere dettagli implementativi
- Garantire consistenza tra provider

### 2. CloudItem (Modello Dati)
```csharp
public class CloudItem
{
    public string Name { get; set; }
    public string CloudPath { get; set; }
    public bool IsFolder { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string Provider { get; set; }
}
```

**Campi:**
- `Name`: Nome visualizzato
- `CloudPath`: Percorso univoco nel cloud
- `IsFolder`: Tipo di elemento
- `Size`: Dimensione in byte
- `LastModified`: Data ultima modifica
- `Provider`: Nome provider di origine

### 3. CloudServiceFactory (Gestione)
```csharp
public class CloudServiceFactory
{
    private ConcurrentDictionary<string, ICloudProvider> _providers;
    private ConcurrentDictionary<string, ICloudProvider> _connectedProviders;
    
    // Singleton pattern
    public static CloudServiceFactory Instance { get; }
    
    // Registrazione dinamica
    public bool RegisterProvider(string key, ICloudProvider provider);
    public ICloudProvider? GetProvider(string key);
    
    // Gestione connessioni
    public Task<bool> ConnectProviderAsync(string key, CancellationToken ct);
    public void DisconnectProvider(string key);
    public void DisconnectAll();
}
```

**Pattern Singleton:**
- Accesso globale tramite `CloudServiceFactory.Instance`
- Inizializzazione lazy
- Thread-safe

**Dizionari:**
- `_providers`: Tutti i provider registrati
- `_connectedProviders`: Solo quelli connessi

**Thread Safety:**
- `ConcurrentDictionary` per accesso concorrente
- Operazioni atomiche

### 4. MockProvider (Implementazione)
```csharp
public class MockProvider : ICloudProvider
{
    private readonly ConcurrentDictionary<string, List<CloudItem>> _mockData;
    
    // Dati realistici
    private void InitializeMockData()
    {
        // Struttura OneDrive/Google Drive
        // File con dimensioni realistiche
        // Date distribuite casualmente
    }
}
```

**Caratteristiche:**
- Dati finti ma realistici
- Simula ritardi di rete
- Generazione casuale coerente
- Nessuna dipendenza esterna

## Flusso di Connessione

```
1. Utente clicca "OneDrive" nel menu Cloud
   ↓
2. MainWindow.CloudProvider_Click()
   ↓
3. CloudServiceFactory.ConnectProviderAsync("OneDriveMock")
   ↓
4. MockProvider.ConnectAsync()
   ↓
5. Simula OAuth (placeholder commentato)
   ↓
6. _isConnected = true
   ↓
7. Apri nuova scheda "☁️ OneDrive"
   ↓
8. Carica cartelle/file in background
```

## Flusso di Visualizzazione

```
1. Apri scheda cloud
   ↓
2. LoadCloudRootAsync(provider, tab)
   ↓
3. provider.ListFoldersAsync("/root")
   ↓
4. provider.ListFilesAsync("/root")
   ↓
5. Aggiungi elementi a tab.Items
   ↓
6. UI mostra "☁️ NomeCartella"
```

## Estensibilità

### Aggiungere Nuovo Provider

**Passo 1: Implementare ICloudProvider**
```csharp
public class DropboxProvider : ICloudProvider
{
    public string ProviderName => "Dropbox";
    public bool IsConnected { get; private set; }
    
    public async Task<bool> ConnectAsync(CancellationToken ct)
    {
        // Implementa OAuth Dropbox
        return true;
    }
    
    // Implementa altri metodi...
}
```

**Passo 2: Registrare nella Factory**
```csharp
// Inizializzazione app
var factory = CloudServiceFactory.Instance;
factory.RegisterProvider("Dropbox", new DropboxProvider());
```

**Passo 3: Aggiungere al Menu**
```xml
<MenuFlyoutItem Text="Dropbox" 
                Tag="Dropbox"
                Click="CloudProvider_Click" />
```

**FATTO!** Nessuna modifica alla UI necessaria.

## Sicurezza

### OAuth Placeholder
```csharp
// In ICloudProvider.ConnectAsync()
// ================================================
// DA IMPLEMENTARE:
//
// MSAL (OneDrive):
//   var app = PublicClientApplicationBuilder.Create(ClientId)...
//   var result = await app.AcquireTokenInteractive(scopes)...
//
// Google Auth:
//   var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(...)
//
// Sicurezza:
//   - MAI loggare token
//   - Usare SecureStorage
//   - HTTPS sempre
//   - Gestire 401/403
// ================================================
```

### Best Practices
1. **Storage Sicuro**: KeyVault/SecureStorage per token
2. **HTTPS**: Validazione certificati
3. **Input Validation**: Sanitizzare percorsi
4. **Error Handling**: Non esporre dettagli
5. **Token Refresh**: Prima della scadenza
6. **Rate Limiting**: Evitare throttling API

## Pattern Utilizzati

### 1. Singleton
- `CloudServiceFactory.Instance`
- Accesso globale controllato

### 2. Factory Method
- `RegisterProvider()`
- Creazione dinamica

### 3. Strategy
- `ICloudProvider`
- Algoritmi intercambiabili

### 4. Dependency Injection
- Costruttori
- Interfacce

### 5. Async/Await
- Operazioni non bloccanti
- `IAsyncEnumerable` per streaming

## Testabilità

### Mocking
```csharp
// Test con Moq
var mockProvider = new Mock<ICloudProvider>();
mockProvider.Setup(p => p.ListFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new List<CloudItem> { /* dati test */ });
```

### Integration Test
```csharp
// Test reale con MockProvider
var provider = new MockProvider("Test");
await provider.ConnectAsync();
var folders = await provider.ListFoldersAsync("/root");
Assert.NotEmpty(folders);
```

## Performance

### Ottimizzazioni
- `ConcurrentDictionary`: Accesso O(1)
- `ConfigureAwait(false)`: Evita context switch
- `IAsyncEnumerable`: Streaming, non buffering
- Caching: Dati mock in memoria

### Limiti (Mock)
- Max 1000 cartelle per conteggio
- Timeout simulato 30s
- Ritardo rete 100-500ms

## Estensioni Future

1. **Cache**: Memorizzazione locale
2. **Background Sync**: Aggiornamenti periodici
3. **Delta Query**: Solo modifiche
4. **Batch Operations**: Download multipli
5. **Compression**: Trasferimento ottimizzato
6. **Retry Logic**: Tentativi automatici
7. **Circuit Breaker**: Prevenzione overload

## Conclusione

L'architettura cloud è:
- ✅ **Modulare**: Nuovi provider senza modifiche UI
- ✅ **Sicura**: Placeholder OAuth chiari
- ✅ **Testabile**: MockProvider integrato
- ✅ **Estensibile**: Design aperto alle estensioni
- ✅ **Mantenibile**: Separazione delle responsabilità
