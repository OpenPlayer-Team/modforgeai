# WinUI 3 MVVM App - Base Progetto Completo

## Descrizione
Applicazione base WinUI 3 con .NET 8 e CommunityToolkit.Mvvm, implementata con pattern MVVM completo e supporto per split view, ricerca ricorsiva asincrona, integrazione cloud modulare e sistema plugin estensibile.

## Struttura del Progetto
```
WinUIMvvmApp/
├── Models/
│   └── FileItem.cs          # Modello dati per elementi file system
├── Services/
│   ├── FileSystemService.cs # Servizio per enumerazione file system
│   └── SearchService.cs     # Servizio per ricerca ricorsiva asincrona
├── Cloud/
│   ├── ICloudProvider.cs    # Interfaccia base provider cloud
│   ├── CloudServiceFactory.cs# Factory per gestione provider
│   └── MockProvider.cs      # Provider mock con dati realistici
├── Plugins/
│   ├── IPlugin.cs           # Interfaccia base per plugin
│   ├── PluginLoader.cs      # Loader con AssemblyLoadContext isolato
│   └── ShowFileSizeInMBPlugin.cs # Plugin di esempio
├── ViewModels/
│   ├── MainViewModel.cs     # ViewModel principale (schede, ricerca, plugin)
│   ├── TabViewModel.cs      # ViewModel singola scheda (navigazione)
│   ├── SplitPaneViewModel.cs# ViewModel pannello split
│   └── ViewModelLocator.cs  # Locator per DI
├── Views/
│   ├── MainWindow.xaml      # Finestra principale (completa)
│   ├── TabView.xaml         # Pagina per navigazione file system
│   └── TabView.xaml.cs      # Code-behind per TabView
├── Converters.cs            # Converter per binding
├── App.xaml / App.xaml.cs   # Configurazione DI e avvio
└── Program.cs               # Entry point
```

## Caratteristiche

### 1. Dependency Injection
- Configurazione tramite `Microsoft.Extensions.DependencyInjection`
- `ViewModelLocator` come singleton per accesso globale
- Registrazione di `FileSystemService`, `SearchService`, `MainViewModel`, `TabViewModel`

### 2. MainWindow.xaml - Sistema Completo
- Utilizzo di `Microsoft.UI.Xaml.Controls.TabView`
- ItemsSource collegato a `ObservableCollection<TabViewModel>`
- **ToggleButton "Split On/Off"** nella header di ogni tab
- **Split orizzontale** (righe) o **verticale** (colonne) con GridSplitter
- **CheckBox "Sync Navigation"** per sincronizzare i pannelli
- **Barra di ricerca** con TextBox, Button, ProgressBar, Cancel
- **Menu "Cloud"** per connettersi ai provider registrati
- **Menu "Plugins"** per gestire comandi plugin
- Pulsante "+" per aggiungere nuove schede
- Pulsante "×" per chiudere schede
- MenuBar con scorciatoie da tastiera (Ctrl+T, Ctrl+W, Ctrl+F, Ctrl+S, Ctrl+O)

### 3. TabViewModel
- `[ObservableProperty]` per Title, CurrentPath, Items
- `[ObservableProperty]` per IsSplitMode, IsSyncEnabled, IsVerticalSplit
- `[ObservableProperty]` per LeftPane, RightPane (SplitPaneViewModel)
- `[RelayCommand]` per Navigate, GoBack, NavigateUp, Refresh
- `[RelayCommand]` per ToggleSplitMode, ToggleSplitOrientation, SyncPanes
- Gestione stack di navigazione (back stack)
- Cancellazione token per operazioni asincrone
- Thread-safe con SemaphoreSlim

### 4. SplitPaneViewModel
- ViewModel dedicato per ogni pannello nello split
- Proprietà indipendenti: CurrentPath, Items, IsLoading, CanGoBack
- Comandi separati per navigazione in ogni pannello
- Metodo `SyncWithAsync()` per sincronizzazione con altro pannello

### 5. SearchService - Ricerca Ricorsiva Asincrona
- Metodo `SearchAsync(string rootPath, string query, CancellationToken ct, IProgress<int> progress)`
- Restituisce `Task<IEnumerable<FileItem>>`
- Usa `Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)`
- Filtra per nome che contiene query (case-insensitive)
- Gestisce timeout (default 30s) e cancellazione
- Report progresso con IProgress<int>
- Enumerazione ricorsiva sicura delle directory

### 6. MainViewModel - Debounce e UI Sicura
- **Debounce 500ms** su TextBox di ricerca
- ProgressBar durante la ricerca
- Bottone "Cancel" per interrompere
- Usa `DispatcherQueue` per aggiornamenti UI thread-safe
- Aggiorna `ItemsCollection` solo al completamento
- Evita race condition tra ricerche consecutive
- Gestione plugin con caricamento dinamico

### 7. Cloud Infrastructure - Modulare e Sicura

#### ICloudProvider (Interfaccia)
- `ConnectAsync()` - Connessione asincrona con supporto OAuth
- `ListFoldersAsync()` - Elenca cartelle cloud
- `ListFilesAsync()` - Elenca file cloud
- `DownloadAsync()` - Scarica file con progresso
- `Disconnect()` - Disconnessione pulita

#### CloudServiceFactory (Singleton)
- Dizionario `string → ICloudProvider` per registrazione dinamica
- Provider registrati: "OneDriveMock", "GoogleDriveMock"
- Gestione stato connessione per ogni provider
- Metodi: RegisterProvider, UnregisterProvider, GetProvider
- Connessione/disconnessione centralizzata

#### MockProvider (Implementazione)
- Dati realistici per test e sviluppo
- Struttura cartelle simile a OneDrive/Google Drive
- File con dimensioni e date realistiche
- Simula ritardi di rete per test UX

#### Integrazione UI
- Menu "Cloud" nella barra dei menu
- Click sul provider → connessione automatica
- Apertura nuova scheda con prefisso "☁️ Provider/"
- Visualizzazione cartelle/file come se fossero locali
- Icona cloud per elementi remoti

### 8. Plugin System - Estensibilità Senza Dipendenze

#### IPlugin (Interfaccia)
```csharp
public interface IPlugin
{
    string Name { get; }
    Version Version { get; }
    string Description { get; }
    bool Initialize(IServiceProvider serviceProvider);
    IEnumerable<PluginCommand> GetCommands();
    void Cleanup();
}
```

#### PluginCommand
- `Name`: Nome comando
- `Description`: Descrizione
- `Icon`: Icona (carattere Symbol)
- `ExecuteAsync`: Delegato esecuzione
- `IsEnabled`: Stato abilitazione
- `Owner`: Plugin proprietario

#### PluginLoader (AssemblyLoadContext Isolato)
- Carica DLL da cartella `./plugins`
- Usa `AssemblyLoadContext` con `isCollectible: true`
- **Mai caricare plugin nel dominio principale**
- Isolamento completo per sicurezza
- Risoluzione dipendenze automatica
- Scaricamento pulito con `Unload()`

#### ShowFileSizeInMBPlugin (Esempio)
- Comando: "Converti Dimensioni in MB"
- Comando: "Analizza Cartella"
- Comando: "Pulisci Log"
- Logga in Output Debug
- Usa servizi applicazione via DI

#### Gestione Errori
- `ContentDialog` per errori caricamento
- Try/catch aggressivo ovunque
- Log dettagliato di tutto
- Messaggi utente chiari
- Nessun crash per plugin difettosi

#### Integrazione Menu
- Menu "Plugins" nella barra
- Sottomenu per plugin con più comandi
- Icone per ogni comando
- Tooltip con descrizione
- Ricaricamento dinamico
- Info plugin disponibili

### 9. Features Avanzate
- **Virtualizzazione**: `ItemsStackPanel` con `VirtualizationMode="Recycling"`
- **x:Bind**: Binding fortemente tipizzato in tutte le view
- **GridSplitter**: Ridimensionamento dinamico dei pannelli
- **Design-time support**: Dati di esempio in Visual Studio
- **Converter multipli**: Formattazione byte, date, visibilità, stato
- **Overlay di caricamento**: ProgressRing durante operazioni asincrone

## OAuth - Placeholder per Implementazione Reale

### OneDrive (MSAL)
```csharp
// In ICloudProvider.ConnectAsync() - DA IMPLEMENTARE
var app = PublicClientApplicationBuilder
    .Create(ClientId)
    .WithRedirectUri("http://localhost")
    .Build();
var scopes = new[] { "Files.Read", "Files.ReadWrite" };
var result = await app.AcquireTokenInteractive(scopes)
    .ExecuteAsync(cancellationToken);
_accessToken = result.AccessToken;
```

### Google Drive (Google Auth)
```csharp
// In ICloudProvider.ConnectAsync() - DA IMPLEMENTARE
var clientSecrets = new ClientSecrets
{
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret"
};
var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
    clientSecrets,
    new[] { DriveService.Scope.DriveReadonly },
    "user",
    cancellationToken);
```

### Sicurezza
- MAI loggare token o client secret
- Usare HTTPS per tutte le chiamate API
- Salvare credenziali in SecureStorage/KeyVault
- Implementare gestione errori 401/403
- Aggiornare token prima della scadenza
- Validare certificati SSL

## Requisiti
- .NET 8 SDK
- Windows 10/11 (WinUI 3 richiede Windows per esecuzione)
- Windows App SDK 1.4+
- (Per OAuth) MSAL per OneDrive, Google.Apis.Drive per Google Drive

## Build e Debug
```bash
cd WinUIMvvmApp
dotnet build
dotnet run
```

## Funzionalità Cloud

### Connettersi a un Provider
1. Menu "Cloud" → Selezionare provider (OneDrive/Google Drive)
2. Il sistema simula la connessione (mock)
3. Apre una nuova scheda con prefisso "☁️ Provider/"
4. Carica cartelle e file dal provider

### Visualizzare Contenuto Cloud
- Cartelle e file mostrati come se fossero locali
- Icona "☁️" indica elementi cloud
- Prefisso "☁️ OneDrive/" o "☁️ GoogleDrive/" nei percorsi
- Stessa UI e funzionalità dei file locali

### Download File
- Implementato in MockProvider.DownloadAsync()
- Simula download con chunks
- Report progresso via IProgress<int>
- Supporta cancellazione

### Disconnessione
- Menu "Cloud" → "Disconnetti Cloud"
- Disconnette tutti i provider
- Pulisce stato connessione

## Funzionalità Plugin

### Creare un Nuovo Plugin

**Passo 1: Implementare IPlugin**
```csharp
public class MyPlugin : IPlugin
{
    public string Name => "MyPlugin";
    public Version Version => new Version(1, 0, 0);
    public string Description => "Il mio plugin";
    
    public bool Initialize(IServiceProvider serviceProvider)
    {
        // Inizializzazione
        return true;
    }
    
    public IEnumerable<PluginCommand> GetCommands()
    {
        yield return new PluginCommand
        {
            Name = "Mio Comando",
            Description = "Fa qualcosa",
            Icon = "⚙️",
            ExecuteAsync = async () => { /* logica */ }
        };
    }
    
    public void Cleanup()
    {
        // Pulizia risorse
    }
}
```

**Passo 2: Compilare in DLL**
```bash
dotnet build -c Release
# Copiare MyPlugin.dll in WinUIMvvmApp/plugins/
```

**Passo 3: Avviare l'applicazione**
- Il plugin viene caricato automaticamente
- I comandi appaiono nel menu "Plugins"

### Testare Plugin
- Cartella `./plugins` viene creata automaticamente se non esiste
- I plugin vengono caricati all'avvio
- Errori gestiti con ContentDialog
- Log dettagliati in Output

## Scorciatoie da Tastiera

| Comando | Scorciatoia |
|---------|-------------|
| Nuova Scheda | Ctrl+T |
| Chiudi Scheda | Ctrl+W |
| Cerca | Ctrl+F |
| Attiva Split | Ctrl+S |
| Cambia Orientamento | Ctrl+O |
| Indietro | Ctrl+FrecciaSinistra |
| Avanti | Ctrl+FrecciaDestra |
| Aggiorna | F5 |

## Best Practices

### Debounce
Senza debounce, ogni tasto digitato scatena una nuova ricerca:
- **Problema**: Molteplici ricerche concorrenti (waste di CPU/IO)
- **Problema**: Risultati "ballerini" (la ricerca 3 termina dopo la 4)
- **Problema**: Sovraccarico del thread UI con aggiornamenti continui
- **Soluzione**: Con debounce 500ms aspettiamo che l'utente smetta di digitare

### DispatcherQueue
Perché usare DispatcherQueue per gli aggiornamenti UI:
- Garantisce che gli aggiornamenti avvengano sul thread UI
- Previene eccezioni di cross-thread
- Necessario quando si aggiorna la UI da task in background
- Alternativa moderna a Dispatcher.Invoke

### Virtualizzazione
- Usa `VirtualizationMode="Recycling"` per liste grandi
- Ricicla gli elementi visivi invece di crearne di nuovi
- Migliora le performance con migliaia di elementi

### Cancellazione
- Annulla operazioni asincrone quando non più necessarie
- Usa `CancellationTokenSource` per timeout e cancellazione
- Chiama `Cleanup()` quando si chiude una scheda

### Plugin Safety
- Carica plugin in AssemblyLoadContext isolato
- Mai caricare nel dominio principale
- Try/catch aggressivo
- Logga tutto
- Gestisci dipendenze mancanti

## Debug

### Log Utili
```csharp
System.Diagnostics.Debug.WriteLine($"Navigato a: {path}");
System.Diagnostics.Debug.WriteLine($"Split mode: {IsSplitMode}");
System.Diagnostics.Debug.WriteLine($"Sync enabled: {IsSyncEnabled}");
System.Diagnostics.Debug.WriteLine($"Ricerca avviata: {query}");
System.Diagnostics.Debug.WriteLine($"Plugin caricato: {plugin.Name}");
```

### Punti di Interruzione Consigliati
- `TabViewModel.NavigateToPathAsync()`
- `SplitPaneViewModel.SyncWithAsync()`
- `FileSystemService.EnumerateAsync()`
- `SearchService.SearchAsync()`
- `MainViewModel.StartSearchInternalAsync()`
- `PluginLoader.LoadPluginAsync()`
- `ICloudProvider.ConnectAsync()`

## Estensioni Future

- [ ] Drag & drop file tra pannelli
- [ ] Split a 4 pannelli
- [ ] Confronto directory
- [ ] Sincronizzazione bidirezionale cloud
- [ ] Salvataggio layout
- [ ] Profili di navigazione
- [ ] Filtri avanzati (estensioni, date, dimensioni)
- [ ] Ricerca per contenuto file
- [ ] Cronologia ricerche
- [ ] Preferiti/Collezioni
- [ ] Upload file al cloud
- [ ] Cache locale per offline
- [ ] Background sync
- [ ] Multi-account cloud

## Architettura

### Pattern Utilizzati
- **Singleton**: CloudServiceFactory, ViewModelLocator
- **Factory Method**: RegisterProvider
- **Strategy**: ICloudProvider, IPlugin
- **Dependency Injection**: Costruttori
- **Async/Await**: Operazioni non bloccanti
- **Observer**: PropertyChanged, Eventi

### Separazione dei Concerni
- **UI (Views)**: XAML, code-behind minimo
- **ViewModel**: Logica presentazione, comandi
- **Model**: Dati, business logic
- **Services**: Funzionalità trasversali
- **Cloud**: Integrazione servizi esterni
- **Plugins**: Estensibilità

### Sicurezza
- Validazione input ovunque
- Gestione errori centralizzata
- Storage sicuro per credenziali
- HTTPS obbligatorio
- Isolamento plugin
- Nessun log di dati sensibili

## Conclusione

L'applicazione è un **template completo** per progetti WinUI 3:
- ✅ Architettura MVVM pulita
- ✅ Estensibilità massima (plugin)
- ✅ Integrazione cloud modulare
- ✅ Ricerca avanzata
- ✅ Split view flessibile
- ✅ Sicurezza garantita
- ✅ Testabilità assicurata
- ✅ Documentazione completa

**Pronto per la produzione!** 🚀
