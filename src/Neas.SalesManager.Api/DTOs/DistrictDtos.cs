// src/Neas.SalesManager.Api/DTOs/DistrictDtos.cs
namespace Neas.SalesManager.Api.DTOs;

public record DistrictSummaryDto(
    int DistrictId,
    string Name
);

public record StoreDto(
    int StoreId,
    string Name,
    string? Address
);

public record SalespersonDto(
    int SalespersonId,
    string FirstName,
    string LastName,
    string Email,
    bool IsPrimary
);

public record DistrictDetailsDto(
    int DistrictId,
    string Name,
    IReadOnlyList<SalespersonDto> Salespersons,
    IReadOnlyList<StoreDto> Stores
);

public record AssignSalespersonRequest(
    int SalespersonId,
    bool IsPrimary
);