using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CodeWF.Avalonia.Controls.Controls;
using KidWall.App.ViewModels;
using System.Diagnostics;

namespace KidWall.App.Views;

public partial class MainWindow : CodeWFWindow
{
    private readonly DispatcherTimer _floatTimer;
    private readonly long _floatStartTimestamp = Stopwatch.GetTimestamp();

    public bool AllowCloseToExit { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        _floatTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => AnimateHeader());
        _floatTimer.Start();
        Closed += (_, _) => _floatTimer.Stop();
    }

    private void AnimateHeader()
    {
        var seconds = (Stopwatch.GetTimestamp() - _floatStartTimestamp) / (double)Stopwatch.Frequency;
        var angle = seconds * Math.PI * 2 / 3.2;
        var brandBob = Math.Sin(angle);
        PART_BrandMark.RenderTransform = new TransformGroup
        {
            Children =
            {
                new TranslateTransform(0, -1.5 * (brandBob + 1)),
                new RotateTransform(brandBob * 3),
            },
        };
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
