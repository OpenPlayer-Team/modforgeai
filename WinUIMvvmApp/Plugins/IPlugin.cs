using System.Reflection;

namespace WinUIMvvmApp.Plugins;

/// <summary>
/// Interfaccia base per tutti i plugin
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Nome del plugin
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Versione del plugin (formato: Major.Minor.Build)
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Descrizione del plugin
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Inizializza il plugin con i servizi dell'applicazione
    /// </summary>
    /// <param name="serviceProvider">Provider di servizi dell'applicazione</param>
    /// <returns>True se l'inizializzazione è riuscita</returns>
    bool Initialize(IServiceProvider serviceProvider);

    /// <summary>
    /// Ottiene i comandi esposti dal plugin
    /// </summary>
    /// <returns>Enumerazione di comandi disponibili</returns>
    IEnumerable<PluginCommand> GetCommands();

    /// <summary>
    /// Esegue la pulizia delle risorse
    /// </summary>
    void Cleanup();
}

/// <summary>
/// Comando esposto da un plugin
/// </summary>
public class PluginCommand
{
    /// <summary>
    /// Nome del comando
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descrizione del comando
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Icona del comando (carattere Symbol)
    /// </summary>
    public string Icon { get; set; } = "⚙️";

    /// <summary>
    /// Delegato per l'esecuzione asincrona
    /// </summary>
    public Func<Task>? ExecuteAsync { get; set; }

    /// <summary>
    /// Indica se il comando è attualmente abilitato
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Plugin proprietario
    /// </summary>
    public IPlugin? Owner { get; set; }
}

/// <summary>
/// Risultato del caricamento di un plugin
/// </summary>
public class PluginLoadResult
{
    /// <summary>
    /// Nome del plugin
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// Percorso del file DLL
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Indica se il caricamento è riuscito
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Messaggio di errore (se Success = false)
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Dettagli dell'eccezione
    /// </summary>
    public string? ExceptionDetails { get; set; }

    /// <summary>
    /// Istanza del plugin caricato (se Success = true)
    /// </summary>
    public IPlugin? Plugin { get; set; }

    /// <summary>
    /// Comandi registrati
    /// </summary>
    public List<PluginCommand> Commands { get; set; } = new();
}
