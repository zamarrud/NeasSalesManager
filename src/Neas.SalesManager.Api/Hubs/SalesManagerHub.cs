using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Neas.SalesManager.Api.Hubs;

public class SalesManagerHub : Hub
{
    // Clients can listen for "DistrictUpdated" events
    public async Task BroadcastDistrictUpdate(int districtId)
    {
        await Clients.Others.SendAsync("DistrictUpdated", districtId);
    }
}