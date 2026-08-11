// src/Neas.SalesManager.Wpf/Models/DistrictUiModels.cs
namespace Neas.SalesManager.Wpf.Models;

public class DistrictModel
{
    public int DistrictId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SalespersonModel
{
    public int SalespersonId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string RoleDisplay => IsPrimary ? "Primary" : "Secondary";
}

public class StoreModel
{
    public int StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

// API Contract DTOs for Deserialization
public record DistrictSummaryApiDto(int DistrictId, string Name);
public record SalespersonApiDto(int SalespersonId, string FirstName, string LastName, string Email, bool IsPrimary);
public record StoreApiDto(int StoreId, string Name, string? Address);
public record DistrictDetailsApiDto(int DistrictId, string Name, List<SalespersonApiDto> Salespersons, List<StoreApiDto> Stores);
public record AssignSalespersonApiRequest(int SalespersonId, bool IsPrimary);