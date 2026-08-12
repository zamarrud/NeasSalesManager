using System;
using System.Threading.Tasks;

namespace Neas.SalesManager.Wpf.Services;

public interface INotificationService : IAsyncDisposable
{
    event Action<int>? OnDistrictUpdated;
    Task StartAsync();
}