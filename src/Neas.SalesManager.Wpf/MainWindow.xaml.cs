// src/Neas.SalesManager.Wpf/MainWindow.xaml.cs
using System.Net.Http;
using System.Windows;
using Neas.SalesManager.Wpf.Services;
using Neas.SalesManager.Wpf.ViewModels;

namespace Neas.SalesManager.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Setup API client and viewmodel
            var httpClient = new HttpClient();
            ISalesApiClient apiClient = new SalesApiClient(httpClient);
            var viewModel = new DistrictOverviewViewModel(apiClient);

            // Bind DataContext
            DataContext = viewModel;

            // Load initial data
            Loaded += async (s, e) => await viewModel.LoadDistrictsAsync();
        }
    }
}