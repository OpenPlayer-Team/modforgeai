using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinUIMvvmApp.ViewModels;
using WinUIMvvmApp.Plugins;
using WinUIMvvmApp.Cloud;

namespace WinUIMvvmApp.Views;

/// <summary>
/// Finestra principale dell'applicazione
/// Gestisce la visualizzazione a schede con supporto split, cloud e plugin
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// ViewModel principale
    /// </summary>
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        
        // Recupera il ViewModel dal locator
        ViewModel = ((App)Application.Current).ViewModelLocator.MainViewModel;
        DataContext = ViewModel;

        // Aggiorna menu plugin quando i comandi cambiano
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.PluginCommands))
            {
                UpdatePluginsMenu();
            }
        };

        System.Diagnostics.Debug.WriteLine("MainWindow inizializzata");
    }

    /// <summary>
    /// Gestore per il pulsante "+" (AddTabButton)
    /// </summary>
    private void MainTabView_AddTabButtonClick(Controls.TabView sender, Controls.TabViewTabButtonClickEventArgs args)
    {
        ViewModel.AddNewTabCommand.Execute("C:\\");
        System.Diagnostics.Debug.WriteLine("Nuova scheda richiesta");
    }

    /// <summary>
    /// Gestore per la richiesta di chiusura scheda
    /// </summary>
    private void MainTabView_TabCloseRequested(Controls.TabView sender, Controls.TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is ViewModels.TabViewModel tab)
        {
            ViewModel.CloseTabCommand.Execute(tab);
            System.Diagnostics.Debug.WriteLine($"Chiusura scheda richiesta: {tab.Title}");
        }
    }

    /// <summary>
    /// Gestore menu Esci
    /// </summary>
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Uscita applicazione");
        Application.Current.Exit();
    }

    #region Cloud Menu Handlers

    /// <summary>
    /// Gestore click su provider cloud nel menu
    /// </summary>
    private async void CloudProvider_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string providerKey)
            return;

        try
        {
            var factory = CloudServiceFactory.Instance;
            var provider = factory.GetProvider(providerKey);

            if (provider == null)
            {
                System.Diagnostics.Debug.WriteLine($"Provider non trovato: {providerKey}");
                return;
            }

            // Mostra stato connessione
            menuItem.IsEnabled = false;
            menuItem.Text = $"Connessione a {provider.ProviderName}...";

            // Connette al provider
            var connected = await factory.ConnectProviderAsync(providerKey).ConfigureAwait(true);

            if (connected)
            {
                System.Diagnostics.Debug.WriteLine($"Connesso a {provider.ProviderName}");
                
                // Apre una nuova scheda per il cloud
                await OpenCloudTabAsync(provider).ConfigureAwait(true);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Connessione fallita per {providerKey}");
                await ShowErrorDialogAsync("Connessione Cloud", $"Impossibile connettersi a {provider.ProviderName}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore connessione cloud: {ex}");
            await ShowErrorDialogAsync("Errore Cloud", $"Errore durante la connessione: {ex.Message}");
        }
        finally
        {
            // Ripristina stato menu
            menuItem.IsEnabled = true;
            menuItem.Text = menuItem.Tag?.ToString()?.Replace("Mock", "") ?? "Cloud";
        }
    }

    /// <summary>
    /// Apre una nuova scheda per visualizzare il contenuto cloud
    /// </summary>
    private async Task OpenCloudTabAsync(ICloudProvider provider)
    {
        // Crea una scheda speciale per il cloud
        var cloudTab = new TabViewModel($"C:\\Cloud\\{provider.ProviderName}");
        cloudTab.Title = $"☁️ {provider.ProviderName}";

        // Aggiunge la scheda
        ViewModel.Tabs.Add(cloudTab);
        ViewModel.SelectedTab = cloudTab;

        System.Diagnostics.Debug.WriteLine($"Scheda cloud aperta: {provider.ProviderName}");

        // Carica la root del cloud in background
        _ = LoadCloudRootAsync(provider, cloudTab);
    }

    /// <summary>
    /// Carica la directory radice del provider cloud
    /// </summary>
    private async Task LoadCloudRootAsync(ICloudProvider provider, TabViewModel tab)
    {
        try
        {
            tab.IsLoading = true;
            tab.StatusMessage = $"Caricamento {provider.ProviderName}...";

            // Cancella elementi correnti
            tab.Items.Clear();

            // Aggiunge elemento speciale per tornare alla root locale
            tab.Items.Add(new FileItem
            {
                Name = "← File Locali",
                FullPath = "C:\\",
                IsDirectory = true,
                Size = 0,
                LastModified = DateTime.Now,
                Icon = "🏠"
            });

            // Carica cartelle cloud
            var folders = await provider.ListFoldersAsync("/root").ConfigureAwait(true);
            foreach (var folder in folders)
            {
                tab.Items.Add(new FileItem
                {
                    Name = $"📁 {folder.Name}",
                    FullPath = folder.CloudPath,
                    IsDirectory = true,
                    Size = 0,
                    LastModified = folder.LastModified,
                    Icon = "☁️"
                });
            }

            // Carica file cloud
            var files = await provider.ListFilesAsync("/root").ConfigureAwait(true);
            foreach (var file in files)
            {
                tab.Items.Add(new FileItem
                {
                    Name = file.Name,
                    FullPath = file.CloudPath,
                    IsDirectory = false,
                    Size = file.Size,
                    LastModified = file.LastModified,
                    Icon = "☁️"
                });
            }

            tab.StatusMessage = $"Connesso a {provider.ProviderName} - {tab.Items.Count - 1} elementi";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore caricamento cloud: {ex}");
            tab.StatusMessage = $"Errore: {ex.Message}";
        }
        finally
        {
            tab.IsLoading = false;
        }
    }

    /// <summary>
    /// Aggiorna la connessione cloud
    /// </summary>
    private async void RefreshCloud_Click(object sender, RoutedEventArgs e)
    {
        var factory = CloudServiceFactory.Instance;
        var connected = factory.GetConnectedProviders();

        if (connected.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("Nessun provider cloud connesso");
            await ShowInfoDialogAsync("Cloud", "Nessun provider cloud connesso");
            return;
        }

        foreach (var provider in connected.Values)
        {
            System.Diagnostics.Debug.WriteLine($"Aggiornamento {provider.ProviderName}...");
        }

        await ShowInfoDialogAsync("Cloud", "Aggiornamento completato");
    }

    /// <summary>
    /// Disconnette tutti i provider cloud
    /// </summary>
    private void DisconnectCloud_Click(object sender, RoutedEventArgs e)
    {
        var factory = CloudServiceFactory.Instance;
        factory.DisconnectAll();
        System.Diagnostics.Debug.WriteLine("Tutti i provider cloud disconnessi");
    }

    #endregion

    #region Plugin Menu Handlers

    /// <summary>
    /// Aggiorna il menu dei plugin
    /// </summary>
    private void UpdatePluginsMenu()
    {
        PluginsSubMenu.Items.Clear();

        var commands = ViewModel.PluginCommands.ToList();
        
        if (commands.Count == 0)
        {
            PluginsSubMenu.Items.Add(NoPluginsItem);
            return;
        }

        // Raggruppa per plugin
        var grouped = commands.GroupBy(c => c.Owner?.Name ?? "Sconosciuto");
        
        foreach (var group in grouped)
        {
            if (group.Key != "Sconosciuto" && group.Count() > 1)
            {
                // Crea sottomenu per plugin con più comandi
                var subItem = new MenuFlyoutSubItem
                {
                    Text = group.Key,
                    Icon = new SymbolIcon(Symbol.Puzzle)
                };

                foreach (var cmd in group)
                {
                    subItem.Items.Add(CreatePluginMenuItem(cmd));
                }

                PluginsSubMenu.Items.Add(subItem);
            }
            else
            {
                // Comando singolo
                foreach (var cmd in group)
                {
                    PluginsSubMenu.Items.Add(CreatePluginMenuItem(cmd));
                }
            }
        }
    }

    /// <summary>
    /// Crea un elemento di menu per un comando plugin
    /// </summary>
    private MenuFlyoutItem CreatePluginMenuItem(PluginCommand cmd)
    {
        var item = new MenuFlyoutItem
        {
            Text = cmd.Name,
            Icon = new SymbolIcon(GetSymbol(cmd.Icon)),
            IsEnabled = cmd.IsEnabled,
            ToolTipService.ToolTip = cmd.Description
        };

        item.Click += async (s, e) =>
        {
            try
            {
                await cmd.ExecuteAsync?.Invoke()!;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore esecuzione comando plugin: {ex}");
                await ShowErrorDialogAsync("Errore Plugin", $"Errore durante l'esecuzione del comando: {ex.Message}");
            }
        };

        return item;
    }

    /// <summary>
    /// Converte stringa icona in Symbol
    /// </summary>
    private Symbol GetSymbol(string icon)
    {
        return icon switch
        {
            "📊" => Symbol.BarChart,
            "📈" => Symbol.TrendUp,
            "🧹" => Symbol.Scan,
            "⚙️" => Symbol.Setting,
            "🔍" => Symbol.Find,
            _ => Symbol.Puzzle
        };
    }

    /// <summary>
    /// Ricarica tutti i plugin
    /// </summary>
    private async void ReloadPlugins_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.ReloadPluginsAsync();
            await ShowInfoDialogAsync("Plugin", "Plugin ricaricati con successo");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore ricaricamento plugin: {ex}");
            await ShowErrorDialogAsync("Errore", $"Impossibile ricaricare i plugin: {ex.Message}");
        }
    }

    /// <summary>
    /// Mostra informazioni sui plugin caricati
    /// </summary>
    private async void ShowPluginsInfo_Click(object sender, RoutedEventArgs e)
    {
        var results = ViewModel.PluginLoadResults;
        
        if (results.Count == 0)
        {
            await ShowInfoDialogAsync("Plugin", "Nessun plugin caricato");
            return;
        }

        var message = "Stato caricamento plugin:\n\n";
        
        foreach (var result in results)
        {
            var status = result.Success ? "✅" : "❌";
            message += $"{status} {result.PluginName}\n";
            
            if (!result.Success)
            {
                message += $"   Errore: {result.ErrorMessage}\n";
            }
            else if (result.Plugin != null)
            {
                message += $"   Versione: {result.Plugin.Version}\n";
                message += $"   Comandi: {result.Commands.Count}\n";
            }
            
            message += "\n";
        }

        await ShowInfoDialogAsync("Plugin", message);
    }

    #endregion

    #region Dialog Helpers

    /// <summary>
    /// Mostra dialog di errore
    /// </summary>
    private async Task ShowErrorDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore mostrare dialog: {ex}");
        }
    }

    /// <summary>
    /// Mostra dialog informativo
    /// </summary>
    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore mostrare dialog: {ex}");
        }
    }

    #endregion

    /// <summary>
    /// Navigazione alla finestra principale
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        System.Diagnostics.Debug.WriteLine("MainWindow attiva");
    }
}


    /// <summary>
    /// Gestore per il pulsante "+" (AddTabButton)
    /// </summary>
    private void MainTabView_AddTabButtonClick(Controls.TabView sender, Controls.TabViewTabButtonClickEventArgs args)
    {
        ViewModel.AddNewTabCommand.Execute("C:\\");
        System.Diagnostics.Debug.WriteLine("Nuova scheda richiesta");
    }

    /// <summary>
    /// Gestore per la richiesta di chiusura scheda
    /// </summary>
    private void MainTabView_TabCloseRequested(Controls.TabView sender, Controls.TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is ViewModels.TabViewModel tab)
        {
            ViewModel.CloseTabCommand.Execute(tab);
            System.Diagnostics.Debug.WriteLine($"Chiusura scheda richiesta: {tab.Title}");
        }
    }

    /// <summary>
    /// Gestore menu Esci
    /// </summary>
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Uscita applicazione");
        Application.Current.Exit();
    }

    #region Cloud Menu Handlers

    /// <summary>
    /// Gestore click su provider cloud nel menu
    /// </summary>
    private async void CloudProvider_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string providerKey)
            return;

        try
        {
            var factory = CloudServiceFactory.Instance;
            var provider = factory.GetProvider(providerKey);

            if (provider == null)
            {
                System.Diagnostics.Debug.WriteLine($"Provider non trovato: {providerKey}");
                return;
            }

            // Mostra stato connessione
            menuItem.IsEnabled = false;
            menuItem.Text = $"Connessione a {provider.ProviderName}...";

            // Connette al provider
            var connected = await factory.ConnectProviderAsync(providerKey).ConfigureAwait(true);

            if (connected)
            {
                System.Diagnostics.Debug.WriteLine($"Connesso a {provider.ProviderName}");
                
                // Apre una nuova scheda per il cloud
                await OpenCloudTabAsync(provider).ConfigureAwait(true);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Connessione fallita per {providerKey}");
                // TODO: Mostrare messaggio di errore all'utente
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore connessione cloud: {ex.Message}");
            // TODO: Gestione errore utente
        }
        finally
        {
            // Ripristina stato menu
            menuItem.IsEnabled = true;
            menuItem.Text = menuItem.Tag?.ToString()?.Replace("Mock", "") ?? "Cloud";
        }
    }

    /// <summary>
    /// Apre una nuova scheda per visualizzare il contenuto cloud
    /// </summary>
    private async Task OpenCloudTabAsync(ICloudProvider provider)
    {
        // Crea una scheda speciale per il cloud
        var cloudTab = new TabViewModel($"C:\\Cloud\\{provider.ProviderName}");
        cloudTab.Title = $"☁️ {provider.ProviderName}";

        // Aggiunge la scheda
        ViewModel.Tabs.Add(cloudTab);
        ViewModel.SelectedTab = cloudTab;

        System.Diagnostics.Debug.WriteLine($"Scheda cloud aperta: {provider.ProviderName}");

        // Carica la root del cloud in background
        _ = LoadCloudRootAsync(provider, cloudTab);
    }

    /// <summary>
    /// Carica la directory radice del provider cloud
    /// </summary>
    private async Task LoadCloudRootAsync(ICloudProvider provider, TabViewModel tab)
    {
        try
        {
            tab.IsLoading = true;
            tab.StatusMessage = $"Caricamento {provider.ProviderName}...";

            // Cancella elementi correnti
            tab.Items.Clear();

            // Aggiunge elemento speciale per tornare alla root locale
            tab.Items.Add(new FileItem
            {
                Name = "← File Locali",
                FullPath = "C:\\",
                IsDirectory = true,
                Size = 0,
                LastModified = DateTime.Now,
                Icon = "🏠"
            });

            // Carica cartelle cloud
            var folders = await provider.ListFoldersAsync("/root").ConfigureAwait(true);
            foreach (var folder in folders)
            {
                tab.Items.Add(new FileItem
                {
                    Name = $"📁 {folder.Name}",
                    FullPath = folder.CloudPath,
                    IsDirectory = true,
                    Size = 0,
                    LastModified = folder.LastModified,
                    Icon = "☁️"
                });
            }

            // Carica file cloud
            var files = await provider.ListFilesAsync("/root").ConfigureAwait(true);
            foreach (var file in files)
            {
                tab.Items.Add(new FileItem
                {
                    Name = file.Name,
                    FullPath = file.CloudPath,
                    IsDirectory = false,
                    Size = file.Size,
                    LastModified = file.LastModified,
                    Icon = "☁️"
                });
            }

            tab.StatusMessage = $"Connesso a {provider.ProviderName} - {tab.Items.Count - 1} elementi";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Errore caricamento cloud: {ex.Message}");
            tab.StatusMessage = $"Errore: {ex.Message}";
        }
        finally
        {
            tab.IsLoading = false;
        }
    }

    /// <summary>
    /// Aggiorna la connessione cloud
    /// </summary>
    private async void RefreshCloud_Click(object sender, RoutedEventArgs e)
    {
        var factory = CloudServiceFactory.Instance;
        var connected = factory.GetConnectedProviders();

        if (connected.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("Nessun provider cloud connesso");
            return;
        }

        foreach (var provider in connected.Values)
        {
            System.Diagnostics.Debug.WriteLine($"Aggiornamento {provider.ProviderName}...");
            // TODO: Implementare refresh dati
        }
    }

    /// <summary>
    /// Disconnette tutti i provider cloud
    /// </summary>
    private void DisconnectCloud_Click(object sender, RoutedEventArgs e)
    {
        var factory = CloudServiceFactory.Instance;
        factory.DisconnectAll();
        System.Diagnostics.Debug.WriteLine("Tutti i provider cloud disconnessi");
    }

    #endregion

    /// <summary>
    /// Navigazione alla finestra principale
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        System.Diagnostics.Debug.WriteLine("MainWindow attiva");
    }
}


    /// <summary>
    /// Gestore per il pulsante "+" (AddTabButton)
    /// </summary>
    private void MainTabView_AddTabButtonClick(Controls.TabView sender, Controls.TabViewTabButtonClickEventArgs args)
    {
        ViewModel.AddNewTabCommand.Execute("C:\\");
        System.Diagnostics.Debug.WriteLine("Nuova scheda richiesta");
    }

    /// <summary>
    /// Gestore per la richiesta di chiusura scheda
    /// </summary>
    private void MainTabView_TabCloseRequested(Controls.TabView sender, Controls.TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is ViewModels.TabViewModel tab)
        {
            ViewModel.CloseTabCommand.Execute(tab);
            System.Diagnostics.Debug.WriteLine($"Chiusura scheda richiesta: {tab.Title}");
        }
    }

    /// <summary>
    /// Gestore menu Esci
    /// </summary>
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Uscita applicazione");
        Application.Current.Exit();
    }

    /// <summary>
    /// Navigazione alla finestra principale
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        System.Diagnostics.Debug.WriteLine("MainWindow attiva");
    }
}
