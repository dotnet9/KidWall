using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using KidWall.Core.Models;

namespace KidWall.App.ViewModels;

public partial class WallpaperItemViewModel : ObservableObject
{
    public WallpaperItemViewModel(Wallpaper model)
    {
        Model = model;
        Thumb = LoadBitmap(model.ThumbPath, 480);
        Preview = LoadBitmap(model.FullPath, 1280);
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
