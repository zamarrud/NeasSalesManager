// src/Neas.SalesManager.Api/Data/IDistrictRepository.cs
using Neas.SalesManager.Api.DTOs;

namespace Neas.SalesManager.Api.Data;

public interface IDistrictRepository
{
    Task<IEnumerable<DistrictSummaryDto>> GetAllDistrictsAsync();
    Task<DistrictDetailsDto?> GetDistrictDetailsAsync(int districtId);
    Task AssignSalespersonAsync(int districtId, int salespersonId, bool isPrimary);
    Task RemoveSalespersonAsync(int districtId, int salespersonId);
}