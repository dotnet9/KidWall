using System.Text.Json;
using System.Text.Json.Serialization;

namespace KidWall.Core.Models;

/// <summary>为原生发布生成偏好与资源目录的 JSON 元数据。</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppPreferences))]
[JsonSerializable(typeof(WallpaperCatalog))]
internal partial class WallpaperJsonContext : JsonSerializerContext { }
