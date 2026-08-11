using Neas.SalesManager.Wpf.Models;

namespace Neas.SalesManager.Wpf.Services;

public interface ISalesApiClient
{
    Task CreateDistrictAsync(string name, int primarySalespersonId);
    Task<List<DistrictSummary>> GetDistrictsAsync();
    Task<DistrictDetails?> GetDistrictDetailsAsync(int districtId);
    Task<List<Salesperson>> GetAllSalespersonsAsync();
    Task AssignSalespersonAsync(int districtId, int salespersonId, bool isPrimary);
    Task RemoveSalespersonAsync(int districtId, int salespersonId);
}