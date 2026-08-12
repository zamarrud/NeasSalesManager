using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Neas.SalesManager.Wpf.Models;
using Neas.SalesManager.Wpf.Services;

namespace Neas.SalesManager.Wpf.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ISalesApiClient _apiClient;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;

    public ObservableCollection<DistrictSummary> Districts { get; } = new();
    public ObservableCollection<Store> Stores { get; } = new();
    public ObservableCollection<Salesperson> Salespersons { get; } = new();        
    public ObservableCollection<Salesperson> AvailableSalespersons { get; } = new();
    
    // Clean Filtered Property: Returns ONLY unassigned salespersons (or empty if all are assigned)
    public IEnumerable<Salesperson> AssignableSalespersons
    {
        get
        {
            if (AvailableSalespersons == null || !AvailableSalespersons.Any())
                return Enumerable.Empty<Salesperson>();

            // Get IDs of salespersons already assigned to this district
            var assignedIds = Salespersons.Select(s => s.SalespersonId).ToHashSet();

            // Return only salespersons who are NOT assigned to this district
            return AvailableSalespersons.Where(sp => !assignedIds.Contains(sp.SalespersonId)).ToList();
        }
    }

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

    public MainViewModel(
            ISalesApiClient apiClient, 
            IDialogService dialogService, 
            INotificationService notificationService)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _notificationService = notificationService;

        CreateDistrictCommand = new AsyncRelayCommand(CreateDistrictAsync, () => !string.IsNullOrWhiteSpace(NewDistrictName) && SelectedPrimaryForNewDistrict != null);
        RefreshCommand = new AsyncRelayCommand(LoadInitialDataAsync);
        //AssignSalespersonCommand = new AsyncRelayCommand(AssignSalespersonAsync, () => SelectedDistrict != null);
        AssignSalespersonCommand = new AsyncRelayCommand(AssignSalespersonAsync, CanAssignSalesperson);
        RemoveSalespersonCommand = new AsyncRelayCommand(RemoveSalespersonAsync, CanRemoveSalesperson);
        TogglePrimaryCommand = new AsyncRelayCommand<Salesperson>(TogglePrimarySalespersonAsync);

        // Subscribe to real-time notification with UI Dispatcher marshalling
        _notificationService.OnDistrictUpdated += (districtId) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                await LoadDistrictsAsync1();
                if (SelectedDistrict != null && SelectedDistrict.DistrictId == districtId)
                {
                    await LoadDistrictDetailsAsync();
                }
            });
        };

        _ = LoadInitialDataAsync();
        _ = InitializeNotificationsAsync();
    }

    private bool CanAssignSalesperson()
    {
        return SelectedDistrict != null
            && SelectedSalespersonToAssign != null
            && AssignableSalespersons.Any();
    }

    private bool CanRemoveSalesperson()
    {
        // Enable button ONLY when a secondary salesperson is selected
        return SelectedSalesperson != null && !SelectedSalesperson.IsPrimary;
    }

    public async Task LoadInitialDataAsync()
    {
        try
        {
            StatusMessage = "Loading data...";
                        
            var allSalespersons = await _apiClient.GetAllSalespersonsAsync();
            AvailableSalespersons.Clear();
            foreach (var sp in allSalespersons) AvailableSalespersons.Add(sp);                        
                        
            SelectedPrimaryForNewDistrict = AvailableSalespersons.FirstOrDefault();

            await LoadDistrictsAsync();

            StatusMessage = "Data loaded successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load system data.";
            _dialogService.ShowError($"Error loading salespersons: {ex.Message}", "Initialization Error");
        }
    }

    public async Task LoadDistrictsAsync()
    {
        try
        {
            var currentSelectedId = SelectedDistrict?.DistrictId;
            var districtsList = await _apiClient.GetDistrictsAsync();

            Districts.Clear();
            foreach (var d in districtsList)
            {
                Districts.Add(d);
            }

            // Preserve selection or auto-select first item
            if (currentSelectedId.HasValue)
            {
                SelectedDistrict = Districts.FirstOrDefault(d => d.DistrictId == currentSelectedId.Value)
                                   ?? Districts.FirstOrDefault();
            }
            else
            {
                SelectedDistrict = Districts.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to refresh districts.";
        }
    }

    public async Task LoadDistrictsAsync1()
    {
        var districts = await _apiClient.GetDistrictsAsync();
        // Update UI collection
    }

    // Update District Details and Refresh the Dropdown List
    private async Task LoadDistrictDetailsAsync()
    {
        if (SelectedDistrict == null) return;

        try
        {
            var details = await _apiClient.GetDistrictDetailsAsync(SelectedDistrict.DistrictId);

            Salespersons.Clear();
            if (details?.Salespersons != null)
            {
                foreach (var sp in details.Salespersons)
                {
                    Salespersons.Add(sp);
                }
            }

            Stores.Clear();
            if (details?.Stores != null)
            {
                foreach (var st in details.Stores)
                {
                    Stores.Add(st);
                }
            }
                        
            OnPropertyChanged(nameof(AssignableSalespersons));
            SelectedSalespersonToAssign = AssignableSalespersons.FirstOrDefault();

            (AssignSalespersonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (RemoveSalespersonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load district details.";
            _dialogService.ShowError(ex.Message, "Data Error");
        }
    }

    private async Task InitializeNotificationsAsync()
    {
        try
        {
            await _notificationService.StartAsync();
        }
        catch (Exception ex)
        {
            // Log or handle initial connection error gracefully
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