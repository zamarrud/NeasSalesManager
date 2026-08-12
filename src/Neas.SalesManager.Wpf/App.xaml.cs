using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Neas.SalesManager.Wpf.Services;
using Neas.SalesManager.Wpf.ViewModels;

namespace Neas.SalesManager.Wpf;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        services.AddHttpClient<ISalesApiClient, SalesApiClient>();
        
        services.AddHttpClient<ISalesApiClient, SalesApiClient>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:5000/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddTransient<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }
}