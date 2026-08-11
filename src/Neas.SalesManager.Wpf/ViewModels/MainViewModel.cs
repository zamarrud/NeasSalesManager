using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Neas.SalesManager.Wpf.Models;
using Neas.SalesManager.Wpf.Services;

namespace Neas.SalesManager.Wpf.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ISalesApiClient _apiClient;
    private readonly IDialogService _dialogService;

    public ObservableCollection<DistrictSummary> Districts { get; } = new();
    public ObservableCollection<Store> Stores { get; } = new();
    public ObservableCollection<Salesperson> Salespersons { get; } = new();    
    // 1. Add new ObservableCollection for the dropdown items
    public ObservableCollection<Salesperson> AvailableSalespersons { get; } = new();

    private string _newDistrictName = string.Empty;
    public string NewDistrictName
    {
        get => _newDistrictName;
        set
        {
            _newDistrictName = value;
            OnPropertyChanged();
            (CreateDistrictCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private Salesperson? _selectedPrimaryForNewDistrict;
    public Salesperson? SelectedPrimaryForNewDistrict
    {
        get => _selectedPrimaryForNewDistrict;
        set
        {
            _selectedPrimaryForNewDistrict = value;
            OnPropertyChanged();
            (CreateDistrictCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private DistrictSummary? _selectedDistrict;
    public DistrictSummary? SelectedDistrict
    {
        get => _selectedDistrict;
        set
        {
            _selectedDistrict = value;
            OnPropertyChanged();
            _ = LoadDistrictDetailsAsync();
            (AssignSalespersonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private Salesperson? _selectedSalesperson;
    public Salesperson? SelectedSalesperson
    {
        get => _selectedSalesperson;
        set
        {
            _selectedSalesperson = value;
            OnPropertyChanged();
            (RemoveSalespersonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // 2. Add property for the selected salesperson in the dropdown
    private Salesperson? _selectedSalespersonToAssign;
    public Salesperson? SelectedSalespersonToAssign
    {
        get => _selectedSalespersonToAssign;
        set
        {
            _selectedSalespersonToAssign = value;
            OnPropertyChanged();
            (AssignSalespersonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private bool _isPrimaryAssign;
    public bool IsPrimaryAssign
    {
        get => _isPrimaryAssign;
        set { _isPrimaryAssign = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }
         

    public ICommand CreateDistrictCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AssignSalespersonCommand { get; }
    public ICommand RemoveSalespersonCommand { get; }
    public ICommand TogglePrimaryCommand { get; }

    public MainViewModel(ISalesApiClient apiClient, IDialogService dialogService)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;

        CreateDistrictCommand = new AsyncRelayCommand(CreateDistrictAsync, () => !string.IsNullOrWhiteSpace(NewDistrictName) && SelectedPrimaryForNewDistrict != null);
        RefreshCommand = new AsyncRelayCommand(LoadInitialDataAsync);
        AssignSalespersonCommand = new AsyncRelayCommand(AssignSalespersonAsync, () => SelectedDistrict != null);
        RemoveSalespersonCommand = new AsyncRelayCommand(RemoveSalespersonAsync, () => SelectedSalesperson != null);
        TogglePrimaryCommand = new AsyncRelayCommand<Salesperson>(TogglePrimarySalespersonAsync);

        _ = LoadInitialDataAsync(); 
    }

    public async Task LoadInitialDataAsync()
    {
        try
        {
            StatusMessage = "Loading data...";

            // Load All Available Salespersons for the Dropdown
            AvailableSalespersons.Clear();
            var allSalespersons = await _apiClient.GetAllSalespersonsAsync();
            foreach (var sp in allSalespersons) AvailableSalespersons.Add(sp);

            if (AvailableSalespersons.Any())
            {
                SelectedSalespersonToAssign = AvailableSalespersons.First();
                SelectedPrimaryForNewDistrict = AvailableSalespersons.First();
            }

            // Load Districts
            Districts.Clear();
            var districtsList = await _apiClient.GetDistrictsAsync();
            foreach (var d in districtsList) Districts.Add(d);

            if (Districts.Any() && SelectedDistrict == null) SelectedDistrict = Districts.First();

            StatusMessage = "Data loaded successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Error loading data.";
            _dialogService.ShowError($"Failed to load data: {ex.Message}", "Connection Error");
        }
    }

    private async Task LoadDistrictDetailsAsync()
    {
        if (SelectedDistrict == null) return;

        try
        {
            StatusMessage = $"Loading details for {SelectedDistrict.Name}...";
            Stores.Clear();
            Salespersons.Clear();

            var details = await _apiClient.GetDistrictDetailsAsync(SelectedDistrict.DistrictId);
            if (details != null)
            {
                foreach (var st in details.Stores) Stores.Add(st);
                foreach (var sp in details.Salespersons) Salespersons.Add(sp);
            }
            StatusMessage = $"Loaded details for {SelectedDistrict.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Error loading district details.";
            MessageBox.Show($"Error loading details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public async Task CreateDistrictAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDistrictName) || SelectedPrimaryForNewDistrict == null) return;

        try
        {
            StatusMessage = $"Creating district '{NewDistrictName}'...";
            // 1. Send API Request
            await _apiClient.CreateDistrictAsync(NewDistrictName, SelectedPrimaryForNewDistrict.SalespersonId);

            // 2. Refresh UI Data Collections
            await LoadInitialDataAsync();

            // 3. Clear Input Property
            NewDistrictName = string.Empty;
            StatusMessage = "District created successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = "District creation failed.";
            _dialogService.ShowError(ex.Message, "Creation Error");
        }
    }

    private async Task TogglePrimarySalespersonAsync(Salesperson? clickedSalesperson)
    {
        if (SelectedDistrict == null || clickedSalesperson == null) return;

        // If already primary, do nothing (every district must maintain 1 primary)
        if (clickedSalesperson.IsPrimary) return;

        try
        {
            StatusMessage = $"Promoting {clickedSalesperson.FirstName} {clickedSalesperson.LastName} to Primary...";

            // 1. Call API to set this salesperson as Primary (DB procedure will demote the previous primary)
            await _apiClient.AssignSalespersonAsync(
                SelectedDistrict.DistrictId,
                clickedSalesperson.SalespersonId,
                isPrimary: true
            );

            // 2. Reload District Details from API so the DataGrid refreshes with the new roles
            await LoadDistrictDetailsAsync();

            StatusMessage = $"{clickedSalesperson.FirstName} {clickedSalesperson.LastName} is now the primary salesperson.";
        }
        catch (Exception ex)
        {
            await LoadDistrictDetailsAsync(); // Reset grid state on failure
            StatusMessage = "Failed to update primary salesperson.";
            _dialogService.ShowError(ex.Message, "Assignment Conflict / Error");
        }
    }

    private async Task AssignSalespersonAsync()
    {
        if (SelectedDistrict == null || SelectedSalespersonToAssign == null) return;

        // Check if user attempts to uncheck Primary on the current primary salesperson
        var currentPrimary = Salespersons.FirstOrDefault(sp => sp.IsPrimary);
        if (!IsPrimaryAssign && currentPrimary != null && currentPrimary.SalespersonId == SelectedSalespersonToAssign.SalespersonId)
        {
            _dialogService.ShowWarning(
                $"'{currentPrimary.FirstName} {currentPrimary.LastName}' is currently the Primary Salesperson.\n\nEvery district must have a primary salesperson. Assign another salesperson as primary to swap roles.",
                "Cannot Remove Primary Status"
            );
            return;
        }

        try
        {
            StatusMessage = $"Assigning {SelectedSalespersonToAssign.FirstName}...";
            await _apiClient.AssignSalespersonAsync(
                SelectedDistrict.DistrictId,
                SelectedSalespersonToAssign.SalespersonId,
                IsPrimaryAssign
            );

            await LoadDistrictDetailsAsync();
            StatusMessage = "Salesperson assignment updated successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Assignment failed.";
            _dialogService.ShowError(ex.Message, "Assignment Conflict / Error");
        }
    }

    private async Task RemoveSalespersonAsync()
    {
        if (SelectedDistrict == null || SelectedSalesperson == null) return;

        // Client-side Guard Clause
        if (SelectedSalesperson.IsPrimary)
        {
            _dialogService.ShowWarning(
                $"'{SelectedSalesperson.FirstName} {SelectedSalesperson.LastName}' is currently the Primary Salesperson for this district.\n\nTo ensure continuity, please assign another primary salesperson before removing this one.",
                "Cannot Remove Primary Salesperson"
            );
            return;
        }

        try
        {
            StatusMessage = $"Removing {SelectedSalesperson.FirstName}...";
            await _apiClient.RemoveSalespersonAsync(SelectedDistrict.DistrictId, SelectedSalesperson.SalespersonId);

            await LoadDistrictDetailsAsync();
            StatusMessage = "Salesperson removed successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Removal failed.";
            _dialogService.ShowError($"Error removing salesperson: {ex.Message}", "Removal Error");
        }
    }
}