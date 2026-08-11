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

    #region Create District Tests

    [Fact]
    public async Task CreateDistrictCommand_CallsApiAndReloads_WhenValidInputProvided()
    {
        // Arrange
        var existingDistrict = new DistrictSummary(1, "Copenhagen Central");
        var createdDistrict = new DistrictSummary(2, "North Denmark");

        _apiClientMock.SetupSequence(x => x.GetDistrictsAsync())
            .ReturnsAsync(new List<DistrictSummary> { existingDistrict })
            .ReturnsAsync(new List<DistrictSummary> { existingDistrict, createdDistrict });

        _apiClientMock.Setup(x => x.GetDistrictDetailsAsync(It.IsAny<int>()))
            .ReturnsAsync(new DistrictDetails(1, "Copenhagen", new(), new()));

        _apiClientMock.Setup(x => x.CreateDistrictAsync("North Denmark", 5))
            .Returns(Task.CompletedTask);

        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(200);

        viewModel.NewDistrictName = "North Denmark";
        viewModel.SelectedPrimaryForNewDistrict = _mockSystemSalespersons.First(sp => sp.SalespersonId == 5);

        // Act
        viewModel.CreateDistrictCommand.Execute(null);
        await Task.Delay(250); // Give async Task execution time to complete

        // Assert
        _apiClientMock.Verify(x => x.CreateDistrictAsync("North Denmark", 5), Times.Once);
        Assert.Equal(2, viewModel.Districts.Count);
        Assert.True(string.IsNullOrEmpty(viewModel.NewDistrictName));
        Assert.Equal("District created successfully.", viewModel.StatusMessage);
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

    [Fact]
    public async Task AssignSalespersonCommand_ReassignsPrimary_WhenIsPrimaryAssignIsTrue()
    {
        // Arrange: District currently has John Doe (ID 1) as Primary
        var district = new DistrictSummary(1, "Copenhagen Central");
        var detailsBefore = new DistrictDetails(
            1,
            "Copenhagen Central",
            new List<Salesperson> { new(1, "John", "Doe", "john@neas.dk", true) },
            new()
        );

        // Details after primary swap: Michael Nygreen (ID 5) is Primary, John Doe is Secondary
        var detailsAfter = new DistrictDetails(
            1,
            "Copenhagen Central",
            new List<Salesperson>
            {
                new(1, "John", "Doe", "john@neas.dk", false),
                new(5, "Michael", "Nygreen", "michael@neas.dk", true)
            },
            new()
        );

        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(new List<DistrictSummary> { district });
        _apiClientMock.SetupSequence(x => x.GetDistrictDetailsAsync(1))
            .ReturnsAsync(detailsBefore)
            .ReturnsAsync(detailsAfter);

        _apiClientMock.Setup(x => x.AssignSalespersonAsync(1, 5, true)).Returns(Task.CompletedTask);

        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(150);

        // Select Michael Nygreen and check "Is Primary"
        viewModel.SelectedSalespersonToAssign = _mockSystemSalespersons.First(sp => sp.SalespersonId == 5);
        viewModel.IsPrimaryAssign = true;

        // Act
        viewModel.AssignSalespersonCommand.Execute(null);
        await Task.Delay(150);

        // Assert
        _apiClientMock.Verify(x => x.AssignSalespersonAsync(1, 5, true), Times.Once);
        Assert.Equal(2, viewModel.Salespersons.Count);

        var newPrimary = viewModel.Salespersons.First(sp => sp.IsPrimary);
        Assert.Equal(5, newPrimary.SalespersonId);
        Assert.Equal("Michael", newPrimary.FirstName);
        Assert.Equal("Salesperson assigned successfully.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task AssignSalespersonCommand_ShowsWarningDialogAndAborts_WhenAttemptingToDemotePrimaryWithoutSwap()
    {
        // Arrange
        var district = new DistrictSummary(1, "Copenhagen Central");
        var primarySalesperson = new Salesperson(1, "John", "Doe", "john@neas.dk", true);

        var details = new DistrictDetails(1, "Copenhagen Central", new List<Salesperson> { primarySalesperson }, new());

        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(new List<DistrictSummary> { district });
        _apiClientMock.Setup(x => x.GetDistrictDetailsAsync(1)).ReturnsAsync(details);

        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(150);

        // Select the current primary salesperson and UNCHECK "Is Primary"
        viewModel.SelectedSalespersonToAssign = _mockSystemSalespersons.First(sp => sp.SalespersonId == 1);
        viewModel.IsPrimaryAssign = false;

        // Act
        viewModel.AssignSalespersonCommand.Execute(null);
        await Task.Delay(150);

        // Assert
        _apiClientMock.Verify(x => x.AssignSalespersonAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        _dialogServiceMock.Verify(x => x.ShowWarning(It.Is<string>(s => s.Contains("Every district must have a primary salesperson")), "Cannot Remove Primary Status"), Times.Once);
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

    [Fact]
    public async Task RemoveSalespersonCommand_ShowsWarningDialogAndAborts_WhenSalespersonIsPrimary()
    {
        // Arrange
        var district = new DistrictSummary(1, "Copenhagen Central");
        var primarySalesperson = new Salesperson(1, "John", "Doe", "john@neas.dk", true); // IsPrimary = true

        var details = new DistrictDetails(1, "Copenhagen Central", new List<Salesperson> { primarySalesperson }, new());

        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(new List<DistrictSummary> { district });
        _apiClientMock.Setup(x => x.GetDistrictDetailsAsync(1)).ReturnsAsync(details);

        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(150);

        viewModel.SelectedSalesperson = primarySalesperson;

        // Act
        viewModel.RemoveSalespersonCommand.Execute(null);
        await Task.Delay(150);

        // Assert: Verify API remove was NEVER called and warning dialog WAS shown
        _apiClientMock.Verify(x => x.RemoveSalespersonAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _dialogServiceMock.Verify(x => x.ShowWarning(It.Is<string>(s => s.Contains("is currently the Primary Salesperson")), "Cannot Remove Primary Salesperson"), Times.Once);
    }

    [Fact]
    public async Task RemoveSalespersonCommand_CallsApiAndReloads_WhenSalespersonIsSecondary()
    {
        // Arrange
        var district = new DistrictSummary(1, "Copenhagen Central");
        var primarySalesperson = new Salesperson(1, "John", "Doe", "john@neas.dk", true);
        var secondarySalesperson = new Salesperson(2, "Jane", "Smith", "jane@neas.dk", false); // IsPrimary = false

        var detailsWithBoth = new DistrictDetails(1, "Copenhagen", new List<Salesperson> { primarySalesperson, secondarySalesperson }, new());
        var detailsAfterRemoval = new DistrictDetails(1, "Copenhagen", new List<Salesperson> { primarySalesperson }, new());

        _apiClientMock.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(new List<DistrictSummary> { district });
        _apiClientMock.SetupSequence(x => x.GetDistrictDetailsAsync(1))
            .ReturnsAsync(detailsWithBoth)
            .ReturnsAsync(detailsAfterRemoval);

        _apiClientMock.Setup(x => x.RemoveSalespersonAsync(1, 2)).Returns(Task.CompletedTask);

        var viewModel = new MainViewModel(_apiClientMock.Object, _dialogServiceMock.Object);
        await Task.Delay(150);

        viewModel.SelectedSalesperson = secondarySalesperson;

        // Act
        viewModel.RemoveSalespersonCommand.Execute(null);
        await Task.Delay(150);

        // Assert
        _apiClientMock.Verify(x => x.RemoveSalespersonAsync(1, 2), Times.Once);
        Assert.Single(viewModel.Salespersons);
        Assert.True(viewModel.Salespersons[0].IsPrimary);
        Assert.Equal("Salesperson removed successfully.", viewModel.StatusMessage);
    }

    #endregion
}