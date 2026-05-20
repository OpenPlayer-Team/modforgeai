using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using WinUIMvvmApp.Models;

namespace WinUIMvvmApp.Cloud;

/// <summary>
/// Real implementation of OneDrive provider using Microsoft Graph API
/// </summary>
public class OneDriveProvider : ICloudProvider
{
    private const string GraphEndpoint = "https://graph.microsoft.com/v1.0";
    private const string Scopes = "Files.ReadWrite.All";
    
    private string _clientId = ""; // TODO: Set your Azure AD app client ID
    private string _tenantId = ""; // TODO: Set your Azure AD tenant ID
    private string _redirectUri = "http://localhost"; // TODO: Set your redirect URI
    
    private IPublicClientApplication _publicClientApp;
    private AuthenticationResult _authResult;
    private HttpClient _httpClient;
    private bool _isConnected;
    private string _accessToken;

    public string ProviderName => "OneDrive";
    public bool IsConnected => _isConnected;

    public OneDriveProvider()
    {
        // Initialize MSAL public client application
        _publicClientApp = PublicClientApplicationBuilder.Create(_clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, _tenantId)
            .WithRedirectUri(_redirectUri)
            .Build();
        
        _httpClient = new HttpClient();
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: In a real app, you would attempt to acquire token silently first
            // from token cache, and only fall back to interactive if needed
            
            // For now, we'll use interactive authentication
            // In production, consider using device code flow or integrated auth
            var accounts = await _publicClientApp.GetAccountsAsync();
            
            _authResult = await _publicClientApp.AcquireTokenInteractive(new[] { Scopes })
                .WithAccount(accounts.FirstOrDefault())
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync(cancellationToken);
            
            _accessToken = _authResult.AccessToken;
            
            // Configure HTTP client with bearer token
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _accessToken);
            
            _isConnected = true;
            return true;
        }
        catch (MsalException ex)
        {
            // Handle MSAL specific errors
            System.Diagnostics.Debug.WriteLine($"MSAL error: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error connecting to OneDrive: {ex.Message}");
            return false;
        }
    }

    public async Task<IEnumerable<CloudItem>> ListFoldersAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Provider not connected. Call ConnectAsync first.");

        try
        {
            // Normalize path for Graph API
            string graphPath = NormalizePathForGraph(path);
            string requestUrl = $"{GraphEndpoint}/me/drive/root:/{graphPath}:/children";
            
            // Add filter for folders only
            requestUrl += "?$filter=folder ne null";
            
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument doc = JsonDocument.Parse(json);
            
            var folders = new List<CloudItem>();
            
            if (doc.RootElement.TryGetProperty("value", out JsonElement valueArray))
            {
                foreach (JsonElement item in valueArray.EnumerateArray())
                {
                    folders.Add(new CloudItem
                    {
                        Name = item.GetProperty("name").GetString(),
                        CloudPath = item.GetProperty("parentReference").GetProperty("path").GetString().Replace("/drive/root:", ""),
                        IsFolder = true,
                        Size = 0,
                        LastModified = item.GetProperty("lastModifiedDateTime").GetDateTime(),
                        Provider = ProviderName
                    });
                }
            }
            
            return folders;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP error listing OneDrive folders: {ex.Message}");
            return Enumerable.Empty<CloudItem>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error listing OneDrive folders: {ex.Message}");
            return Enumerable.Empty<CloudItem>();
        }
    }

    public async Task<IEnumerable<CloudItem>> ListFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Provider not connected. Call ConnectAsync first.");

        try
        {
            // Normalize path for Graph API
            string graphPath = NormalizePathForGraph(path);
            string requestUrl = $"{GraphEndpoint}/me/drive/root:/{graphPath}:/children";
            
            // Add filter for files only
            requestUrl += "?$filter=file ne null";
            
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument doc = JsonDocument.Parse(json);
            
            var files = new List<CloudItem>();
            
            if (doc.RootElement.TryGetProperty("value", out JsonElement valueArray))
            {
                foreach (JsonElement item in valueArray.EnumerateArray())
                {
                    files.Add(new CloudItem
                    {
                        Name = item.GetProperty("name").GetString(),
                        CloudPath = item.GetProperty("parentReference").GetProperty("path").GetString().Replace("/drive/root:", ""),
                        IsFolder = false,
                        Size = item.GetProperty("size").GetInt64(),
                        LastModified = item.GetProperty("lastModifiedDateTime").GetDateTime(),
                        Provider = ProviderName
                    });
                }
            }
            
            return files;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP error listing OneDrive files: {ex.Message}");
            return Enumerable.Empty<CloudItem>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error listing OneDrive files: {ex.Message}");
            return Enumerable.Empty<CloudItem>();
        }
    }

    public async Task DownloadAsync(string cloudPath, string localPath, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Provider not connected. Call ConnectAsync first.");

        try
        {
            // Normalize path for Graph API
            string graphPath = NormalizePathForGraph(cloudPath);
            string requestUrl = $"{GraphEndpoint}/me/drive/root:/{graphPath}:/content";
            
            // Create local directory if needed
            string localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }
            
            // Get the file to determine size for progress
            var fileInfoResponse = await _httpClient.GetAsync($"{GraphEndpoint}/me/drive/root:/{graphPath}", cancellationToken);
            fileInfoResponse.EnsureSuccessStatusCode();
            var fileInfoJson = await fileInfoResponse.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument fileInfoDoc = JsonDocument.Parse(fileInfoJson);
            long totalSize = fileInfoDoc.RootElement.GetProperty("size").GetInt64();
            
            // Download the file
            using var downloadResponse = await _httpClient.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            downloadResponse.EnsureSuccessStatusCode();
            
            long totalBytesRead = 0;
            long readBytes = 0;
            byte[] buffer = new byte[81920]; // 80KB buffer
            
            await using var contentStream = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            
            while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, readBytes, cancellationToken);
                totalBytesRead += readBytes;
                
                int progressPercent = (int)((double)totalBytesRead / totalSize * 100);
                progress?.Report(progressPercent);
            }
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP error downloading OneDrive file: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error downloading OneDrive file: {ex.Message}");
            throw;
        }
    }

    public void Disconnect()
    {
        // Clear token cache if needed
        _isConnected = false;
        _accessToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    private string NormalizePathForGraph(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            return string.Empty;
        
        // Remove leading slash if present
        if (path.StartsWith("/"))
            path = path.Substring(1);
            
        // Replace backslashes with forward slashes
        return path.Replace("\\", "/");
    }
}