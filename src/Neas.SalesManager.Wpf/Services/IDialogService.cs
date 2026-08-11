using System.Windows;

namespace Neas.SalesManager.Wpf.Services;

public interface IDialogService
{
    void ShowError(string message, string title);
    void ShowWarning(string message, string title);
}

public class DialogService : IDialogService
{
    public void ShowError(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowWarning(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}