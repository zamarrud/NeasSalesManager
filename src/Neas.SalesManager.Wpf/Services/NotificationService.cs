using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace Neas.SalesManager.Wpf.Services;

public class NotificationService : INotificationService
{
    private readonly HubConnection _hubConnection;

    public event Action<int>? OnDistrictUpdated;

    public NotificationService()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/hubs/salesmanager")
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) })
            .Build();

        _hubConnection.On<int>("DistrictUpdated", (districtId) =>
        {
            System.Diagnostics.Debug.WriteLine($"[SignalR] Received DistrictUpdated for ID: {districtId}");
            OnDistrictUpdated?.Invoke(districtId);
        });
    }

    public async Task StartAsync()
    {
        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            try
            {
                await _hubConnection.StartAsync();
                System.Diagnostics.Debug.WriteLine($"[SignalR] Connected successfully. State: {_hubConnection.State}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR Connection Error]: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
    }
}