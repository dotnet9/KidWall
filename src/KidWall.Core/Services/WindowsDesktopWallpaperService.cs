using System.Runtime.InteropServices;

namespace KidWall.Core.Services;

/// <summary>Windows 桌面壁纸服务：通过 SystemParametersInfo(SPI_SETDESKWALLPAPER) 应用壁纸。</summary>
public sealed class WindowsDesktopWallpaperService : IDesktopWallpaperService
{
    private const uint SpiSetDeskWallpaper = 0x0014;
    private const uint SpifUpdateIniFile = 0x01;
    private const uint SpifSendWinIniChange = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    public bool SetWallpaper(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return false;
        }

        // 使用绝对路径，避免工作目录导致的相对路径失效
        var fullPath = Path.GetFullPath(imagePath);
        return SystemParametersInfo(SpiSetDeskWallpaper, 0, fullPath, SpifUpdateIniFile | SpifSendWinIniChange);
    }
}
