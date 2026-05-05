using Microsoft.UI.Xaml;
using WinUIMvvmApp.ViewModels;

namespace WinUIMvvmApp;

/// <summary>
/// Entry point dell'applicazione WinUI 3
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Abilita XAML Islands e WinUI 3
        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);

            // Crea e avvia l'applicazione
            _ = new App();
        });
    }
}
