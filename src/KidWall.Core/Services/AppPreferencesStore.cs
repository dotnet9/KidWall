using System.Text.Json;
using KidWall.Core.Models;

namespace KidWall.Core.Services;

/// <summary>将偏好设置以 JSON 形式持久化到本地用户数据目录。</summary>
public sealed class AppPreferencesStore
{
    private readonly string _filePath;

    public AppPreferencesStore(string directory)
    {
        _filePath = Path.Combine(directory, "preferences.json");
    }

    public AppPreferences Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppPreferences>(json) ?? new AppPreferences();
            }
        }
        catch (Exception)
        {
            // 配置损坏时回退默认值
        }

        return new AppPreferences();
    }

    public void Save(AppPreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
