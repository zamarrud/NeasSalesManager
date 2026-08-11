// src/Neas.SalesManager.Wpf/ViewModels/DistrictOverviewViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Neas.SalesManager.Wpf.Commands;
using Neas.SalesManager.Wpf.Models;
using Neas.SalesManager.Wpf.Services;

namespace Neas.SalesManager.Wpf.ViewModels;

public class DistrictOverviewViewModel : INotifyPropertyChanged
{
    private readonly ISalesApiClient _apiClient;
    private DistrictModel? _selectedDistrict;
    private int _newSalespersonId;
    private bool _assignAsPrimary;
    private string _statusMessage = "Ready";

    public ObservableCollection<DistrictModel> Districts { get; } = new();
    public ObservableCollection<SalespersonModel> AssociatedSalespersons { get; } = new();
    public ObservableCollection<StoreModel> Stores { get; } = new();

    public DistrictModel? SelectedDistrict
    {
        get => _selectedDistrict;
        set
        {
            if (_selectedDistrict != value)
            {
                _selectedDistrict = value;
                OnPropertyChanged();
                if (_selectedDistrict != null)
                {
                    _ = LoadDistrictDetailsAsync(_selectedDistrict.DistrictId);
                }
            }
        }
    }

    public int NewSalespersonId
    {
        get => _newSalespersonId;
        set { _newSalespersonId = value; OnPropertyChanged(); }
    }

    public bool AssignAsPrimary
    {
        get => _assignAsPrimary;
        set { _assignAsPrimary = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public ICommand LoadDistrictsCommand { get; }
    public ICommand AssignSalespersonCommand { get; }
    public ICommand RemoveSalespersonCommand { get; }

    public DistrictOverviewViewModel(ISalesApiClient apiClient)
    {
        _apiClient = apiClient;
        LoadDistrictsCommand = new AsyncRelayCommand(LoadDistrictsAsync);
        AssignSalespersonCommand = new AsyncRelayCommand(AssignSalespersonAsync, () => SelectedDistrict != null && NewSalespersonId > 0);
        RemoveSalespersonCommand = new AsyncRelayCommand<SalespersonModel>(RemoveSalespersonAsync);
    }

    public async Task LoadDistrictsAsync()
    {
        try
        {
            StatusMessage = "Loading districts...";
            var list = await _apiClient.GetDistrictsAsync();
            Districts.Clear();
            foreach (var item in list)
            {
                Districts.Add(new DistrictModel { DistrictId = item.DistrictId, Name = item.Name });
            }
            StatusMessage = $"Loaded {Districts.Count} districts.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading districts: {ex.Message}";
        }
    }

    public async Task LoadDistrictDetailsAsync(int districtId)
    {
        try
        {
            StatusMessage = $"Loading details for district {districtId}...";
            var details = await _apiClient.GetDistrictDetailsAsync(districtId);

            AssociatedSalespersons.Clear();
            Stores.Clear();

            if (details != null)
            {
                foreach (var sp in details.Salespersons)
                {
                    AssociatedSalespersons.Add(new SalespersonModel
                    {
                        SalespersonId = sp.SalespersonId,
                        FullName = $"{sp.FirstName} {sp.LastName}",
                        Email = sp.Email,
                        IsPrimary = sp.IsPrimary
                    });
                }

                foreach (var st in details.Stores)
                {
                    Stores.Add(new StoreModel
                    {
                        StoreId = st.StoreId,
                        Name = st.Name,
                        Address = st.Address ?? "N/A"
                    });
                }
            }
            StatusMessage = "District details loaded successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading district details: {ex.Message}";
        }
    }

    public async Task AssignSalespersonAsync()
    {
        if (SelectedDistrict == null || NewSalespersonId <= 0) return;

        try
        {
            StatusMessage = "Assigning salesperson...";
            bool success = await _apiClient.AssignSalespersonAsync(SelectedDistrict.DistrictId, NewSalespersonId, AssignAsPrimary);
            if (success)
            {
                StatusMessage = "Salesperson assigned successfully.";
                await LoadDistrictDetailsAsync(SelectedDistrict.DistrictId);
            }
            else
            {
                StatusMessage = "Failed to assign salesperson.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Assignment Error: {ex.Message}";
        }
    }

    public async Task RemoveSalespersonAsync(SalespersonModel? salesperson)
    {
        if (salesperson == null || SelectedDistrict == null) return;

        try
        {
            StatusMessage = "Removing salesperson...";
            bool success = await _apiClient.RemoveSalespersonAsync(SelectedDistrict.DistrictId, salesperson.SalespersonId);
            if (success)
            {
                AssociatedSalespersons.Remove(salesperson);
                StatusMessage = "Salesperson removed successfully.";
            }
            else
            {
                StatusMessage = "Failed to remove salesperson.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Removal Error: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}