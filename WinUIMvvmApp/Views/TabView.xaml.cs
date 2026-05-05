using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinUIMvvmApp.ViewModels;

namespace WinUIMvvmApp.Views;

/// <summary>
/// Pagina per la visualizzazione e navigazione del file system
/// Supporta sia la modalità singolo pannello che split
/// </summary>
public sealed partial class TabView : Page
{
    /// <summary>
    /// ViewModel associato (può essere TabViewModel o SplitPaneViewModel)
    /// </summary>
    public object? ViewModel { get; private set; }

    public TabView()
    {
        InitializeComponent();
        
        // Il DataContext viene impostato dal TabContentTemplate in MainWindow.xaml
        // tramite binding a LeftPane o RightPane (SplitPaneViewModel) 
        // oppure al TabViewModel stesso per la modalità singolo pannello
        Loaded += TabView_Loaded;
    }

    private void TabView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel = DataContext;
        
        if (ViewModel is SplitPaneViewModel splitPane)
        {
            System.Diagnostics.Debug.WriteLine($"TabView caricato per pannello: {splitPane.PaneTitle}");
        }
        else if (ViewModel is TabViewModel tabVm)
        {
            System.Diagnostics.Debug.WriteLine($"TabView caricato per scheda: {tabVm.Title}");
        }
    }

    /// <summary>
    /// Gestore del click sugli elementi della lista
    /// </summary>
    private void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Models.FileItem fileItem && fileItem.IsDirectory)
        {
            // Determina quale ViewModel è attivo e naviga
            if (ViewModel is TabViewModel tabVm)
            {
                _ = tabVm.NavigateCommand.ExecuteAsync(fileItem);
            }
            else if (ViewModel is SplitPaneViewModel splitVm)
            {
                _ = splitVm.NavigateCommand.ExecuteAsync(fileItem);
            }
        }
    }

    /// <summary>
    /// Navigazione alla pagina - override per logging/debug
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        System.Diagnostics.Debug.WriteLine($"Navigato a TabView");
    }

    /// <summary>
    /// Navigazione dalla pagina - override per cleanup
    /// </summary>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        System.Diagnostics.Debug.WriteLine($"Navigato da TabView");
    }
}
