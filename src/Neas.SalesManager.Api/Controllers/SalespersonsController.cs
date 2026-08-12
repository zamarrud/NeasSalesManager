using Microsoft.AspNetCore.Mvc;
using Neas.SalesManager.Api.Data;
using Neas.SalesManager.Api.DTOs;

namespace Neas.SalesManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SalespersonsController : ControllerBase
{
    private readonly ISalespersonRepository _repository;
    private readonly ILogger<SalespersonsController> _logger;

    public SalespersonsController(ISalespersonRepository repository, ILogger<SalespersonsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Commandment 2: Systems expose/share data via Web Services - Data is available via HTTP GET
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SalespersonDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSalespersons()
    {
        var salespersons = await _repository.GetAllSalespersonsAsync();
        return Ok(salespersons);
    }
}