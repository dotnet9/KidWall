using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KidWall.App.Services;
using KidWall.Core.Models;
using KidWall.Core.Services;
using System.Collections.ObjectModel;

namespace KidWall.App.ViewModels;

public partial class MainViewModel : LocalizedViewModel
{
    private const string KeyAll = "all";
    private const string KeyRecommended = "recommended";
    private const string KeyCartoon = "cartoon";
    private const string KeyStarry = "starry";
    private const string KeyIllustration = "illustration";
    private const string KeyDynamic = "dynamic";
    private const string KeyLocal = "local";

    private readonly AppPreferences _preferences;
    private readonly AppPreferencesStore _preferencesStore;
    private readonly IDesktopWallpaperService _wallpaperService;
    private readonly IDynamicWallpaperService _dynamicWallpaperService;
    private readonly IMainWindowService _mainWindowService;
    private readonly string _resDirectory;
    private readonly DispatcherTimer _rotateTimer;
    private readonly DispatcherTimer _messageTimer;
    private readonly List<string> _localFolders = [];

    private List<WallpaperItemViewModel> _allItems = [];
    private string _appliedWallpaperId = string.Empty;
    private bool _restoredInitialWallpaper;

    public MainViewModel(
        AppPreferences preferences,
        AppPreferencesStore preferencesStore,
        IDesktopWallpaperService wallpaperService,
        IDynamicWallpaperService dynamicWallpaperService,
        IMainWindowService mainWindowService,
        string resDirectory)
    {
        _preferences = preferences;
        _preferencesStore = preferencesStore;
        _wallpaperService = wallpaperService;
        _dynamicWallpaperService = dynamicWallpaperService;
        _mainWindowService = mainWindowService;
        _resDirectory = resDirectory;

        Categories.Add(new CategoryItemViewModel(KeyAll, OnCategorySelected));
        Categories.Add(new CategoryItemViewModel(KeyRecommended, OnCategorySelected));
        Categories.Add(new CategoryItemViewModel(KeyCartoon, OnCategorySelected));
        Categories.Add(new CategoryItemViewModel(KeyStarry, OnCategorySelected));
        Categories.Add(new CategoryItemViewModel(KeyIllustration, OnCategorySelected));
        Categories.Add(new CategoryItemViewModel(KeyDynamic, OnCategorySelected));
        Categories.Add(new CategoryItemViewModel(KeyLocal, OnCategorySelected));

        _rotateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(Math.Max(1, _preferences.AutoRotateIntervalMinutes)) };
        _rotateTimer.Tick += (_, _) => RotateOnce();

        _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
        _messageTimer.Tick += (_, _) =>
        {
            _messageTimer.Stop();
            StatusMessage = null;
        };

        RefreshLocalizedText();
        SelectedCategory = Categories[0];
        Categories[0].IsSelected = true;
        SyncAutoRotateTimer();
        _ = LoadAsync();
    }

    public string WindowTitle => L(Localization.Shell.Window.Title);

    public string WindowSubtitle => L(Localization.Shell.Window.Subtitle);

    public string SearchPlaceholder => L(Localization.Shell.Search.Placeholder);

    public string LocalButtonText => L(Localization.Shell.Actions.Local);

    public string SettingsButtonText => L(Localization.Shell.Actions.Settings);

    public string MinimizeTip => L(Localization.Shell.Controls.Minimize);

    public string MaximizeTip => L(Localization.Shell.Controls.Maximize);

    public string CloseTip => L(Localization.Shell.Controls.Close);

    public string PreviewButtonText => L(Localization.Main.Card.Preview);

    public string SetWallpaperButtonText => L(Localization.Main.Card.SetWallpaper);

    public string CurrentBadgeText => L(Localization.Main.Card.Current);

    public string DynamicBadgeText => L(Localization.Main.Card.Dynamic);

    public string PreviewResolution => L(Localization.Preview.Labels.Resolution);

    public string ApplyWallpaperText => L(Localization.Preview.Labels.Apply);

    public string SettingsTitle => L(Localization.Settings.Page.Title);

    public string SettingsSubtitle => L(Localization.Settings.Page.Subtitle);

    public string AutoStartTitle => L(Localization.Settings.AutoStart.Title);

    public string AutoStartDesc => L(Localization.Settings.AutoStart.Desc);

    public string AutoRotateTitle => L(Localization.Settings.AutoRotate.Title);

    public string AutoRotateDesc => string.Format(
        L(Localization.Settings.AutoRotate.Desc),
        Math.Max(1, _preferences.AutoRotateIntervalMinutes));

    public string BatterySaverTitle => L(Localization.Settings.BatterySaver.Title);

    public string BatterySaverDesc => L(Localization.Settings.BatterySaver.Desc);

    public string ContentFilterTitle => L(Localization.Settings.ContentFilter.Title);

    public string ContentFilterDesc => L(Localization.Settings.ContentFilter.Desc);

    public string SaveSettingsText => L(Localization.Settings.Page.Save);

    public string EmptyText => L(Localization.Main.Empty.Text);

    public string PlannedText => L(Localization.Main.Card.Planned);

    public ObservableCollection<CategoryItemViewModel> Categories { get; } = [];

    public ObservableCollection<WallpaperItemViewModel> Wallpapers { get; } = [];

    /// <summary>是否显示“动态壁纸规划中”空状态提示。</summary>
    public bool ShowDynamicPlanned => SelectedCategory?.Key == KeyDynamic && Wallpapers.Count == 0;

    /// <summary>是否显示普通空状态。</summary>
    public bool ShowEmpty => !IsLoading && !ShowDynamicPlanned && Wallpapers.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowDynamicPlanned))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CategoryItemViewModel? _selectedCategory;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewOpen))]
    private WallpaperItemViewModel? _previewItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusVisible))]
    private string? _statusMessage;

    public string SectionTitle => SelectedCategory?.SectionTitle ?? string.Empty;

    public string SectionIcon => SelectedCategory?.Icon ?? string.Empty;

    public string CountText => string.Format(L(Localization.Main.Labels.Count), Wallpapers.Count);

    public string DynamicPlannedText => L(Localization.Main.Status.DynamicPlanned);

    public bool IsPreviewOpen => PreviewItem is not null;

    public bool IsStatusVisible => !string.IsNullOrWhiteSpace(StatusMessage);

    // ---------- 设置项（直接绑定到偏好，变化即保存） ----------

    public bool AutoStart
    {
        get => _preferences.AutoStart;
        set
        {
            if (_preferences.AutoStart == value)
            {
                return;
            }

            _preferences.AutoStart = value;
            SyncAutoStart();
            _preferencesStore.Save(_preferences);
            OnPropertyChanged();
        }
    }

    public bool AutoRotate
    {
        get => _preferences.AutoRotate;
        set
        {
            if (_preferences.AutoRotate == value)
            {
                return;
            }

            _preferences.AutoRotate = value;
            SyncAutoRotateTimer();
            _preferencesStore.Save(_preferences);
            OnPropertyChanged();
        }
    }

    public bool BatterySaver
    {
        get => _preferences.DynamicBatterySaver;
        set
        {
            if (_preferences.DynamicBatterySaver == value)
            {
                return;
            }

            _preferences.DynamicBatterySaver = value;
            _preferencesStore.Save(_preferences);
            OnPropertyChanged();
        }
    }

    public bool ContentFilter
    {
        get => _preferences.ContentFilter;
        set
        {
            if (_preferences.ContentFilter == value)
            {
                return;
            }

            _preferences.ContentFilter = value;
            _preferencesStore.Save(_preferences);
            OnPropertyChanged();
        }
    }

    // ---------- 命令 ----------

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var sources = new List<IWallpaperSource> { new BuiltInWallpaperSource(_resDirectory) };
            sources.AddRange(_localFolders.Select(folder => new LocalWallpaperSource(folder)));

            var items = new List<WallpaperItemViewModel>();
            foreach (var source in sources)
            {
                var wallpapers = await source.LoadAsync();
                items.AddRange(wallpapers.Select(w => new WallpaperItemViewModel(w)));
            }

            _allItems = items;
            foreach (var item in items)
            {
                item.IsCurrent = item.Id == _appliedWallpaperId;
            }

            RefreshFilter();
            RestoreInitialWallpaper();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ApplyWallpaper(WallpaperItemViewModel item)
    {
        ApplyWallpaperCore(item, showStatus: true, persistPreferences: true);
    }

    private bool ApplyWallpaperCore(WallpaperItemViewModel item, bool showStatus, bool persistPreferences)
    {
        try
        {
            if (item.IsDynamic)
            {
                // 动态壁纸：全屏视频挂载到桌面壁纸层（WorkerW）
                if (!_dynamicWallpaperService.Show(item.FullPath))
                {
                    if (showStatus)
                    {
                        ShowStatus($"动态壁纸设置失败：{item.Name}");
                    }

                    return false;
                }

                foreach (var wallpaper in _allItems)
                {
                    wallpaper.IsCurrent = false;
                }

                item.IsCurrent = true;
                _appliedWallpaperId = item.Id;
                if (persistPreferences)
                {
                    SaveLastAppliedWallpaper(item);
                }

                if (showStatus)
                {
                    ShowStatus($"🎬 动态壁纸已应用：{item.Name}");
                }

                return true;
            }

            // 静态壁纸：先把系统桌面壁纸切过去，再收起动态宿主，避免中间闪底色。
            if (!_wallpaperService.SetWallpaper(item.FullPath))
            {
                if (showStatus)
                {
                    ShowStatus("设置壁纸失败：文件不可用 🥺");
                }

                return false;
            }

            _dynamicWallpaperService.Stop();

            foreach (var wallpaper in _allItems)
            {
                wallpaper.IsCurrent = false;
            }

            item.IsCurrent = true;
            _appliedWallpaperId = item.Id;
            if (persistPreferences)
            {
                SaveLastAppliedWallpaper(item);
            }

            if (showStatus)
            {
                ShowStatus(string.Format(L(Localization.Toast.Messages.Applied), item.Name));
            }

            return true;
        }
        catch (Exception)
        {
            if (showStatus)
            {
                ShowStatus($"应用壁纸失败：{item.Name}");
            }

            return false;
        }
    }

    [RelayCommand]
    private void OpenPreview(WallpaperItemViewModel item) => PreviewItem = item;

    [RelayCommand]
    private void ClosePreview() => PreviewItem = null;

    [RelayCommand]
    private void ToggleFavorite(WallpaperItemViewModel item)
    {
        item.IsFavorite = !item.IsFavorite;
        ShowStatus(item.IsFavorite ? $"💛 已收藏：{item.Name}" : $"收藏已取消：{item.Name}");
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    private void MinimizeWindow() => _mainWindowService.Minimize();

    [RelayCommand]
    private void ToggleMaximizeWindow() => _mainWindowService.ToggleMaximize();

    [RelayCommand]
    private void CloseWindow() => _mainWindowService.CloseToTray();

    [RelayCommand]
    private async Task PickLocalFolderAsync()
    {
        var folder = await _mainWindowService.PickLocalFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            await AddLocalFolderAsync(folder);
        }
    }

    [RelayCommand]
    private void PrepareToClose()
    {
        PreviewItem = null;
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _preferencesStore.Save(_preferences);
        IsSettingsOpen = false;
        ShowStatus(L(Localization.Toast.Messages.Saved));
    }

    // ---------- 本地文件夹 ----------

    public async Task AddLocalFolderAsync(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || _localFolders.Contains(folder))
        {
            return;
        }

        _localFolders.Add(folder);
        await LoadAsync();
        var localCount = _allItems.Count(item => item.IsLocal);
        ShowStatus(string.Format(L(Localization.Toast.Messages.LocalImported), localCount));
    }

    // ---------- 内部 ----------

    private void OnCategorySelected(CategoryItemViewModel category)
    {
        foreach (var item in Categories)
        {
            item.IsSelected = ReferenceEquals(item, category);
        }

        SelectedCategory = category;
        RefreshFilter();
    }

    private static bool MatchCategory(WallpaperItemViewModel item, string key) => key switch
    {
        KeyRecommended => item.Model.IsRecommended,
        KeyCartoon => item.Category == WallpaperCategory.Cartoon,
        KeyStarry => item.Category == WallpaperCategory.Starry,
        KeyIllustration => item.Category == WallpaperCategory.Illustration,
        KeyDynamic => item.IsDynamic,
        KeyLocal => item.IsLocal,
        _ => true,
    };

    private void RefreshFilter()
    {
        var key = SelectedCategory?.Key ?? KeyAll;
        Wallpapers.Clear();
        foreach (var item in _allItems)
        {
            if (MatchCategory(item, key) && item.Model.MatchesKeyword(SearchText))
            {
                Wallpapers.Add(item);
            }
        }

        UpdateCategoryCounts();
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionIcon));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowDynamicPlanned));
    }

    private void UpdateCategoryCounts()
    {
        foreach (var category in Categories)
        {
            category.Count = category.Key switch
            {
                KeyAll => _allItems.Count,
                _ => _allItems.Count(item => MatchCategory(item, category.Key)),
            };
        }
    }
    private void RotateOnce()
    {
        var candidates = Wallpapers.Where(item => item.Id != _appliedWallpaperId).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var next = candidates[Random.Shared.Next(candidates.Count)];
        ApplyWallpaper(next);
        ShowStatus(string.Format(L(Localization.Toast.Messages.AutoRotated), next.Name));
    }

    private void SyncAutoStart()
    {
        try
        {
            if (_preferences.AutoStart)
            {
                AutoStartManager.Enable(Environment.ProcessPath ?? AppContext.BaseDirectory);
            }
            else
            {
                AutoStartManager.Disable();
            }
        }
        catch (Exception)
        {
            // 注册表写入失败不阻塞使用
        }
    }

    private void SyncAutoRotateTimer()
    {
        if (_preferences.AutoRotate)
        {
            _rotateTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, _preferences.AutoRotateIntervalMinutes));
            _rotateTimer.Start();
        }
        else
        {
            _rotateTimer.Stop();
        }
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
        _messageTimer.Stop();
        _messageTimer.Start();
    }

    private void SaveLastAppliedWallpaper(WallpaperItemViewModel item)
    {
        _preferences.LastAppliedWallpaperId = item.Id;
        _preferences.LastAppliedWallpaperPath = item.FullPath;
        _preferencesStore.Save(_preferences);
    }

    private void RestoreInitialWallpaper()
    {
        if (_restoredInitialWallpaper)
        {
            return;
        }

        _restoredInitialWallpaper = true;

        if (string.IsNullOrWhiteSpace(_preferences.LastAppliedWallpaperId) &&
            string.IsNullOrWhiteSpace(_preferences.LastAppliedWallpaperPath))
        {
            return;
        }

        var item = _allItems.FirstOrDefault(w => w.Id == _preferences.LastAppliedWallpaperId)
                   ?? _allItems.FirstOrDefault(w =>
                       !string.IsNullOrWhiteSpace(_preferences.LastAppliedWallpaperPath) &&
                       string.Equals(w.FullPath, _preferences.LastAppliedWallpaperPath, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            return;
        }

        ApplyWallpaperCore(item, showStatus: false, persistPreferences: false);
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilter();
    }

    protected override void RefreshLocalizedText()
    {
        Categories[0].DisplayName = L(Localization.Main.Categories.All);
        Categories[1].DisplayName = L(Localization.Main.Categories.Recommended);
        Categories[2].DisplayName = L(Localization.Main.Categories.Cartoon);
        Categories[3].DisplayName = L(Localization.Main.Categories.Starry);
        Categories[4].DisplayName = L(Localization.Main.Categories.Illustration);
        Categories[5].DisplayName = L(Localization.Main.Categories.Dynamic);
        Categories[6].DisplayName = L(Localization.Main.Categories.Local);

        Categories[0].SectionTitle = L(Localization.Main.Section.All);
        Categories[1].SectionTitle = L(Localization.Main.Section.Recommended);
        Categories[2].SectionTitle = L(Localization.Main.Section.Cartoon);
        Categories[3].SectionTitle = L(Localization.Main.Section.Starry);
        Categories[4].SectionTitle = L(Localization.Main.Section.Illustration);
        Categories[5].SectionTitle = L(Localization.Main.Section.Dynamic);
        Categories[6].SectionTitle = L(Localization.Main.Section.Local);

        foreach (var category in Categories)
        {
            category.PlannedText = L(Localization.Main.Card.Planned);
        }

        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(WindowSubtitle));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(LocalButtonText));
        OnPropertyChanged(nameof(SettingsButtonText));
        OnPropertyChanged(nameof(MinimizeTip));
        OnPropertyChanged(nameof(MaximizeTip));
        OnPropertyChanged(nameof(CloseTip));
        OnPropertyChanged(nameof(PreviewButtonText));
        OnPropertyChanged(nameof(SetWallpaperButtonText));
        OnPropertyChanged(nameof(CurrentBadgeText));
        OnPropertyChanged(nameof(DynamicBadgeText));
        OnPropertyChanged(nameof(PreviewResolution));
        OnPropertyChanged(nameof(ApplyWallpaperText));
        OnPropertyChanged(nameof(SettingsTitle));
        OnPropertyChanged(nameof(SettingsSubtitle));
        OnPropertyChanged(nameof(AutoStartTitle));
        OnPropertyChanged(nameof(AutoStartDesc));
        OnPropertyChanged(nameof(AutoRotateTitle));
        OnPropertyChanged(nameof(AutoRotateDesc));
        OnPropertyChanged(nameof(BatterySaverTitle));
        OnPropertyChanged(nameof(BatterySaverDesc));
        OnPropertyChanged(nameof(ContentFilterTitle));
        OnPropertyChanged(nameof(ContentFilterDesc));
        OnPropertyChanged(nameof(SaveSettingsText));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(PlannedText));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionIcon));
        OnPropertyChanged(nameof(CountText));
    }
}
