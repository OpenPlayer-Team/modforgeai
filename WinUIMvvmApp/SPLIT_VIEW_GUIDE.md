# 📄 Guida alle Funzionalità Split View

## Panoramica
La funzionalità Split View consente di dividere ogni scheda in due pannelli indipendenti per la navigazione file system, con opzione di sincronizzazione.

## Architettura

### TabViewModel
Gestisce la modalità split e coordina i due pannelli:
- `IsSplitMode`: Attiva/disattiva lo split
- `IsVerticalSplit`: Orientamento (true = colonne, false = righe)
- `IsSyncEnabled`: Sincronizzazione automatica tra pannelli
- `LeftPane` / `RightPane`: Istanze di `SplitPaneViewModel`

### SplitPaneViewModel
Rappresenta un singolo pannello nello split:
- Proprietà indipendenti per navigazione
- Stack di navigazione separato
- Metodo `SyncWithAsync()` per sincronizzazione

## Flusso di Sincronizzazione

```
1. Utente naviga nel Pannello Sinistro
   ↓
2. Se IsSyncEnabled = true
   ↓
3. RightPane.SyncWithAsync(LeftPane)
   ↓
4. RightPane naviga allo stesso percorso
   ↓
5. Entrambi i pannelli mostrano la stessa directory
```

## Componenti XAML

### GridSplitter
```xml
<GridSplitter Background="{ThemeResource ...}"
              HorizontalAlignment="Stretch"
              VerticalAlignment="Stretch"
              ShowsPreview="True" />
```
- Permette ridimensionamento dinamico
- Mostra preview durante il trascinamento

### x:Bind
Tutti i binding usano `x:Bind` per:
- Type safety
- Performance migliori
- Compilazione verificata

## Scorciatoie da Tastiera

| Comando | Scorciatoia |
|---------|-------------|
| Nuova Scheda | Ctrl+T |
| Chiudi Scheda | Ctrl+W |
| Attiva Split | Ctrl+S |
| Cambia Orientamento | Ctrl+O |
| Sync Navigation | Checkbox |
| Indietro | Ctrl+FrecciaSinistra |
| Avanti | Ctrl+FrecciaDestra |
| Aggiorna | F5 |

## Best Practices

1. **Virtualizzazione**: Usare `VirtualizationMode="Recycling"` per liste grandi
2. **Cancellazione**: Annullare operazioni asincrone quando non più necessarie
3. **Thread Safety**: Usare `SemaphoreSlim` per operazioni concorrenti
4. **Memory**: Chiamare `Cleanup()` quando si chiude una scheda
5. **Binding**: Preferire `x:Bind` a `Binding` per performance

## Debug

### Log Utili
```csharp
System.Diagnostics.Debug.WriteLine($"Navigato a: {path}");
System.Diagnostics.Debug.WriteLine($"Split mode: {IsSplitMode}");
System.Diagnostics.Debug.WriteLine($"Sync enabled: {IsSyncEnabled}");
```

### Punti di Interruzione Consigliati
- `TabViewModel.NavigateToPathAsync()`
- `SplitPaneViewModel.SyncWithAsync()`
- `FileSystemService.EnumerateAsync()`

## Estensioni Future

- [ ] Drag & drop file tra pannelli
- [ ] Split a 4 pannelli
- [ ] Confronto directory
- [ ] Sincronizzazione bidirezionale
- [ ] Salvataggio layout
- [ ] Profili di navigazione
