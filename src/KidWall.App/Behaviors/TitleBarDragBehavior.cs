using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaControl = Avalonia.Controls.Control;
using AvaloniaTextBox = Avalonia.Controls.TextBox;
using AvaloniaWindow = Avalonia.Controls.Window;
using AvaloniaWindowState = Avalonia.Controls.WindowState;

namespace KidWall.App.Behaviors;

public sealed class TitleBarDragBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TitleBarDragBehavior, AvaloniaControl, bool>("IsEnabled");

    static TitleBarDragBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<AvaloniaControl>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(AvaloniaControl control) => control.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(AvaloniaControl control, bool value) => control.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(AvaloniaControl control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            control.PointerPressed += OnPointerPressed;
            control.DoubleTapped += OnDoubleTapped;
        }
        else
        {
            control.PointerPressed -= OnPointerPressed;
            control.DoubleTapped -= OnDoubleTapped;
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not AvaloniaControl control)
        {
            return;
        }

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is AvaloniaControl sourceControl &&
            (sourceControl is AvaloniaButton ||
             sourceControl is AvaloniaTextBox ||
             sourceControl.FindAncestorOfType<AvaloniaButton>() is not null ||
             sourceControl.FindAncestorOfType<AvaloniaTextBox>() is not null))
        {
            return;
        }

        if (Avalonia.Controls.TopLevel.GetTopLevel(control) is AvaloniaWindow window)
        {
            window.BeginMoveDrag(e);
        }
    }

    private static void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not AvaloniaControl control)
        {
            return;
        }

        if (e.Source is AvaloniaControl sourceControl &&
            (sourceControl is AvaloniaButton ||
             sourceControl is AvaloniaTextBox ||
             sourceControl.FindAncestorOfType<AvaloniaButton>() is not null ||
             sourceControl.FindAncestorOfType<AvaloniaTextBox>() is not null))
        {
            return;
        }

        if (Avalonia.Controls.TopLevel.GetTopLevel(control) is AvaloniaWindow window)
        {
            window.WindowState = window.WindowState == AvaloniaWindowState.Maximized
                ? AvaloniaWindowState.Normal
                : AvaloniaWindowState.Maximized;
        }
    }
}
