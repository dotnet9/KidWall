using Avalonia.Controls;
using Avalonia.Input;
using KidWall.App.ViewModels;

namespace KidWall.App.Views;

public partial class MainWindow : Window
{
    public bool AllowCloseToExit { get; set; }

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainViewModel viewModel)
        {
            if (viewModel.IsPreviewOpen)
            {
                viewModel.ClosePreviewCommand.Execute(null);
                e.Handled = true;
            }
            else if (viewModel.IsSettingsOpen)
            {
                viewModel.ToggleSettingsCommand.Execute(null);
                e.Handled = true;
            }
        }

        base.OnKeyDown(e);
    }
}
