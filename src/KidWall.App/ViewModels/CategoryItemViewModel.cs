using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KidWall.App.ViewModels;

public partial class CategoryItemViewModel : ObservableObject
{
    public CategoryItemViewModel(
        string key,
        Action<CategoryItemViewModel> onSelect)
    {
        Key = key;
        Icon = key switch
        {
            "all" => "🪄",
            "recommended" => "🌟",
            "cartoon" => "🐣",
            "starry" => "🌙",
            "illustration" => "🎨",
            "dynamic" => "🎞️",
            "local" => "📁",
            _ => string.Empty,
        };
        SelectCommand = new RelayCommand(() => onSelect(this));
    }

    /// <summary>筛选键：all / recommended / cartoon / starry / illustration / dynamic / local。</summary>
    public string Key { get; }

    /// <summary>原型中的分类图标。</summary>
    public string Icon { get; }

    public ICommand SelectCommand { get; }

    /// <summary>胶囊显示名。</summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>页面标题（如「卡通壁纸」）。</summary>
    [ObservableProperty]
    private string _sectionTitle = string.Empty;

    /// <summary>规划中功能（保留给未来的分类）。</summary>
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

}
