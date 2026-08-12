using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Neas.SalesManager.Wpf.Models;

namespace Neas.SalesManager.Wpf.Services;

public class SalesApiClient : ISalesApiClient
{
    private readonly HttpClient _httpClient;

    public SalesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        // Ensure BaseAddress is fallback-configured if missing from DI
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("http://localhost:5000/"); // Update port to match your API
        }
    }

    /// <summary>
    /// Commandment 2: Read data via Web Services using HTTP GET
    /// </summary>
    public async Task<List<DistrictSummary>> GetDistrictsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<DistrictSummary>>("api/districts") ?? new();
    }

    /// <summary>
    /// Commandment 2: Read single resource via Web Services using HTTP GET
    /// </summary>
    public async Task<DistrictDetails?> GetDistrictDetailsAsync(int districtId)
    {
        return await _httpClient.GetFromJsonAsync<DistrictDetails>($"api/districts/{districtId}");
    }

    /// <summary>
    /// Commandment 2: Read data via Web Services using HTTP GET
    /// </summary>
    public async Task<List<Salesperson>> GetAllSalespersonsAsync()
    {
        var response = await _httpClient.GetAsync("api/salespersons");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<Salesperson>>();
        return result ?? new List<Salesperson>();
    }

    /// <summary>
    /// Commandment 2: Receive/update data via Web Services using HTTP PUT
    /// </summary>
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

    public async Task CreateDistrictAsync(string name, int primarySalespersonId)
    {
        var request = new CreateDistrictRequest(name, primarySalespersonId);
        var response = await _httpClient.PostAsJsonAsync("api/districts", request);

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