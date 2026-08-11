// tests/Neas.SalesManager.Wpf.Tests/DistrictOverviewViewModelTests.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Neas.SalesManager.Wpf.Models;
using Neas.SalesManager.Wpf.Services;
using Neas.SalesManager.Wpf.ViewModels;
using Xunit;

namespace Neas.SalesManager.Wpf.Tests;

public class DistrictOverviewViewModelTests
{
    private readonly Mock<ISalesApiClient> _mockApiClient;
    private readonly DistrictOverviewViewModel _viewModel;

    public DistrictOverviewViewModelTests()
    {
        _mockApiClient = new Mock<ISalesApiClient>();
        _viewModel = new DistrictOverviewViewModel(_mockApiClient.Object);
    }

    [Fact]
    public async Task LoadDistrictsAsync_PopulatesDistrictsObservableCollection()
    {
        // Arrange
        var mockDistricts = new List<DistrictSummaryApiDto>
        {
            new(1, "North Denmark"),
            new(2, "Southern Denmark")
        };
        _mockApiClient.Setup(client => client.GetDistrictsAsync()).ReturnsAsync(mockDistricts);

        // Act
        await _viewModel.LoadDistrictsAsync();

        // Assert
        Assert.Equal(2, _viewModel.Districts.Count);
        Assert.Equal("North Denmark", _viewModel.Districts[0].Name);
        Assert.Equal("Southern Denmark", _viewModel.Districts[1].Name);
    }

    [Fact]
    public async Task LoadDistrictDetailsAsync_PopulatesSalespersonsAndStoresCollections()
    {
        // Arrange
        int districtId = 1;
        var mockDetails = new DistrictDetailsApiDto(
            districtId,
            "North Denmark",
            new List<SalespersonApiDto>
            {
                new(1, "Mads", "Mikkelsen", "mads@neasenergy.com", true),
                new(2, "Freja", "Lind", "freja@neasenergy.com", false)
            },
            new List<StoreApiDto>
            {
                new(101, "Aalborg Store", "Hobrovej 42")
            }
        );

        _mockApiClient.Setup(client => client.GetDistrictDetailsAsync(districtId))
            .ReturnsAsync(mockDetails);

        // Act
        await _viewModel.LoadDistrictDetailsAsync(districtId);

        // Assert
        Assert.Equal(2, _viewModel.AssociatedSalespersons.Count);
        Assert.Single(_viewModel.Stores);

        Assert.True(_viewModel.AssociatedSalespersons[0].IsPrimary);
        Assert.Equal("Mads Mikkelsen", _viewModel.AssociatedSalespersons[0].FullName);
        Assert.Equal("Aalborg Store", _viewModel.Stores[0].Name);
    }

    [Fact]
    public async Task AssignSalespersonAsync_CallsApiAndReloadsDetails_WhenSuccessful()
    {
        // Arrange
        int districtId = 1;
        int salespersonId = 3;

        _mockApiClient.Setup(client => client.AssignSalespersonAsync(districtId, salespersonId, true))
            .ReturnsAsync(true);

        _mockApiClient.Setup(client => client.GetDistrictDetailsAsync(districtId))
            .ReturnsAsync(new DistrictDetailsApiDto(districtId, "North Denmark", new List<SalespersonApiDto>(), new List<StoreApiDto>()));

        // Setting SelectedDistrict triggers the first GetDistrictDetailsAsync call via setter
        _viewModel.SelectedDistrict = new DistrictModel { DistrictId = districtId, Name = "North Denmark" };
        _viewModel.NewSalespersonId = salespersonId;
        _viewModel.AssignAsPrimary = true;

        // Act
        await _viewModel.AssignSalespersonAsync();

        // Assert
        _mockApiClient.Verify(client => client.AssignSalespersonAsync(districtId, salespersonId, true), Times.Once);
        _mockApiClient.Verify(client => client.GetDistrictDetailsAsync(districtId), Times.Exactly(2));

        // StatusMessage is updated to "District details loaded successfully." after Reload finishes
        Assert.Equal("District details loaded successfully.", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task RemoveSalespersonAsync_RemovesItemFromCollection_WhenApiSucceeds()
    {
        // Arrange
        _viewModel.SelectedDistrict = new DistrictModel { DistrictId = 1, Name = "North Denmark" };
        var salespersonToRemove = new SalespersonModel
        {
            SalespersonId = 2,
            FullName = "Freja Lind",
            Email = "freja@neasenergy.com",
            IsPrimary = false
        };
        _viewModel.AssociatedSalespersons.Add(salespersonToRemove);

        _mockApiClient.Setup(client => client.RemoveSalespersonAsync(1, 2))
            .ReturnsAsync(true);

        // Act
        await _viewModel.RemoveSalespersonAsync(salespersonToRemove);

        // Assert
        Assert.Empty(_viewModel.AssociatedSalespersons);
        _mockApiClient.Verify(client => client.RemoveSalespersonAsync(1, 2), Times.Once);
        Assert.Equal("Salesperson removed successfully.", _viewModel.StatusMessage);
    }
}