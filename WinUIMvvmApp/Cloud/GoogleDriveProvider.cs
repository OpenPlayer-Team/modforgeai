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
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using WinUIMvvmApp.Models;

namespace WinUIMvvmApp.Cloud;

/// <summary>
/// Real implementation of Google Drive provider using Google Drive API
/// </summary>
public class GoogleDriveProvider : ICloudProvider
{
    private const string ApplicationName = "WinUI 3 File Explorer";
    private const string[] Scopes = { DriveService.Scope.DriveReadwrite };
    
    private string _clientId = ""; // TODO: Set your Google Cloud client ID
    private string _clientSecret = ""; // TODO: Set your Google Cloud client secret
    
    private DriveService _driveService;
    private bool _isConnected;
    private UserCredential _credential;

    public string ProviderName => "GoogleDrive";
    public bool IsConnected => _isConnected;

    public GoogleDriveProvider()
    {
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: In a real app, you would load existing credentials from secure storage
            // and only go through the auth flow if needed or expired
            
            // For demonstration, we'll use the installed app flow
            // In production, consider using service accounts or stored refresh tokens
            
            // Create the credential using installed app flow
            // Note: This will pop up a browser window for user consent
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
                $"{{\"installed\":{{\"client_id\":\"{_clientId}\",\"client_secret\":\"{_clientSecret}\",\"auth_uri\":\"https://accounts.google.com/o/oauth2/auth\",\"token_uri\":\"https://oauth2.googleapis.com/token\"}}}}"));
            
            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None);
            
            // Create the Drive service
            _driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = ApplicationName,
            });
            
            _isConnected = true;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error connecting to Google Drive: {ex.Message}");
            return false;
        }
    }

    public async Task<IEnumerable<CloudItem>> ListFoldersAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Provider not connected. Call ConnectAsync first.");

        try
        {
            // Normalize the path for Google Drive API
            string normalizedPath = NormalizePath(path);
            
            // Get the folder ID for the path
            string folderId = await GetFolderIdAsync(normalizedPath, cancellationToken);
            if (folderId == "root")
            {
                folderId = "root";
            }
            
            // List folders in the specified folder
            var request = _driveService.Files.List();
            request.Q = $"'{folderId}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id, name, modifiedTime)";
            request.Spaces = "drive";
            
            var result = await request.ExecuteAsync(cancellationToken);
            
            var folders = new List<CloudItem>();
            
            foreach (var file in result.Files)
            {
                folders.Add(new CloudItem
                {
                    Name = file.Name,
                    CloudPath = $"{normalizedPath}/{file.Name}".TrimStart('/'),
                    IsFolder = true,
                    Size = 0,
                    LastModified = file.ModifiedTime.GetValueOrDefault(),
                    Provider = ProviderName
                });
            }
            
            return folders;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error listing Google Drive folders: {ex.Message}");
            return Enumerable.Empty<CloudItem>();
        }
    }

    public async Task<IEnumerable<CloudItem>> ListFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Provider not connected. Call ConnectAsync first.");

        try
        {
            // Normalize the path for Google Drive API
            string normalizedPath = NormalizePath(path);
            
            // Get the folder ID for the path
            string folderId = await GetFolderIdAsync(normalizedPath, cancellationToken);
            if (folderId == "root")
            {
                folderId = "root";
            }
            
            // List files in the specified folder
            var request = _driveService.Files.List();
            request.Q = $"'{folderId}' in parents and mimeType != 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id, name, size, modifiedTime)";
            request.Spaces = "drive";
            
            var result = await request.ExecuteAsync(cancellationToken);
            
            var files = new List<CloudItem>();
            
            foreach (var file in result.Files)
            {
                files.Add(new CloudItem
                {
                    Name = file.Name,
                    CloudPath = $"{normalizedPath}/{file.Name}".TrimStart('/'),
                    IsFolder = false,
                    Size = file.Size ?? 0,
                    LastModified = file.ModifiedTime.GetValueOrDefault(),
                    Provider = ProviderName
                });
            }
            
            return files;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error listing Google Drive files: {ex.Message}");
            return Enumerable.Empty<CloudItem>();
        }
    }

    public async Task DownloadAsync(string cloudPath, string localPath, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Provider not connected. Call ConnectAsync first.");

        try
        {
            // Normalize the path for Google Drive API
            string normalizedPath = NormalizePath(cloudPath);
            
            // Get the file ID for the path
            string fileId = await GetFileIdAsync(normalizedPath, cancellationToken);
            
            // Get the file metadata to determine size
            var fileRequest = _driveService.Files.Get(fileId);
            fileRequest.Fields = "size, name";
            var fileMetadata = await fileRequest.ExecuteAsync(cancellationToken);
            
            // Create local directory if needed
            string localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }
            
            // Download the file
            var exportRequest = _driveService.Files.Get(fileId);
            exportRequest.Fields = "*";
            
            // Get the file stream
            using var stream = new MemoryStream();
            await exportRequest.DownloadAsync(stream);
            
            // Create local directory if needed
            string localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }
            
            // Write to file
            await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Seek(0, SeekOrigin.Begin);
            await stream.CopyToAsync(fileStream, cancellationToken);
            
            // Report 100% progress at the end
            progress?.Report(100);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error downloading Google Drive file: {ex.Message}");
            throw;
        }
    }

    public void Disconnect()
    {
        // In a real app, you might want to revoke the token or clear stored credentials
        _isConnected = false;
        _driveService = null;
        _credential = null;
    }

    private async Task<string> GetFolderIdAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            return "root";
        
        // Split the path into components
        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        string parentId = "root";
        
        foreach (var part in parts)
        {
            // Search for the folder in the parent
            var request = _driveService.Files.List();
            request.Q = $"'{parentId}' in parents and name = '{part}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id)";
            
            var result = await request.ExecuteAsync(cancellationToken);
            
            if (result.Files != null && result.Files.Any())
            {
                parentId = result.Files.First().Id;
            }
            else
            {
                // Folder not found, return the last known parent
                return parentId;
            }
        }
        
        return parentId;
    }

    private async Task<string> GetFileIdAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            throw new ArgumentException("Path cannot be root for file operations");
        
        // Split the path into components
        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        string parentId = "root";
        string fileName = parts.Last();
        
        // Get the parent folder ID
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            var request = _driveService.Files.List();
            request.Q = $"'{parentId}' in parents and name = '{part}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id)";
            
            var result = await request.ExecuteAsync(cancellationToken);
            
            if (result.Files != null && result.Files.Any())
            {
                parentId = result.Files.First().Id;
            }
            else
            {
                // Folder not found
                throw new FileNotFoundException($"Folder '{part}' not found in path '{path}'");
            }
        }
        
        // Now search for the file in the parent folder
        var request = _driveService.Files.List();
        request.Q = $"'{parentId}' in parents and name = '{fileName}' and trashed = false";
        request.Fields = "files(id)";
        
        var result = await request.ExecuteAsync(cancellationToken);
        
        if (result.Files != null && result.Files.Any())
        {
            return result.Files.First().Id;
        }
        else
        {
            throw new FileNotFoundException($"File '{fileName}' not found in path '{path}'");
        }
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";
        
        // Ensure path starts with /
        if (!path.StartsWith("/"))
            path = "/" + path;
        
        // Replace backslashes with forward slashes
        return path.Replace("\\", "/");
    }
}