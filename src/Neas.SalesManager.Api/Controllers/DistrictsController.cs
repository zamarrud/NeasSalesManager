using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Neas.SalesManager.Api.Data;
using Neas.SalesManager.Api.DTOs;
using Neas.SalesManager.Api.Hubs;
using System.Threading.Tasks;

namespace Neas.SalesManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DistrictsController : ControllerBase
{
    private readonly IDistrictRepository _repository;
    private readonly ILogger<DistrictsController> _logger;
    private readonly IHubContext<SalesManagerHub> _hubContext;

    public DistrictsController(
        IDistrictRepository repository, 
        ILogger<DistrictsController> logger,
        IHubContext<SalesManagerHub> hubContext)
    {
        _repository = repository;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Commandment 2: Systems expose/share data via Web Services - Data is available via HTTP GET
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DistrictSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDistricts()
    {
        var districts = await _repository.GetAllDistrictsAsync();
        return Ok(districts);
    }

    /// <summary>
    /// Commandment 2: Systems expose/share data via Web Services - Data is available via HTTP GET
    /// </summary>
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
    /// Commandment 2: Systems receive data via Web Services - Data is sent via HTTP PUT
    /// </summary>
    [HttpPut("{districtId:int}/salespersons")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignSalesperson(int districtId, [FromBody] AssignSalespersonRequest request)
    {
        if (request.SalespersonId <= 0)
        {
            return BadRequest(new { message = "Valid SalespersonId is required." });
        }

        await _repository.AssignSalespersonAsync(districtId, request.SalespersonId, request.IsPrimary);

        _logger.LogInformation(
            "Assigned Salesperson {SalespersonId} to District {DistrictId} (Primary: {IsPrimary})",
            request.SalespersonId, districtId, request.IsPrimary
        );

        // Send event notification via SignalR
        await _hubContext.Clients.All.SendAsync("DistrictUpdated", districtId);

        return NoContent();
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

        // MUST broadcast creation to all clients
        await _hubContext.Clients.All.SendAsync("DistrictUpdated", newDistrictId);

        return CreatedAtAction(nameof(GetDistrictDetails), new { id = newDistrictId }, new { DistrictId = newDistrictId, Name = request.Name });
    }

    [HttpDelete("{districtId:int}/salespersons/{salespersonId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveSalesperson(int districtId, int salespersonId)
    {
        await _repository.RemoveSalespersonAsync(districtId, salespersonId);

        _logger.LogInformation(
            "Removed Salesperson {SalespersonId} from District {DistrictId}",
            salespersonId, districtId
        );

        // Send event notification via SignalR
        await _hubContext.Clients.All.SendAsync("DistrictUpdated", districtId);

        return NoContent();
    }
}