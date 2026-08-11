// src/Neas.SalesManager.Api/Data/DistrictRepository.cs
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Neas.SalesManager.Api.DTOs;

namespace Neas.SalesManager.Api.Data;

public class DistrictRepository : IDistrictRepository
{
    private readonly string _connectionString;

    public DistrictRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration), "DefaultConnection string is missing.");
    }

    public async Task<IEnumerable<DistrictSummaryDto>> GetAllDistrictsAsync()
    {
        const string sql = @"
            SELECT DistrictId, Name
            FROM dbo.District 
            ORDER BY Name ASC;";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<DistrictSummaryDto>(sql);
    }

    public async Task<DistrictDetailsDto?> GetDistrictDetailsAsync(int districtId)
    {
        using var connection = new SqlConnection(_connectionString);

        // Executes sp_GetDistrictDetails to return 3 result sets in a single DB round-trip
        using var multi = await connection.QueryMultipleAsync(
            "dbo.sp_GetDistrictDetails",
            new { DistrictId = districtId },
            commandType: CommandType.StoredProcedure
        );

        var district = await multi.ReadSingleOrDefaultAsync<DistrictSummaryDto>();
        if (district == null) return null;

        var stores = (await multi.ReadAsync<StoreDto>()).ToList();
        var salespersons = (await multi.ReadAsync<SalespersonDto>()).ToList();

        return new DistrictDetailsDto(district.DistrictId, district.Name, salespersons, stores);
    }

    public async Task AssignSalespersonAsync(int districtId, int salespersonId, bool isPrimary)
    {
        using var connection = new SqlConnection(_connectionString);

        // Executes atomic procedure handling primary demotion/upsert inside a single transaction
        await connection.ExecuteAsync(
            "dbo.sp_AssignSalespersonToDistrict",
            new { DistrictId = districtId, SalespersonId = salespersonId, IsPrimary = isPrimary },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task RemoveSalespersonAsync(int districtId, int salespersonId)
    {
        const string sql = @"
            DELETE FROM dbo.DistrictSalesperson 
            WHERE DistrictId = @DistrictId AND SalespersonId = @SalespersonId;";

        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { DistrictId = districtId, SalespersonId = salespersonId });
    }
}