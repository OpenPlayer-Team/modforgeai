using System;

namespace WinUIMvvmApp.Models;

/// <summary>
/// Rappresenta un elemento del file system (file o cartella)
/// </summary>
public class FileItem
{
    /// <summary>
    /// Nome del file o cartella
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Percorso completo
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// Indica se è una cartella
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// Dimensione in byte (solo per file)
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Data di ultima modifica
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Icona associata (può essere usata per binding)
    /// </summary>
    public string Icon => IsDirectory ? "📁" : "📄";

    public override string ToString() => Name;
}
