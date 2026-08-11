// src/Neas.SalesManager.Api/Controllers/DistrictsController.cs
using Microsoft.AspNetCore.Mvc;
using Neas.SalesManager.Api.Data;
using Neas.SalesManager.Api.DTOs;

namespace Neas.SalesManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DistrictsController : ControllerBase
{
    private readonly IDistrictRepository _repository;
    private readonly ILogger<DistrictsController> _logger;

    public DistrictsController(IDistrictRepository repository, ILogger<DistrictsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DistrictSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDistricts()
    {
        var districts = await _repository.GetAllDistrictsAsync();
        return Ok(districts);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DistrictDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDistrictDetails(int id)
    {
        var district = await _repository.GetDistrictDetailsAsync(id);
        if (district == null)
        {
            _logger.LogWarning("District with ID {DistrictId} not found.", id);
            return NotFound(new { Message = $"District {id} not found." });
        }

        return Ok(district);
    }

    [HttpGet("salespersons")]
    [ProducesResponseType(typeof(IEnumerable<SalespersonDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSalespersons()
    {
        var salespersons = await _repository.GetAllSalespersonsAsync();
        return Ok(salespersons);
    }

    [HttpPut("{id:int}/salespersons")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignSalesperson(int id, [FromBody] AssignSalespersonRequest request)
    {
        if (request.SalespersonId <= 0)
        {
            return BadRequest(new { Message = "Invalid SalespersonId specified." });
        }

        await _repository.AssignSalespersonAsync(id, request.SalespersonId, request.IsPrimary);

        _logger.LogInformation("Assigned Salesperson {SalespersonId} to District {DistrictId} (Primary: {IsPrimary})",
            request.SalespersonId, id, request.IsPrimary);

        return NoContent();
    }

    [HttpDelete("{id:int}/salespersons/{salespersonId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveSalesperson(int id, int salespersonId)
    {
        await _repository.RemoveSalespersonAsync(id, salespersonId);

        _logger.LogInformation("Removed Salesperson {SalespersonId} from District {DistrictId}",
            salespersonId, id);

        return NoContent();
    }
}