using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace KidWall.App.Controls;

public partial class DynamicCardOverlay : UserControl
{
    public static readonly StyledProperty<bool> IsActionsVisibleProperty =
        AvaloniaProperty.Register<DynamicCardOverlay, bool>(nameof(IsActionsVisible));

    public static readonly StyledProperty<ICommand?> PreviewCommandProperty =
        AvaloniaProperty.Register<DynamicCardOverlay, ICommand?>(nameof(PreviewCommand));

    public static readonly StyledProperty<ICommand?> ApplyCommandProperty =
        AvaloniaProperty.Register<DynamicCardOverlay, ICommand?>(nameof(ApplyCommand));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<DynamicCardOverlay, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<string?> PreviewTextProperty =
        AvaloniaProperty.Register<DynamicCardOverlay, string?>(nameof(PreviewText));

    public static readonly StyledProperty<string?> ApplyTextProperty =
        AvaloniaProperty.Register<DynamicCardOverlay, string?>(nameof(ApplyText));

    public static readonly StyledProperty<string?> CurrentTextProperty =
        AvaloniaProperty.Register<DynamicCardOverlay, string?>(nameof(CurrentText));

    public static readonly StyledProperty<string?> DynamicTextProperty =
        AvaloniaProperty.Register<DynamicCardOverlay, string?>(nameof(DynamicText));

    public ICommand? PreviewCommand
    {
        get => GetValue(PreviewCommandProperty);
        set => SetValue(PreviewCommandProperty, value);
    }

    public ICommand? ApplyCommand
    {
        get => GetValue(ApplyCommandProperty);
        set => SetValue(ApplyCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public string? PreviewText
    {
        get => GetValue(PreviewTextProperty);
        set => SetValue(PreviewTextProperty, value);
    }

    public string? ApplyText
    {
        get => GetValue(ApplyTextProperty);
        set => SetValue(ApplyTextProperty, value);
    }

    public string? CurrentText
    {
        get => GetValue(CurrentTextProperty);
        set => SetValue(CurrentTextProperty, value);
    }

    public string? DynamicText
    {
        get => GetValue(DynamicTextProperty);
        set => SetValue(DynamicTextProperty, value);
    }

    public bool IsActionsVisible
    {
        get => GetValue(IsActionsVisibleProperty);
        private set => SetValue(IsActionsVisibleProperty, value);
    }

    public DynamicCardOverlay()
    {
        InitializeComponent();
        PointerEntered += (_, _) => IsActionsVisible = true;
        PointerExited += (_, _) => IsActionsVisible = false;
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || IsInsideButton(e.Source as Visual))
        {
            return;
        }

        if (PreviewCommand?.CanExecute(CommandParameter) == true)
        {
            PreviewCommand.Execute(CommandParameter);
            e.Handled = true;
        }
    }

    private static bool IsInsideButton(Visual? source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Button)
            {
                return true;
            }
        }

        return false;
    }
}
