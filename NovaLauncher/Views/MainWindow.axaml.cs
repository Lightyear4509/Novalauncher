using Avalonia.Controls;
using NovaLauncher.Services;
using NovaLauncher.ViewModels;

namespace NovaLauncher.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        IFileDialogService fileDialogService =
            new FileDialogService(this);

        _viewModel =
            new MainWindowViewModel(fileDialogService);

        DataContext = _viewModel;

        _viewModel.UpdateLibraryCount();

        if (_viewModel.Games.Count > 0)
        {
            _viewModel.StatusText =
                "Status: Saved library loaded.";
        }
    }
}