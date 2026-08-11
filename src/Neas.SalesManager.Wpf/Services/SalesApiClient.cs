using System.Net.Http;
using System.Net.Http.Json;
using Neas.SalesManager.Wpf.Models;

namespace Neas.SalesManager.Wpf.Services;

public class SalesApiClient : ISalesApiClient
{
    private readonly HttpClient _httpClient;

    public SalesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5000/");
    }

    public async Task<List<DistrictSummary>> GetDistrictsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<DistrictSummary>>("api/districts") ?? new();
    }

    public async Task<DistrictDetails?> GetDistrictDetailsAsync(int districtId)
    {
        return await _httpClient.GetFromJsonAsync<DistrictDetails>($"api/districts/{districtId}");
    }
    
    public async Task<List<Salesperson>> GetAllSalespersonsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<Salesperson>>("api/districts/salespersons") ?? new();
    }

    public async Task AssignSalespersonAsync(int districtId, int salespersonId, bool isPrimary)
    {
        var request = new AssignSalespersonRequest(salespersonId, isPrimary);
        var response = await _httpClient.PutAsJsonAsync($"api/districts/{districtId}/salespersons", request);

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API Error ({response.StatusCode}): {errorJson}");
        }
    }

    public async Task RemoveSalespersonAsync(int districtId, int salespersonId)
    {
        var response = await _httpClient.DeleteAsync($"api/districts/{districtId}/salespersons/{salespersonId}");
        response.EnsureSuccessStatusCode();
    }
}