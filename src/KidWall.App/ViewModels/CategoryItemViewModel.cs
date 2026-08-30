using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KidWall.App.ViewModels;

public partial class CategoryItemViewModel : ObservableObject
{
    private static readonly IBrush InactiveBackground = new SolidColorBrush(Avalonia.Media.Color.Parse("#1AFFFFFF"));
    private static readonly IBrush ActiveBackground = new SolidColorBrush(Avalonia.Media.Color.Parse("#FFD166"));
    private static readonly IBrush InactiveForeground = new SolidColorBrush(Avalonia.Media.Color.Parse("#B8FFB8FF"));
    private static readonly IBrush ActiveForeground = new SolidColorBrush(Avalonia.Media.Color.Parse("#3A2500"));

    public CategoryItemViewModel(
        string key,
        Action<CategoryItemViewModel> onSelect)
    {
        Key = key;
        SelectCommand = new RelayCommand(() => onSelect(this));
    }

    /// <summary>筛选键：all / cartoon / starry / illustration / dynamic / local。</summary>
    public string Key { get; }

    public ICommand SelectCommand { get; }

    /// <summary>胶囊显示名。</summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>页面标题（如「卡通壁纸」）。</summary>
    [ObservableProperty]
    private string _sectionTitle = string.Empty;

    /// <summary>规划中功能（动态壁纸）。</summary>
    [ObservableProperty]
    private bool _isPlanned;

    /// <summary>规划中徽标文案。</summary>
    [ObservableProperty]
    private string _plannedText = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _count;

    public string CountText => Count.ToString();

    /// <summary>选中态背景（active 时高亮）。</summary>
    public IBrush BackgroundBrush => IsSelected ? ActiveBackground : InactiveBackground;

    /// <summary>选中态前景。</summary>
    public IBrush ForegroundBrush => IsSelected ? ActiveForeground : InactiveForeground;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundBrush));
        OnPropertyChanged(nameof(ForegroundBrush));
    }
}
