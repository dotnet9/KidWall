using Avalonia;
using Avalonia.Controls;
using KidWall.App.ViewModels;
using KidWall.App.Views;

namespace KidWall.App.Behaviors;

public sealed class WindowCloseBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<WindowCloseBehavior, Window, bool>("IsEnabled");

    private WindowCloseBehavior()
    {
    }

    static WindowCloseBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Window>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Window window) => window.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Window window, bool value) => window.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(Window window, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            window.Closing += OnClosing;
        }
        else
        {
            window.Closing -= OnClosing;
        }
    }

    private static void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        if (window.DataContext is MainViewModel viewModel)
        {
            viewModel.PrepareToCloseCommand.Execute(null);
        }

        if (window is MainWindow mainWindow && !mainWindow.AllowCloseToExit)
        {
            e.Cancel = true;
            window.Hide();
        }
    }
}
