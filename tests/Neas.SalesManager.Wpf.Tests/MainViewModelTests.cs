using Moq;
using Neas.SalesManager.Wpf.Models;
using Neas.SalesManager.Wpf.Services;
using Neas.SalesManager.Wpf.ViewModels;
using System.Net.Http;
using Xunit;

namespace Neas.SalesManager.Wpf.Tests;

public class MainViewModelTests
{
    private readonly Mock<ISalesApiClient> _apiClientMock;
    private readonly Mock<IDialogService> _dialogServiceMock;

    private readonly List<Salesperson> _mockSystemSalespersons = new()
    {
        new(1, "John", "Doe", "john@neas.dk", false),
        new(2, "Jane", "Smith", "jane@neas.dk", false),
        new(5, "Michael", "Nygreen", "michael@neas.dk", false)
    };

    public MainViewModelTests()
    {
        _apiClientMock = new Mock<ISalesApiClient>();
        _dialogServiceMock = new Mock<IDialogService>();

        // Default setup for salespersons dropdown loading
        _apiClientMock.Setup(x => x.GetAllSalespersonsAsync())
            .ReturnsAsync(_mockSystemSalespersons);
    }

    #region Initial Load Tests

    [Fact]
    public async Task Constructor_AutomaticallyLoadsDataAndSelectsFirst()
    {
        // Arrange
        var mockDistricts = new List<DistrictSummary>
        {
            new(1, "Copenhagen Central"),
            new(2, "Aarhus East")
        };

        var mockDetails = new DistrictDetails(
            1,
            "Copenhagen Central",
            new List<Salesperson> { new(10, "John", "Doe", "john@neas.dk", true) },
            new List<Store> { new(101, "Store A", "Main St 1") }
        );

        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(mockDistricts);
        _apiClientMock.Setup(x => x.GetDistrictDetailsAsync(1)).ReturnsAsync(mockDetails);

        // Act
        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(150); // Allow async void constructor call to complete

        // Assert
        Assert.Equal(3, viewModel.AvailableSalespersons.Count);
        Assert.Equal(2, viewModel.Districts.Count);
        Assert.NotNull(viewModel.SelectedDistrict);
        Assert.Equal(1, viewModel.SelectedDistrict.DistrictId);
        Assert.Single(viewModel.Salespersons);
        Assert.Single(viewModel.Stores);

        _apiClientMock.Verify(x => x.GetAllSalespersonsAsync(), Times.Once);
        _apiClientMock.Verify(x => x.GetDistrictsAsync(), Times.Once);
        _apiClientMock.Verify(x => x.GetDistrictDetailsAsync(1), Times.Once);
    }

    [Fact]
    public async Task LoadInitialDataAsync_HandlesEmptyDistrictsGracefully()
    {
        // Arrange
        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(new List<DistrictSummary>());

        // Act
        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await viewModel.LoadInitialDataAsync();

        // Assert
        Assert.Empty(viewModel.Districts);
        Assert.Null(viewModel.SelectedDistrict);
        Assert.Equal("Data loaded successfully.", viewModel.StatusMessage);
    }

    #endregion

    #region District Selection Tests

    [Fact]
    public async Task SelectedDistrict_Changed_LoadsNewDistrictDetails()
    {
        // Arrange
        var mockDistricts = new List<DistrictSummary>
        {
            new(1, "Copenhagen Central"),
            new(2, "Aarhus East")
        };

        var detailsDistrict1 = new DistrictDetails(1, "Copenhagen Central", new(), new());
        var detailsDistrict2 = new DistrictDetails(
            2,
            "Aarhus East",
            new List<Salesperson> { new(20, "Jane", "Smith", "jane@neas.dk", true) },
            new List<Store>()
        );

        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(mockDistricts);
        _apiClientMock.Setup(x => x.GetDistrictDetailsAsync(1)).ReturnsAsync(detailsDistrict1);
        _apiClientMock.Setup(x => x.GetDistrictDetailsAsync(2)).ReturnsAsync(detailsDistrict2);

        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(150);

        // Act
        viewModel.SelectedDistrict = mockDistricts[1]; // Select Aarhus East (ID 2)
        await Task.Delay(150);

        // Assert
        Assert.Equal(2, viewModel.SelectedDistrict.DistrictId);
        Assert.Single(viewModel.Salespersons);
        Assert.Equal("Jane", viewModel.Salespersons[0].FirstName);

        _apiClientMock.Verify(x => x.GetDistrictDetailsAsync(2), Times.Once);
    }

    #endregion

    #region Assign Salesperson Tests

    [Fact]
    public async Task AssignSalespersonCommand_CallsApiAndReloadsDetails_WhenSuccessful()
    {
        // Arrange
        var district = new DistrictSummary(1, "Copenhagen Central");
        var detailsBefore = new DistrictDetails(1, "Copenhagen Central", new(), new());
        var detailsAfter = new DistrictDetails(
            1,
            "Copenhagen Central",
            new List<Salesperson> { new(5, "Michael", "Nygreen", "michael@neas.dk", true) },
            new()
        );

        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(new List<DistrictSummary> { district });
        _apiClientMock.SetupSequence(x => x.GetDistrictDetailsAsync(1))
            .ReturnsAsync(detailsBefore)
            .ReturnsAsync(detailsAfter);

        _apiClientMock.Setup(x => x.AssignSalespersonAsync(1, 5, true)).Returns(Task.CompletedTask);

        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(150);

        viewModel.SelectedSalespersonToAssign = _mockSystemSalespersons.First(sp => sp.SalespersonId == 5);
        viewModel.IsPrimaryAssign = true;

        // Act
        viewModel.AssignSalespersonCommand.Execute(null);
        await Task.Delay(150);

        // Assert
        _apiClientMock.Verify(x => x.AssignSalespersonAsync(1, 5, true), Times.Once);
        Assert.Single(viewModel.Salespersons);
        Assert.Equal("Michael", viewModel.Salespersons[0].FirstName);
        Assert.Equal("Salesperson assigned successfully.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task AssignSalespersonCommand_CallsDialogService_OnApiError()
    {
        // Arrange
        var district = new DistrictSummary(1, "Copenhagen Central");
        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(new List<DistrictSummary> { district });
        _apiClientMock.Setup(x => x.GetDistrictDetailsAsync(1)).ReturnsAsync(new DistrictDetails(1, "Copenhagen", new(), new()));

        const string errorMessage = "API Error (Conflict): A primary salesperson is already assigned.";
        _apiClientMock.Setup(x => x.AssignSalespersonAsync(1, 5, true))
            .ThrowsAsync(new HttpRequestException(errorMessage));

        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(150);

        viewModel.SelectedSalespersonToAssign = _mockSystemSalespersons.First(sp => sp.SalespersonId == 5);
        viewModel.IsPrimaryAssign = true;

        // Act
        viewModel.AssignSalespersonCommand.Execute(null);
        await Task.Delay(150);

        // Assert
        _apiClientMock.Verify(x => x.AssignSalespersonAsync(1, 5, true), Times.Once);
        _dialogServiceMock.Verify(x => x.ShowError(errorMessage, "Assignment Conflict / Error"), Times.Once);
        Assert.Equal("Assignment failed.", viewModel.StatusMessage);
    }

    #endregion

    #region Remove Salesperson Tests

    [Fact]
    public async Task RemoveSalespersonCommand_CallsApiAndReloadsDetails_WhenSuccessful()
    {
        // Arrange
        var district = new DistrictSummary(1, "Copenhagen Central");
        var salesperson = new Salesperson(10, "John", "Doe", "john@neas.dk", false);

        var detailsWithUser = new DistrictDetails(1, "Copenhagen", new List<Salesperson> { salesperson }, new());
        var detailsEmpty = new DistrictDetails(1, "Copenhagen", new List<Salesperson>(), new());

        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(new List<DistrictSummary> { district });
        _apiClientMock.SetupSequence(x => x.GetDistrictDetailsAsync(1))
            .ReturnsAsync(detailsWithUser)
            .ReturnsAsync(detailsEmpty);

        _apiClientMock.Setup(x => x.RemoveSalespersonAsync(1, 10)).Returns(Task.CompletedTask);

        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(150);

        viewModel.SelectedSalesperson = salesperson;

        // Act
        viewModel.RemoveSalespersonCommand.Execute(null);
        await Task.Delay(150);

        // Assert
        _apiClientMock.Verify(x => x.RemoveSalespersonAsync(1, 10), Times.Once);
        Assert.Empty(viewModel.Salespersons);
        Assert.Equal("Salesperson removed successfully.", viewModel.StatusMessage);
    }

    #endregion
}