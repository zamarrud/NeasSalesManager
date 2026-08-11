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

    private DistrictSummary? _selectedDistrict;
    // 2. Add property for the selected salesperson in the dropdown
    private Salesperson? _selectedSalespersonToAssign;
    public Salesperson? SelectedSalespersonToAssign
    {
        get => _selectedSalespersonToAssign;
        set
        {
            _selectedSalespersonToAssign = value;
            OnPropertyChanged();
            // Automatically updates command execution availability
            (AssignSalespersonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }
    public DistrictSummary? SelectedDistrict
    {
        get => _selectedDistrict;
        set
        {
            _selectedDistrict = value;
            OnPropertyChanged();
            _ = LoadDistrictDetailsAsync();
        }
    }

    private Salesperson? _selectedSalesperson;
    public Salesperson? SelectedSalesperson
    {
        get => _selectedSalesperson;
        set { _selectedSalesperson = value; OnPropertyChanged(); }
    }

    private int _assignSalespersonId = 1;
    public int AssignSalespersonId
    {
        get => _assignSalespersonId;
        set { _assignSalespersonId = value; OnPropertyChanged(); }
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

    public ICommand RefreshCommand { get; }
    public ICommand AssignSalespersonCommand { get; }
    public ICommand RemoveSalespersonCommand { get; }

    public MainViewModel(ISalesApiClient apiClient, IDialogService dialogService)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(LoadInitialDataAsync);
        AssignSalespersonCommand = new AsyncRelayCommand(AssignSalespersonAsync, () => SelectedDistrict != null);
        RemoveSalespersonCommand = new AsyncRelayCommand(RemoveSalespersonAsync, () => SelectedSalesperson != null);

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
                SelectedSalespersonToAssign = AvailableSalespersons.First();

            // Load Districts
            Districts.Clear();
            var districtsList = await _apiClient.GetDistrictsAsync();
            foreach (var d in districtsList) Districts.Add(d);

            if (Districts.Any()) SelectedDistrict = Districts.First();

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

    private async Task AssignSalespersonAsync()
    {
        if (SelectedDistrict == null || SelectedSalespersonToAssign == null) return;

        try
        {
            StatusMessage = $"Assigning {SelectedSalespersonToAssign.FirstName}...";
            await _apiClient.AssignSalespersonAsync(
                SelectedDistrict.DistrictId,
                SelectedSalespersonToAssign.SalespersonId,
                IsPrimaryAssign
            );

            await LoadDistrictDetailsAsync();
            StatusMessage = "Salesperson assigned successfully.";
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

        try
        {
            StatusMessage = $"Removing salesperson {SelectedSalesperson.FirstName}...";
            await _apiClient.RemoveSalespersonAsync(SelectedDistrict.DistrictId, SelectedSalesperson.SalespersonId);

            await LoadDistrictDetailsAsync();
            StatusMessage = "Salesperson removed successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Removal failed.";
            MessageBox.Show($"Error removing salesperson: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}