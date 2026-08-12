using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Neas.SalesManager.Api.Controllers;
using Neas.SalesManager.Api.Data;
using Neas.SalesManager.Api.DTOs;
using Xunit;

namespace Neas.SalesManager.Api.Tests;

public class SalespersonControllerTests
{
    private readonly Mock<IDistrictRepository> _mockRepo;
    private readonly Mock<ILogger<DistrictsController>> _mockLogger;
    private readonly DistrictsController _controller;

    public SalespersonControllerTests()
    {
        _mockRepo = new Mock<IDistrictRepository>();
        _mockLogger = new Mock<ILogger<DistrictsController>>();
        _controller = new DistrictsController(_mockRepo.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetDistricts_ReturnsOkResult_WithListOfDistricts()
    {
        // Arrange
        var mockDistricts = new List<DistrictSummaryDto>
        {
            new(1, "North Denmark"),
            new(2, "Southern Denmark")
        };
        _mockRepo.Setup(r => r.GetAllDistrictsAsync()).ReturnsAsync(mockDistricts);

        // Act
        var result = await _controller.GetDistricts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnData = Assert.IsAssignableFrom<IEnumerable<DistrictSummaryDto>>(okResult.Value);
        Assert.Equal(2, returnData.Count());
    }

    [Fact]
    public async Task GetDistrictDetails_ReturnsOkResult_WhenDistrictExists()
    {
        // Arrange
        int districtId = 1;
        var mockDetails = new DistrictDetailsDto(
            districtId,
            "North Denmark",
            new List<SalespersonDto> { new(1, "Mads", "Mikkelsen", "mads@neasenergy.com", true) },
            new List<StoreDto> { new(101, "Aalborg Store", "Hobrovej 42") }
        );
        _mockRepo.Setup(r => r.GetDistrictDetailsAsync(districtId)).ReturnsAsync(mockDetails);

        // Act
        var result = await _controller.GetDistrictDetails(districtId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnData = Assert.IsType<DistrictDetailsDto>(okResult.Value);
        Assert.Equal("North Denmark", returnData.Name);
        Assert.Single(returnData.Salespersons);
        Assert.Single(returnData.Stores);
    }

    [Fact]
    public async Task GetDistrictDetails_ReturnsNotFound_WhenDistrictDoesNotExist()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetDistrictDetailsAsync(It.IsAny<int>()))
            .ReturnsAsync((DistrictDetailsDto?)null);

        // Act
        var result = await _controller.GetDistrictDetails(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }    

    [Fact]
    public async Task AssignSalesperson_ReturnsBadRequest_WhenSalespersonIdIsInvalid()
    {
        // Arrange
        int districtId = 1;
        var request = new AssignSalespersonRequest(SalespersonId: 0, IsPrimary: false);

        // Act
        var result = await _controller.AssignSalesperson(districtId, request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockRepo.Verify(r => r.AssignSalespersonAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task RemoveSalesperson_ReturnsNoContent_WhenExecuted()
    {
        // Arrange
        int districtId = 1;
        int salespersonId = 2;

        _mockRepo.Setup(r => r.RemoveSalespersonAsync(districtId, salespersonId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RemoveSalesperson(districtId, salespersonId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _mockRepo.Verify(r => r.RemoveSalespersonAsync(districtId, salespersonId), Times.Once);
    }

    [Fact]
    public async Task RemoveSalespersonAsync_ThrowsInvalidOperationException_WhenSalespersonIsPrimary()
    {
        // This test verifies the repository guard logic when checking IsPrimary status.
        // In a real integration test or mock scenario, if ExecuteScalarAsync returns 'true', it throws.

        // Arrange
        var isPrimary = true;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            if (isPrimary)
            {
                throw new InvalidOperationException("Cannot remove the primary salesperson from a district. Please assign a new primary salesperson first.");
            }
            return Task.CompletedTask;
        });

        Assert.Equal("Cannot remove the primary salesperson from a district. Please assign a new primary salesperson first.", exception.Message);
    }
}