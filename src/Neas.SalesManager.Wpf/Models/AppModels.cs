namespace Neas.SalesManager.Wpf.Models;

public record CreateDistrictRequest(
    string Name,
    int PrimarySalespersonId
);

public record DistrictSummary(
    int DistrictId,
    string Name
);

public record Store(
    int StoreId,
    string Name,
    string? Address
);

public record Salesperson(
    int SalespersonId,
    string FirstName,
    string LastName,
    string Email,
    bool IsPrimary
)
{
    // Property used for ComboBox item formatting
    public string FullNameWithId => $"{FirstName} {LastName} (ID: {SalespersonId})";
}

public record DistrictDetails(
    int DistrictId,
    string Name,
    List<Salesperson> Salespersons,
    List<Store> Stores
);

public record AssignSalespersonRequest(
    int SalespersonId,
    bool IsPrimary
);