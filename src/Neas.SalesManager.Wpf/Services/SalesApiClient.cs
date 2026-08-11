// src/Neas.SalesManager.Wpf/Services/SalesApiClient.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Neas.SalesManager.Wpf.Models;

namespace Neas.SalesManager.Wpf.Services
{
    public interface ISalesApiClient
    {
        Task<List<DistrictSummaryApiDto>> GetDistrictsAsync();
        Task<DistrictDetailsApiDto?> GetDistrictDetailsAsync(int districtId);
        Task<bool> AssignSalespersonAsync(int districtId, int salespersonId, bool isPrimary);
        Task<bool> RemoveSalespersonAsync(int districtId, int salespersonId);
    }

    public class SalesApiClient : ISalesApiClient
    {
        private readonly HttpClient _httpClient;

        public SalesApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.BaseAddress = new Uri("https://localhost:7028/"); // Ensure this matches your API HTTPS port
        }

        public async Task<List<DistrictSummaryApiDto>> GetDistrictsAsync()
        {
            var result = await _httpClient.GetFromJsonAsync<List<DistrictSummaryApiDto>>("api/districts");
            return result ?? new List<DistrictSummaryApiDto>();
        }

        public async Task<DistrictDetailsApiDto?> GetDistrictDetailsAsync(int districtId)
        {
            return await _httpClient.GetFromJsonAsync<DistrictDetailsApiDto>($"api/districts/{districtId}");
        }

        public async Task<bool> AssignSalespersonAsync(int districtId, int salespersonId, bool isPrimary)
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"api/districts/{districtId}/salespersons",
                new AssignSalespersonApiRequest(salespersonId, isPrimary)
            );
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RemoveSalespersonAsync(int districtId, int salespersonId)
        {
            var response = await _httpClient.DeleteAsync($"api/districts/{districtId}/salespersons/{salespersonId}");
            return response.IsSuccessStatusCode;
        }
    }
}