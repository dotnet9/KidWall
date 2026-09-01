using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Interactivity;
using KidWall.App.ViewModels;
using System.Diagnostics;

namespace KidWall.App.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _particleTimer;
    private readonly DispatcherTimer _floatTimer;
    private readonly List<Particle> _particles = [];
    private readonly long _floatStartTimestamp = Stopwatch.GetTimestamp();
    private bool _minimizeHover;
    private bool _maximizeHover;
    private bool _closeHover;

    public bool AllowCloseToExit { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        _particleTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => AnimateParticles());
        _floatTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => AnimateHeader());
        PART_MinimizeButton.PointerEntered += (_, _) => _minimizeHover = true;
        PART_MinimizeButton.PointerExited += (_, _) => _minimizeHover = false;
        PART_MaximizeButton.PointerEntered += (_, _) => _maximizeHover = true;
        PART_MaximizeButton.PointerExited += (_, _) => _maximizeHover = false;
        PART_CloseButton.PointerEntered += (_, _) => _closeHover = true;
        PART_CloseButton.PointerExited += (_, _) => _closeHover = false;
        _floatTimer.Start();
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        EmitParticles(PART_MinimizeButton);
        ExecuteWindowCommand(static vm => vm.MinimizeWindowCommand);
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        EmitParticles(PART_MaximizeButton);
        ExecuteWindowCommand(static vm => vm.ToggleMaximizeWindowCommand);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        EmitParticles(PART_CloseButton);
        ExecuteWindowCommand(static vm => vm.CloseWindowCommand);
    }

    private void ExecuteWindowCommand(Func<MainViewModel, System.Windows.Input.ICommand> commandSelector)
    {
        if (DataContext is MainViewModel viewModel)
        {
            var command = commandSelector(viewModel);
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
        }
    }

    private void EmitParticles(Control source)
    {
        if (PART_ParticleLayer is null)
        {
            return;
        }

        var origin = source.TranslatePoint(
            new Avalonia.Point(source.Bounds.Width / 2, source.Bounds.Height / 2),
            PART_ParticleLayer);
        if (origin is null)
        {
            return;
        }

        var glyphs = new[] { "✨", "🌟", "💫", "⭐", "🌈" };
        for (var index = 0; index < 9; index++)
        {
            var glyph = new TextBlock
            {
                Text = glyphs[Random.Shared.Next(glyphs.Length)],
                FontSize = 15,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                Opacity = 1,
                RenderTransform = new TranslateTransform(),
            };

            Canvas.SetLeft(glyph, origin.Value.X);
            Canvas.SetTop(glyph, origin.Value.Y);
            PART_ParticleLayer.Children.Add(glyph);
            _particles.Add(new Particle(
                glyph,
                Stopwatch.GetTimestamp(),
                Random.Shared.NextDouble() * 140 - 70,
                Random.Shared.NextDouble() * -90 - 30));
        }

        if (!_particleTimer.IsEnabled)
        {
            _particleTimer.Start();
        }
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

        SetCandyTransform(PART_MinimizeButton, Math.Sin(angle), _minimizeHover);
        SetCandyTransform(PART_MaximizeButton, Math.Sin(angle + 0.55), _maximizeHover);
        SetCandyTransform(PART_CloseButton, Math.Sin(angle + 1.1), _closeHover);
    }

    private static void SetCandyTransform(Button button, double bob, bool isHovered)
    {
        var scale = isHovered ? 1.2 : 1;
        var rotation = isHovered ? -10 : bob * 2;
        button.RenderTransform = new TransformGroup
        {
            Children =
            {
                new TranslateTransform(0, isHovered ? 0 : -1.5 * (bob + 1)),
                new ScaleTransform(scale, scale),
                new RotateTransform(rotation),
            },
        };
    }

    private void AnimateParticles()
    {
        var now = Stopwatch.GetTimestamp();
        for (var index = _particles.Count - 1; index >= 0; index--)
        {
            var particle = _particles[index];
            var progress = (now - particle.StartTimestamp) / (double)Stopwatch.Frequency / 0.85;
            if (progress >= 1)
            {
                PART_ParticleLayer.Children.Remove(particle.Glyph);
                _particles.RemoveAt(index);
                continue;
            }

            var x = particle.DeltaX * progress;
            var y = particle.DeltaY * progress + 36 * progress * progress;
            particle.Glyph.RenderTransform = new TranslateTransform(x, y);
            particle.Glyph.Opacity = 1 - progress;
        }

        if (_particles.Count == 0)
        {
            _particleTimer.Stop();
        }
    }

    private sealed record Particle(TextBlock Glyph, long StartTimestamp, double DeltaX, double DeltaY);

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
