using System.Globalization;
using System.Runtime.CompilerServices;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using KidWall.Core.Models;
using KidWall.Core.Services;

namespace KidWall.App;

/// <summary>验证正式原生产物的双语资源和隔离偏好，不创建窗口或壁纸服务。</summary>
internal static class RuntimeVerification
{
    public static void Run(string directory)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            throw new InvalidOperationException("Runtime verification requires a NativeAOT executable.");
        var root = Path.Combine(Path.GetFullPath(directory), "runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var resources = Path.Combine(AppContext.BaseDirectory, "I18n");
        if (OperatingSystem.IsMacOS() && !Directory.Exists(resources))
            resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "I18n"));
        var manager = I18nManager.Instance;
        manager.Register(new JsonLangPlugin { ResourceFolder = resources }, CultureInfo.GetCultureInfo("zh-CN"));
        var labels = new HashSet<string>();
        foreach (var culture in new[] { "zh-CN", "en-US", "zh-CN", "en-US" })
        {
            if (!File.Exists(Path.Combine(resources, culture + ".json")))
                throw new InvalidOperationException("Published language file is missing: " + culture);
            manager.Culture = CultureInfo.GetCultureInfo(culture);
            var text = manager.GetResource(Localization.Shell.Search.Placeholder);
            if (string.IsNullOrWhiteSpace(text) || text == Localization.Shell.Search.Placeholder)
                throw new InvalidOperationException("Published language resources are missing: " + culture);
            labels.Add(text);
        }
        if (labels.Count != 2) throw new InvalidOperationException("Published languages do not switch independently.");
        var store = new AppPreferencesStore(root);
        store.Save(new AppPreferences { AutoStart = false, AutoRotate = false, Language = "en-US", ThemeMode = "System", Palette = "Aurora" });
        var saved = new AppPreferencesStore(root).Load();
        if (saved.AutoStart || saved.AutoRotate || saved.Language != "en-US" || saved.ThemeMode != "System" || saved.Palette != "Aurora")
            throw new InvalidOperationException("Native appearance preferences did not round-trip.");
        var result = $"NativeAOT runtime verification passed; version={typeof(Program).Assembly.GetName().Version}; languages=2; preferences=isolated; wallpaper=unused";
        File.WriteAllText(Path.Combine(root, "runtime-verification.txt"), result);
        Console.WriteLine(result);
    }
}
