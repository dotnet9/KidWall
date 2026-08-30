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
        _overlayKind = ResolveOverlayKind(model);
    }

    public Wallpaper Model { get; }

    public string Id => Model.Id;

    public string Name => Model.Name;

    public string FullPath => Model.FullPath;

    public string Tags => Model.Tags;

    public bool IsDynamic => Model.IsDynamic;

    public bool IsLocal => Model.IsFromLocal;

    public WallpaperCategory Category => Model.Category;

    /// <summary>网格缩略图。</summary>
    public AvaloniaBitmap? Thumb { get; }

    /// <summary>预览大图。</summary>
    public AvaloniaBitmap? Preview { get; }

    /// <summary>动态壁纸的真实预览视频路径；静态壁纸为空。</summary>
    public string? PreviewMediaPath => IsDynamic ? FullPath : null;

    public bool IsTwinkleOverlay => _overlayKind == DynamicOverlayKind.Twinkle;

    public bool IsFlowOverlay => _overlayKind == DynamicOverlayKind.Flow;

    public bool IsAuroraOverlay => _overlayKind == DynamicOverlayKind.Aurora;

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

    private static DynamicOverlayKind ResolveOverlayKind(Wallpaper model)
    {
        if (!model.IsDynamic)
        {
            return DynamicOverlayKind.Flow;
        }

        var hint = $"{model.Name} {model.FullPath}".ToLowerInvariant();
        if (hint.Contains("aurora"))
        {
            return DynamicOverlayKind.Aurora;
        }

        if (hint.Contains("night") || hint.Contains("moon") || hint.Contains("star"))
        {
            return DynamicOverlayKind.Twinkle;
        }

        if (hint.Contains("sky") || hint.Contains("bunny") || hint.Contains("flow"))
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
