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

    public async Task<int> CreateDistrictAsync(string name, int primarySalespersonId)
    {
        using var connection = new SqlConnection(_connectionString);
        var parameters = new DynamicParameters();
        parameters.Add("@Name", name);
        parameters.Add("@PrimarySalespersonId", primarySalespersonId);
        parameters.Add("@NewDistrictId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(
            "dbo.sp_CreateDistrict",
            parameters,
            commandType: CommandType.StoredProcedure
        );

        return parameters.Get<int>("@NewDistrictId");
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
        using var connection = new SqlConnection(_connectionString);

        // Business Rule Guard: Check if the salesperson is the primary before removing
        const string checkSql = @"
        SELECT IsPrimary 
        FROM dbo.DistrictSalesperson 
        WHERE DistrictId = @DistrictId AND SalespersonId = @SalespersonId;";

        var isPrimary = await connection.ExecuteScalarAsync<bool?>(checkSql, new { DistrictId = districtId, SalespersonId = salespersonId });

        if (isPrimary == true)
        {
            throw new InvalidOperationException("Cannot remove the primary salesperson from a district. Please assign a new primary salesperson first.");
        }

        const string deleteSql = @"
        DELETE FROM dbo.DistrictSalesperson 
        WHERE DistrictId = @DistrictId AND SalespersonId = @SalespersonId;";

        await connection.ExecuteAsync(deleteSql, new { DistrictId = districtId, SalespersonId = salespersonId });
    }
}