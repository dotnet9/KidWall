using Avalonia.Controls;

namespace KidWall.App.Views;

public partial class MainWindow : Window
{
    public bool AllowCloseToExit { get; set; }

    public MainWindow()
    {
        InitializeComponent();
    }
}
