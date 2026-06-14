using Avalonia.Controls;
using Dos2SaveEditor.ViewModels;

namespace Dos2SaveEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StorageProvider = StorageProvider;
            // Auto-load from env var for dev convenience
            var path = System.Environment.GetEnvironmentVariable("DOS2_SAVE_PATH");
            if (!string.IsNullOrEmpty(path) && vm.OpenSaveCommand.CanExecute(null))
                vm.OpenSaveCommand.Execute(null);
        }
    }
}