using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LibVLC = LibVLCSharp.Shared.LibVLC;
using LibVLCSharp.Avalonia;
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MediaPathProperty ||
            change.Property == AutoPlayProperty ||
            change.Property == EnableHoverPreviewProperty ||
            change.Property == LoopProperty)
        {
            if (_isLoaded)
            {
                ApplyMediaState();
            }
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
        AttachMediaPlayer();
        ApplyMediaState();
    }

    private void OnUnloaded(object? sender, AvaloniaRoutedEventArgs e)
    {
        _isLoaded = false;
        _isHovering = false;
        _hoverTimer.Stop();
        StopPlayback();
        DisposeMediaPlayer();
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
        if (_videoView is null)
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

            _media = media;
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
            IsPosterVisible = false;
            IsVideoVisible = true;
        });
    }

    private void OnMediaPlayerStopped(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsVideoVisible = false;
            IsPosterVisible = true;
        });
    }

    private void OnMediaPlayerEnded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsVideoVisible = false;
            IsPosterVisible = true;
        });
    }

    private void OnMediaPlayerError(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsVideoVisible = false;
            IsPosterVisible = true;
        });
    }
}
