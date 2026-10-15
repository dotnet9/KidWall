using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CodeWF.Avalonia.Controls.Themes;
using CodeWF.Toolkit.Lang;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using KidWall.Core.Models;
using KidWall.Core.Services;

namespace KidWall.App.ViewModels;

/// <summary>维护独立的主题模式、配色和语言偏好。</summary>
public sealed class AppearanceViewModel : ObservableObject, IDisposable
{
    private readonly AppPreferences _preferences;
    private readonly AppPreferencesStore _store;
    private AppearanceOption? _mode, _palette;
    private LanguageOption? _language;
    private bool _refreshing;
    private bool _disposed;
    public AppearanceViewModel(AppPreferences preferences, AppPreferencesStore store)
    {
        _preferences = preferences; _store = store;
        if (!Enum.TryParse<StudioThemeMode>(preferences.ThemeMode, out var mode) || !Enum.IsDefined(mode)) mode = StudioThemeMode.System;
        if (!Enum.TryParse<StudioPalette>(preferences.Palette, out var palette) || !Enum.IsDefined(palette)) palette = StudioPalette.DotNet;
        App.ThemeController.Mode = mode; App.ThemeController.Palette = palette;
        Languages = [new("zh-CN", T(Localization.Appearance.SimplifiedChinese)), new("en-US", T(Localization.Appearance.English))];
        foreach (var option in Languages) option.SelectCommand = new RelayCommand(() => SelectedLanguage = option);
        _language = Languages.FirstOrDefault(item => item.Key == preferences.Language) ?? Languages[0];
        I18nManager.Instance.CultureChanged += OnCultureChanged;
        Refresh();
    }
    public ObservableCollection<AppearanceOption> Modes { get; } = [];
    public ObservableCollection<AppearanceOption> Palettes { get; } = [];
    public IReadOnlyList<LanguageOption> Languages { get; }
    public string ThemeLabel => T(Localization.Appearance.Mode);
    public string PaletteLabel => T(Localization.Appearance.Palette);
    public string LanguageLabel => T(Localization.Appearance.Language);
    public StudioThemeMode ThemeMode
    {
        get => App.ThemeController.Mode;
        set => SelectedMode = Modes.Single(item => item.Key == value.ToString());
    }
    public AppearanceOption? SelectedMode
    {
        get => _mode;
        set
        {
            if (!SetProperty(ref _mode, value) || _refreshing || value is null) return;
            App.ThemeController.Mode = Enum.Parse<StudioThemeMode>(value.Key);
            _preferences.ThemeMode = value.Key; _store.Save(_preferences);
            OnPropertyChanged(nameof(ThemeMode));
        }
    }
    public AppearanceOption? SelectedPalette
    {
        get => _palette;
        set
        {
            if (!SetProperty(ref _palette, value) || _refreshing || value is null) return;
            App.ThemeController.Palette = Enum.Parse<StudioPalette>(value.Key);
            _preferences.Palette = value.Key; _store.Save(_preferences);
        }
    }
    public LanguageOption? SelectedLanguage
    {
        get => _language;
        set
        {
            if (!SetProperty(ref _language, value) || _refreshing || value is null) return;
            _preferences.Language = value.Key;
            var culture = CultureInfo.GetCultureInfo(value.Key);
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
            I18nManager.Instance.Culture = culture;
            _store.Save(_preferences);
        }
    }
    private static string T(string key) => I18nManager.Instance.GetResource(key);
    /// <summary>全局切语可能来自后台线程，主题资源和绑定集合在 UI 线程刷新。</summary>
    private void OnCultureChanged(object? sender, EventArgs args)
    {
        if (_disposed) return;
        if (Dispatcher.UIThread.CheckAccess()) Refresh();
        else Dispatcher.UIThread.Post(() => { if (!_disposed) Refresh(); });
    }
    private void Refresh()
    {
        _refreshing = true;
        try
        {
            Modes.Clear();
            foreach (var mode in Enum.GetValues<StudioThemeMode>()) Modes.Add(new(mode.ToString(), T(mode switch
            { StudioThemeMode.Light => Localization.Appearance.Light, StudioThemeMode.Dark => Localization.Appearance.Dark, _ => Localization.Appearance.System }), Brushes.Transparent));
            Palettes.Clear();
            foreach (var palette in Enum.GetValues<StudioPalette>())
            {
                Application.Current!.TryGetResource("StudioSwatch" + palette, Application.Current.ActualThemeVariant, out var swatch);
                Palettes.Add(new(palette.ToString(), T(palette switch
                {
                    StudioPalette.Default => Localization.Appearance.Default, StudioPalette.DotNet => Localization.Appearance.DotNet,
                    StudioPalette.Aurora => Localization.Appearance.Aurora, StudioPalette.Ember => Localization.Appearance.Ember,
                    StudioPalette.Aquatic => Localization.Appearance.Aquatic, StudioPalette.Desert => Localization.Appearance.Desert,
                    StudioPalette.Dusk => Localization.Appearance.Dusk, _ => Localization.Appearance.NightSky
                }), swatch as IBrush ?? Brushes.Transparent));
            }
            SelectedMode = Modes.Single(item => item.Key == App.ThemeController.Mode.ToString());
            SelectedPalette = Palettes.Single(item => item.Key == App.ThemeController.Palette.ToString());
            SelectedLanguage = Languages.FirstOrDefault(item => item.Key == I18nManager.Instance.Culture?.Name) ?? Languages[0];
            OnPropertyChanged(string.Empty);
        }
        finally { _refreshing = false; }
    }
    public void Dispose()
    {
        _disposed = true;
        I18nManager.Instance.CultureChanged -= OnCultureChanged;
    }
}

public sealed record AppearanceOption(string Key, string DisplayName, IBrush Swatch);
public sealed record LanguageOption(string Key, string DisplayName)
{
    public ICommand? SelectCommand { get; set; }
}
