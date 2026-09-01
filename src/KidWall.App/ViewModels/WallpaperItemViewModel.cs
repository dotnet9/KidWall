using CommunityToolkit.Mvvm.ComponentModel;
using KidWall.Core.Models;
using Lang.Avalonia;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace KidWall.App.ViewModels;

public partial class WallpaperItemViewModel : ObservableObject
{
    private enum DynamicOverlayKind
    {
        Twinkle,
        Flow,
        Aurora,
    }

    public WallpaperItemViewModel(Wallpaper model)
    {
        Model = model;
        Thumb = LoadBitmap(model.ThumbPath, 480);
        Preview = LoadBitmap(model.IsDynamic ? model.ThumbPath : model.FullPath, 1280);
        _isAnimated = ResolveIsAnimated(model);
        _overlayKind = ResolveOverlayKind(model);
    }

    public Wallpaper Model { get; }

    public string Id => Model.Id;

    public string Name => Model.Name;

    public string FullPath => Model.FullPath;

    public string Tags => Model.Tags;

    public string DisplayTags => $"{CategoryDisplayName} · #{CategoryKey}";

    public bool IsDynamic => Model.IsDynamic;

    /// <summary>原型中的播放光效，既可用于视频，也可用于静态壁纸卡片。</summary>
    public bool IsAnimated => _isAnimated;

    public bool IsLocal => Model.IsFromLocal;

    /// <summary>
    /// 卡片是否自动播放视频。预览对话框打开时暂停，避免原生视频浮层窗口
    /// 盖住预览对话框。
    /// </summary>
    public bool AutoPlay => IsDynamic && !PreviewDialogOpen;

    /// <summary>预览对话框是否打开（由主界面同步）。</summary>
    [ObservableProperty]
    private bool _previewDialogOpen;

    partial void OnPreviewDialogOpenChanged(bool value) => OnPropertyChanged(nameof(AutoPlay));

    public WallpaperCategory Category => Model.Category;

    public string CategoryKey => Category switch
    {
        WallpaperCategory.Cartoon => "cartoon",
        WallpaperCategory.Starry => "starry",
        WallpaperCategory.Illustration => "illustration",
        WallpaperCategory.Dynamic => "dynamic",
        WallpaperCategory.Local => "local",
        _ => "wallpaper",
    };

    public string CategoryDisplayName => Category switch
    {
        WallpaperCategory.Cartoon => "卡通",
        WallpaperCategory.Starry => "星空",
        WallpaperCategory.Illustration => "插画",
        WallpaperCategory.Dynamic => "动态壁纸",
        WallpaperCategory.Local => "本地",
        _ => "壁纸",
    };

    /// <summary>网格缩略图。</summary>
    public AvaloniaBitmap? Thumb { get; }

    /// <summary>预览大图。</summary>
    public AvaloniaBitmap? Preview { get; }

    /// <summary>动态壁纸的真实预览视频路径；静态壁纸为空。</summary>
    public string? PreviewMediaPath => IsDynamic ? FullPath : null;

    public bool IsTwinkleOverlay => IsAnimated && _overlayKind == DynamicOverlayKind.Twinkle;

    public bool IsFlowOverlay => IsAnimated && _overlayKind == DynamicOverlayKind.Flow;

    public bool IsAuroraOverlay => IsAnimated && _overlayKind == DynamicOverlayKind.Aurora;

    /// <summary>是否当前正在使用的桌面壁纸。</summary>
    [ObservableProperty]
    private bool _isCurrent;

    /// <summary>是否已收藏。</summary>
    [ObservableProperty]
    private bool _isFavorite;

    /// <summary>收藏按钮文案。</summary>
    public string FavoriteText => IsFavorite
        ? I18nManager.Instance.GetResource(Localization.Preview.Labels.Favorited)
        : I18nManager.Instance.GetResource(Localization.Preview.Labels.Favorite);

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteText));
    }

    private readonly DynamicOverlayKind _overlayKind;
    private readonly bool _isAnimated;

    private static bool ResolveIsAnimated(Wallpaper model)
    {
        if (model.IsDynamic)
        {
            return true;
        }

        var hint = $"{model.Name} {model.FullPath}".ToLowerInvariant();
        return hint.Contains("moon-stars")
            || hint.Contains("rocket-space")
            || hint.Contains("galaxy-slide")
            || hint.Contains("whale-stars");
    }

    private static DynamicOverlayKind ResolveOverlayKind(Wallpaper model)
    {
        var hint = $"{model.Name} {model.FullPath}".ToLowerInvariant();
        if (hint.Contains("salzdahlum") || hint.Contains("night-sky") || hint.Contains("galaxy-slide"))
        {
            return DynamicOverlayKind.Flow;
        }

        if (hint.Contains("aurora"))
        {
            return DynamicOverlayKind.Aurora;
        }

        if (hint.Contains("moon-stars") || hint.Contains("rocket-space") || hint.Contains("sky-day-night"))
        {
            return DynamicOverlayKind.Twinkle;
        }

        if (hint.Contains("whale-stars"))
        {
            return DynamicOverlayKind.Aurora;
        }

        if (hint.Contains("flow"))
        {
            return DynamicOverlayKind.Flow;
        }

        return hint.Length % 2 == 0
            ? DynamicOverlayKind.Flow
            : DynamicOverlayKind.Twinkle;
    }

    private static AvaloniaBitmap? LoadBitmap(string path, int decodeWidth)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return AvaloniaBitmap.DecodeToWidth(stream, decodeWidth);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
