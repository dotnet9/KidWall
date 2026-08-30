using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using KidWall.Core.Models;
using Lang.Avalonia;

namespace KidWall.App.ViewModels;

public partial class WallpaperItemViewModel : ObservableObject
{
    public WallpaperItemViewModel(Wallpaper model)
    {
        Model = model;
        Thumb = LoadBitmap(model.ThumbPath, 480);
        // 动态壁纸是视频文件，不能用 Bitmap 解码；预览大图由视频播放器呈现
        Preview = model.IsDynamic ? null : LoadBitmap(model.FullPath, 1280);
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
    public Bitmap? Thumb { get; }

    /// <summary>预览大图。</summary>
    public Bitmap? Preview { get; }

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

    private static Bitmap? LoadBitmap(string path, int decodeWidth)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, decodeWidth);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
