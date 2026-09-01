using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System.Windows.Input;
using LibVLC = LibVLCSharp.Shared.LibVLC;
using LibVLCSharp.Shared;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using AvaloniaUserControl = Avalonia.Controls.UserControl;
using AvaloniaVideoView = LibVLCSharp.Avalonia.VideoView;
using AvaloniaRoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace KidWall.App.Controls;

public partial class DynamicWallpaperPreviewView : AvaloniaUserControl
{
    private static readonly Lazy<LibVLC?> SharedLibVlc = new(() =>
    {
        try
        {
            return new LibVLC("--no-video-title-show", "--quiet");
        }
        catch
        {
            return null;
        }
    });

    private readonly DispatcherTimer _hoverTimer;
    private readonly Border _overlayPlaceholder;
    private AvaloniaVideoView? _videoView;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private bool _isLoaded;
    private bool _isHovering;

    public DynamicWallpaperPreviewView()
    {
        InitializeComponent();

        _hoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(HoverDelayMilliseconds)
        };
        _hoverTimer.Tick += OnHoverTimerTick;

        _videoView = PART_VideoView;
        _overlayPlaceholder = new Border
        {
            Width = 1,
            Height = 1,
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
        };
        // Keep Content non-null so LibVLCSharp creates its floating overlay
        // window before a data-bound card overlay arrives.
        _videoView.Content = _overlayPlaceholder;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
    }

    public static readonly StyledProperty<string?> MediaPathProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, string?>(nameof(MediaPath));

    public string? MediaPath
    {
        get => GetValue(MediaPathProperty);
        set => SetValue(MediaPathProperty, value);
    }

    public static readonly StyledProperty<Control?> OverlayContentProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, Control?>(nameof(OverlayContent));

    /// <summary>
    /// Optional content rendered in LibVLCSharp's top-level overlay window.
    /// Native video HWNDs otherwise cover Avalonia siblings.
    /// </summary>
    public Control? OverlayContent
    {
        get => GetValue(OverlayContentProperty);
        set => SetValue(OverlayContentProperty, value);
    }

    public static readonly StyledProperty<ICommand?> PreviewCommandProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, ICommand?>(nameof(PreviewCommand));

    public ICommand? PreviewCommand
    {
        get => GetValue(PreviewCommandProperty);
        set => SetValue(PreviewCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> ApplyCommandProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, ICommand?>(nameof(ApplyCommand));

    public ICommand? ApplyCommand
    {
        get => GetValue(ApplyCommandProperty);
        set => SetValue(ApplyCommandProperty, value);
    }

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, object?>(nameof(CommandParameter));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly StyledProperty<string?> PreviewTextProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, string?>(nameof(PreviewText));

    public string? PreviewText
    {
        get => GetValue(PreviewTextProperty);
        set => SetValue(PreviewTextProperty, value);
    }

    public static readonly StyledProperty<string?> ApplyTextProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, string?>(nameof(ApplyText));

    public string? ApplyText
    {
        get => GetValue(ApplyTextProperty);
        set => SetValue(ApplyTextProperty, value);
    }

    public static readonly StyledProperty<string?> CurrentTextProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, string?>(nameof(CurrentText));

    public string? CurrentText
    {
        get => GetValue(CurrentTextProperty);
        set => SetValue(CurrentTextProperty, value);
    }

    public static readonly StyledProperty<string?> DynamicTextProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, string?>(nameof(DynamicText));

    public string? DynamicText
    {
        get => GetValue(DynamicTextProperty);
        set => SetValue(DynamicTextProperty, value);
    }

    public static readonly StyledProperty<AvaloniaBitmap?> PosterSourceProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, AvaloniaBitmap?>(nameof(PosterSource));

    public AvaloniaBitmap? PosterSource
    {
        get => GetValue(PosterSourceProperty);
        set => SetValue(PosterSourceProperty, value);
    }

    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, bool>(nameof(AutoPlay), false);

    public bool AutoPlay
    {
        get => GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    public static readonly StyledProperty<bool> EnableHoverPreviewProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, bool>(nameof(EnableHoverPreview), true);

    public bool EnableHoverPreview
    {
        get => GetValue(EnableHoverPreviewProperty);
        set => SetValue(EnableHoverPreviewProperty, value);
    }

    public static readonly StyledProperty<bool> LoopProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, bool>(nameof(Loop), true);

    public bool Loop
    {
        get => GetValue(LoopProperty);
        set => SetValue(LoopProperty, value);
    }

    public static readonly StyledProperty<int> HoverDelayMillisecondsProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, int>(nameof(HoverDelayMilliseconds), 180);

    public int HoverDelayMilliseconds
    {
        get => GetValue(HoverDelayMillisecondsProperty);
        set
        {
            SetValue(HoverDelayMillisecondsProperty, value);
            _hoverTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, value));
        }
    }

    public static readonly StyledProperty<bool> IsPosterVisibleProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, bool>(nameof(IsPosterVisible), true);

    public bool IsPosterVisible
    {
        get => GetValue(IsPosterVisibleProperty);
        private set => SetValue(IsPosterVisibleProperty, value);
    }

    public static readonly StyledProperty<bool> IsVideoVisibleProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, bool>(nameof(IsVideoVisible), false);

    public bool IsVideoVisible
    {
        get => GetValue(IsVideoVisibleProperty);
        private set => SetValue(IsVideoVisibleProperty, value);
    }

    public static readonly StyledProperty<bool> IsVideoHostVisibleProperty =
        AvaloniaProperty.Register<DynamicWallpaperPreviewView, bool>(nameof(IsVideoHostVisible), false);

    /// <summary>
    /// Keeps the native video HWND alive before playback starts. LibVLC must
    /// receive that HWND before Play; otherwise it opens its own output window.
    /// </summary>
    public bool IsVideoHostVisible
    {
        get => GetValue(IsVideoHostVisibleProperty);
        private set => SetValue(IsVideoHostVisibleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MediaPathProperty ||
            change.Property == AutoPlayProperty ||
            change.Property == EnableHoverPreviewProperty ||
            change.Property == LoopProperty)
        {
            if (change.Property == MediaPathProperty)
            {
                UpdateVideoHostVisibility();
            }
            else if (change.Property == AutoPlayProperty || change.Property == EnableHoverPreviewProperty)
            {
                UpdateVideoHostVisibility();
            }

            ApplyOverlayContent();
            if (_isLoaded)
            {
                ApplyMediaState();
            }
        }

        if (change.Property == OverlayContentProperty)
        {
            ApplyOverlayContent();
        }

        if (change.Property == PreviewCommandProperty ||
            change.Property == ApplyCommandProperty ||
            change.Property == CommandParameterProperty ||
            change.Property == PreviewTextProperty ||
            change.Property == ApplyTextProperty ||
            change.Property == CurrentTextProperty ||
            change.Property == DynamicTextProperty ||
            change.Property == DataContextProperty)
        {
            SyncOverlayProperties();
        }

        if (change.Property == IsVisibleProperty && _isLoaded)
        {
            if (!IsVisible)
            {
                _isHovering = false;
                _hoverTimer.Stop();
                StopPlayback();
            }
            else
            {
                ApplyMediaState();
            }
        }
    }

    private void OnLoaded(object? sender, AvaloniaRoutedEventArgs e)
    {
        _isLoaded = true;
        UpdateVideoHostVisibility();
        ApplyOverlayContent();
        AttachMediaPlayer();
        ApplyMediaState();
    }

    private void OnUnloaded(object? sender, AvaloniaRoutedEventArgs e)
    {
        _isLoaded = false;
        _isHovering = false;
        _hoverTimer.Stop();
        StopPlayback();
        ApplyOverlayContent();
        DisposeMediaPlayer();
    }

    private void ApplyOverlayContent()
    {
        if (_videoView is null)
        {
            return;
        }

        var shouldShowOverlay = AutoPlay && !string.IsNullOrWhiteSpace(MediaPath);
        _videoView.Content = shouldShowOverlay
            ? OverlayContent ?? _overlayPlaceholder
            : _overlayPlaceholder;
        SyncOverlayProperties();
    }

    private void SyncOverlayProperties()
    {
        if (_videoView?.Content is not DynamicCardOverlay overlay)
        {
            return;
        }

        // VideoView hosts Content in a separate top-level window. Keep the
        // item context explicit instead of relying on inheritance across
        // that window boundary.
        overlay.DataContext = DataContext;
        overlay.PreviewCommand = PreviewCommand;
        overlay.ApplyCommand = ApplyCommand;
        overlay.CommandParameter = CommandParameter ?? DataContext;
        overlay.PreviewText = PreviewText;
        overlay.ApplyText = ApplyText;
        overlay.CurrentText = CurrentText;
        overlay.DynamicText = DynamicText;
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!EnableHoverPreview || AutoPlay || string.IsNullOrWhiteSpace(MediaPath))
        {
            return;
        }

        _isHovering = true;
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, HoverDelayMilliseconds));
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!EnableHoverPreview || AutoPlay)
        {
            return;
        }

        _isHovering = false;
        _hoverTimer.Stop();
        StopPlayback();
    }

    private void UpdateVideoHostVisibility()
    {
        // Keep native video hosts out of gallery cards. The prototype uses
        // lightweight visual effects in the gallery; real video is reserved
        // for the preview surface and desktop wallpaper playback.
        IsVideoHostVisible = !string.IsNullOrWhiteSpace(MediaPath) && (AutoPlay || EnableHoverPreview);
    }

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (_isHovering)
        {
            StartPlayback();
        }
    }

    private void ApplyMediaState()
    {
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, HoverDelayMilliseconds));

        if (AutoPlay)
        {
            StartPlayback();
            return;
        }

        StopPlayback();
    }

    private void AttachMediaPlayer()
    {
        if (_videoView is null || (!AutoPlay && !EnableHoverPreview))
        {
            return;
        }

        EnsureMediaPlayer();
        _videoView.MediaPlayer = _mediaPlayer;
    }

    private void EnsureMediaPlayer()
    {
        var libVlc = SharedLibVlc.Value;
        if (libVlc is null)
        {
            return;
        }

        _mediaPlayer ??= new MediaPlayer(libVlc);
        _mediaPlayer.Mute = true;
        _mediaPlayer.Volume = 0;
        _mediaPlayer.Playing -= OnMediaPlayerPlaying;
        _mediaPlayer.Stopped -= OnMediaPlayerStopped;
        _mediaPlayer.EndReached -= OnMediaPlayerEnded;
        _mediaPlayer.EncounteredError -= OnMediaPlayerError;
        _mediaPlayer.Playing += OnMediaPlayerPlaying;
        _mediaPlayer.Stopped += OnMediaPlayerStopped;
        _mediaPlayer.EndReached += OnMediaPlayerEnded;
        _mediaPlayer.EncounteredError += OnMediaPlayerError;
        if (_videoView is not null)
        {
            _videoView.MediaPlayer = _mediaPlayer;
        }
    }

    private void StartPlayback()
    {
        if (string.IsNullOrWhiteSpace(MediaPath))
        {
            StopPlayback();
            return;
        }

        EnsureMediaPlayer();
        if (_mediaPlayer is null)
        {
            StopPlayback();
            return;
        }

        try
        {
            StopCurrentMedia(showPoster: true);

            var fullPath = Path.GetFullPath(MediaPath);
            if (!File.Exists(fullPath))
            {
                StopPlayback();
                return;
            }

            var libVlc = SharedLibVlc.Value;
            if (libVlc is null)
            {
                StopPlayback();
                return;
            }

            var media = new Media(
                libVlc,
                new Uri(fullPath, UriKind.Absolute));
            if (Loop)
            {
                media.AddOption(":input-repeat=65535");
            }

            media.AddOption(":no-audio");

            _media = media;
            // Keep the poster above the video until LibVLC confirms that a
            // decoded frame is actually being presented.
            IsVideoVisible = true;
            IsPosterVisible = true;

            if (!_mediaPlayer.Play(media))
            {
                StopPlayback();
            }
        }
        catch
        {
            StopPlayback();
        }
    }

    private void StopPlayback()
    {
        StopCurrentMedia(showPoster: true);
    }

    private void StopCurrentMedia(bool showPoster)
    {
        try
        {
            _mediaPlayer?.Stop();
        }
        catch
        {
        }

        try
        {
            _media?.Dispose();
        }
        catch
        {
        }

        _media = null;
        IsVideoVisible = false;
        if (showPoster)
        {
            IsPosterVisible = true;
        }
    }

    private void DisposeMediaPlayer()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        _mediaPlayer.Playing -= OnMediaPlayerPlaying;
        _mediaPlayer.Stopped -= OnMediaPlayerStopped;
        _mediaPlayer.EndReached -= OnMediaPlayerEnded;
        _mediaPlayer.EncounteredError -= OnMediaPlayerError;

        try
        {
            _mediaPlayer.Stop();
        }
        catch
        {
        }

        try
        {
            _mediaPlayer.Dispose();
        }
        catch
        {
        }

        _mediaPlayer = null;
        if (_videoView is not null)
        {
            _videoView.MediaPlayer = null;
        }
    }

    private void OnMediaPlayerPlaying(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, _mediaPlayer) || _media is null || _mediaPlayer?.IsPlaying != true)
            {
                return;
            }

            IsPosterVisible = false;
            IsVideoVisible = true;
        });
    }

    private void OnMediaPlayerStopped(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, _mediaPlayer) || _mediaPlayer?.IsPlaying == true)
            {
                return;
            }

            IsVideoVisible = false;
            IsPosterVisible = true;
        });
    }

    private void OnMediaPlayerEnded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, _mediaPlayer) || Loop)
            {
                return;
            }

            IsVideoVisible = false;
            IsPosterVisible = true;
        });
    }

    private void OnMediaPlayerError(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, _mediaPlayer))
            {
                return;
            }

            IsVideoVisible = false;
            IsPosterVisible = true;
        });
    }
}
