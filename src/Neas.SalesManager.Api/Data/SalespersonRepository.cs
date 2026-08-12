using System.Data;
using Microsoft.Data.SqlClient;
using Neas.SalesManager.Api.DTOs;

namespace Neas.SalesManager.Api.Data;

public class SalespersonRepository : ISalespersonRepository
{
    private readonly string _connectionString;

    public SalespersonRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is missing.");
    }

    public async Task<IEnumerable<SalespersonDto>> GetAllSalespersonsAsync()
    {
        var salespersons = new List<SalespersonDto>();

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "SELECT SalespersonId, FirstName, LastName, Email FROM dbo.Salesperson ORDER BY FirstName, LastName",
            connection);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            salespersons.Add(new SalespersonDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                IsPrimary: false
            ));
        }

        return salespersons;
    }
}