// Esempi di utilizzo della funzionalità Split View

// 1. Attivare lo split in una scheda
var tab = new TabViewModel("C:\\Users");
tab.ToggleSplitModeCommand.Execute(null);
// Ora tab.IsSplitMode = true
// tab.LeftPane e tab.RightPane sono creati

// 2. Cambiare orientamento
tab.ToggleSplitOrientationCommand.Execute(null);
// Alterna tra verticale e orizzontale

// 3. Abilitare sincronizzazione
tab.IsSyncEnabled = true;
// I pannelli si sincronizzano automaticamente

// 4. Sincronizzare manualmente
tab.SyncPanesCommand.Execute(null);
// RightPane copia il percorso da LeftPane

// 5. Navigare in modalità split
tab.LeftPane.NavigateCommand.ExecuteAsync(fileItem);
// Solo il pannello sinistro naviga
// Se IsSyncEnabled=true, anche il destro segue

// 6. Pulizia risorse
tab.Cleanup();
// Libera tutti i pannelli e token

// 7. Disattivare split
tab.ToggleSplitModeCommand.Execute(null);
// IsSplitMode = false
// LeftPane e RightPane = null

// 8. Uso con x:Bind in XAML
// <Button Command="{x:Bind ViewModel.ToggleSplitModeCommand}" />
// <CheckBox IsChecked="{x:Bind ViewModel.IsSyncEnabled, Mode=TwoWay}" />
// <TextBlock Text="{x:Bind ViewModel.LeftPane.CurrentPath, Mode=OneWay}" />
