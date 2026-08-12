using Neas.SalesManager.Api.DTOs;

namespace Neas.SalesManager.Api.Data;

public interface ISalespersonRepository
{
    Task<IEnumerable<SalespersonDto>> GetAllSalespersonsAsync();
}