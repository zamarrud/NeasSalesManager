namespace Neas.SalesManager.Contracts;

public record DistrictSummaryDto(
    int DistrictId,
    string Name
);

public record SalespersonDto(
    int SalespersonId,
    string FirstName,
    string LastName,
    string Email,
    bool IsPrimary
)
{
    public string FullNameWithId => $"{FirstName} {LastName} (ID: {SalespersonId})";
}

public record StoreDto(
    int StoreId,
    string Name,
    string Address
);

public record DistrictDetailsDto(
    int DistrictId,
    string Name,
    IEnumerable<SalespersonDto> Salespersons,
    IEnumerable<StoreDto> Stores
);

public record CreateDistrictRequest(
    string Name,
    int PrimarySalespersonId
);

public record AssignSalespersonRequest(
    int SalespersonId,
    bool IsPrimary
);