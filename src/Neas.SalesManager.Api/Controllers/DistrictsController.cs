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

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDistrict([FromBody] CreateDistrictRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.PrimarySalespersonId <= 0)
        {
            return BadRequest(new { Message = "District Name and a valid Primary Salesperson ID are required." });
        }

        var newDistrictId = await _repository.CreateDistrictAsync(request.Name, request.PrimarySalespersonId);
        return CreatedAtAction(nameof(GetDistrictDetails), new { id = newDistrictId }, new { DistrictId = newDistrictId, Name = request.Name });
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
        
    /// <summary>
    /// Assigns or updates a salesperson for a given district.
    /// Supports both POST and PUT methods to maintain RESTful idempotency and API client compatibility.
    /// </summary>
    [HttpPost("{districtId:int}/salespersons")]
    [HttpPut("{districtId:int}/salespersons")]
    public async Task<IActionResult> AssignSalesperson(int districtId, [FromBody] AssignSalespersonRequest request)
    {
        if (request.SalespersonId <= 0)
            return BadRequest(new { message = "Valid SalespersonId is required." });

        await _repository.AssignSalespersonAsync(districtId, request.SalespersonId, request.IsPrimary);

        _logger.LogInformation("Assigned Salesperson {SalespersonId} to District {DistrictId} (Primary: {IsPrimary})",
            request.SalespersonId, districtId, request.IsPrimary);

        return Ok(new { message = "Salesperson assignment updated successfully." });
    }

    [HttpDelete("{districtId:int}/salespersons/{salespersonId:int}")]
    public async Task<IActionResult> RemoveSalesperson(int districtId, int salespersonId)
    {
        await _repository.RemoveSalespersonAsync(districtId, salespersonId);

        _logger.LogInformation("Removed Salesperson {SalespersonId} from District {DistrictId}",
            salespersonId, districtId);

        return NoContent(); 
    }
}