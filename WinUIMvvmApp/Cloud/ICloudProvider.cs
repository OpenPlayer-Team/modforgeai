using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WinUIMvvmApp.Cloud;

/// <summary>
/// Interfaccia base per i provider cloud
/// Definisce il contratto per operazioni cloud sicure e asincrone
/// </summary>
public interface ICloudProvider
{
    /// <summary>
    /// Nome identificativo del provider (es. "OneDrive", "GoogleDrive")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Indica se il provider è attualmente connesso
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connette al servizio cloud
    /// </summary>
    /// <param name="cancellationToken">Token per annullare l'operazione</param>
    /// <returns>True se la connessione è riuscita</returns>
    /// <remarks>
    /// PLACEHOLDER OAUTH:
    /// Per implementare OAuth reale, sostituire questo metodo con:
    /// 
    /// MSAL (Microsoft/OneDrive):
    /// ----------------------------------------
    /// var app = PublicClientApplicationBuilder
    ///     .Create(ClientId)
    ///     .WithRedirectUri("http://localhost")
    ///     .Build();
    /// var scopes = new[] { "Files.Read", "Files.ReadWrite" };
    /// var result = await app.AcquireTokenInteractive(scopes)
    ///     .ExecuteAsync(cancellationToken);
    /// _accessToken = result.AccessToken;
    /// ----------------------------------------
    /// 
    /// Google Auth (Google Drive):
    /// ----------------------------------------
    /// var clientSecrets = new ClientSecrets
    /// {
    ///     ClientId = "your-client-id",
    ///     ClientSecret = "your-client-secret"
    /// };
    /// var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
    ///     clientSecrets,
    ///     new[] { DriveService.Scope.DriveReadonly },
    ///     "user",
    ///     cancellationToken);
    /// ----------------------------------------
    /// 
    /// Best Practices:
    /// - Salvare il token in SecureStorage/KeyVault
    /// - Implementare token refresh automatico
    /// - Gestire scadenza token (401/403)
    /// - Non loggare mai token o secret
    /// - Usare HTTPS sempre
    /// </remarks>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Elenca le cartelle in un percorso cloud
    /// </summary>
    /// <param name="path">Percorso cloud (es. "/root", "/documents")</param>
    /// <param name="cancellationToken">Token per annullare l'operazione</param>
    /// <returns>Lista di cartelle cloud</returns>
    Task<IEnumerable<CloudItem>> ListFoldersAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elenca i file in un percorso cloud
    /// </summary>
    /// <param name="path">Percorso cloud (es. "/root", "/documents")</param>
    /// <param name="cancellationToken">Token per annullare l'operazione</param>
    /// <returns>Lista di file cloud</returns>
    Task<IEnumerable<CloudItem>> ListFilesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scarica un file dal cloud al percorso locale
    /// </summary>
    /// <param name="cloudPath">Percorso del file nel cloud</param>
    /// <param name="localPath">Percorso di destinazione locale</param>
    /// <param name="progress">Reporter per avanzamento download</param>
    /// <param name="cancellationToken">Token per annullare l'operazione</param>
    /// <returns>Task completato al termine del download</returns>
    Task DownloadAsync(string cloudPath, string localPath, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnette dal servizio cloud
    /// </summary>
    void Disconnect();
}

/// <summary>
/// Rappresenta un elemento (file o cartella) nel cloud
/// </summary>
public class CloudItem
{
    /// <summary>
    /// Nome dell'elemento
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Percorso completo nel cloud
    /// </summary>
    public string CloudPath { get; set; } = string.Empty;

    /// <summary>
    /// Indica se è una cartella
    /// </summary>
    public bool IsFolder { get; set; }

    /// <summary>
    /// Dimensione in byte (0 per le cartelle)
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Data di ultima modifica
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Icona associata
    /// </summary>
    public string Icon => IsFolder ? "📁" : "📄";

    /// <summary>
    /// Provider di origine
    /// </summary>
    public string Provider { get; set; } = string.Empty;
}
