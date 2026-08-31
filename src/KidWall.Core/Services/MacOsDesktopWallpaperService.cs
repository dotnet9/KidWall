using System.Diagnostics;

namespace KidWall.Core.Services;

/// <summary>
/// macOS 桌面壁纸服务。
/// 首选 AppleScript（Finder），失败时自动回退到 NSWorkspace 原生 API，
/// 后者不依赖 Apple Events 自动化权限。
/// </summary>
public sealed class MacOsDesktopWallpaperService : IDesktopWallpaperService
{
    // 避开 ~/Library/Application Support（可能被开发沙箱拦截），使用 ~/.local/share/KidWall
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "KidWall");

    private static readonly string SwiftSourcePath = Path.Combine(CacheDir, "setwallpaper.swift");
    private static readonly string SwiftBinaryPath = Path.Combine(CacheDir, "setwallpaper");

    private const string SwiftSource = """
        import AppKit

        let args = CommandLine.arguments
        guard args.count >= 2, !args[1].isEmpty else { exit(2) }
        guard let screen = NSScreen.main else { exit(3) }
        let url = URL(fileURLWithPath: args[1])
        do {
            try NSWorkspace.shared.setDesktopImageURL(url, for: screen, options: [:])
            exit(0)
        } catch {
            FileHandle.standardError.write(("\(error)\n").data(using: .utf8) ?? Data())
            exit(1)
        }
        """;

    public bool SetWallpaper(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(imagePath);

        // 转义 AppleScript 字符串中的反斜杠与双引号，避免路径注入
        var escapedPath = fullPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var script =
            $"tell application \"Finder\" to set desktop picture to POSIX file \"{escapedPath}\"";

        var (exitCode, stderr) = RunProcess("/usr/bin/osascript", ["-e", script]);
        if (exitCode == 0)
        {
            return true;
        }

        // 典型失败：macOS 自动化权限未授权（-10024/-10004 权限违例）。
        // 回退到 NSWorkspace 原生 API，绕过 Apple Events 自动化权限。
        TryWriteDiagnostics($"osascript failed (exit={exitCode}): {stderr}", fullPath);
        return TrySetViaNativeWorkspace(fullPath);
    }

    /// <summary>通过 NSWorkspace.setDesktopImageURL 设置壁纸（原生 API，无需自动化权限）。</summary>
    private static bool TrySetViaNativeWorkspace(string fullPath)
    {
        try
        {
            if (!File.Exists(SwiftBinaryPath))
            {
                Directory.CreateDirectory(CacheDir);
                File.WriteAllText(SwiftSourcePath, SwiftSource);

                var (compileExit, compileErr) = RunProcess(
                    "/usr/bin/xcrun", ["swiftc", "-O", SwiftSourcePath, "-o", SwiftBinaryPath]);
                if (compileExit != 0)
                {
                    TryWriteDiagnostics($"swiftc failed (exit={compileExit}): {compileErr}", fullPath);
                    return false;
                }
            }

            var (runExit, runErr) = RunProcess(SwiftBinaryPath, [fullPath]);
            if (runExit != 0)
            {
                TryWriteDiagnostics($"NSWorkspace failed (exit={runExit}): {runErr}", fullPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            TryWriteDiagnostics(ex.ToString(), fullPath);
            return false;
        }
    }

    /// <summary>启动外部进程并等待结束，返回退出码与标准错误输出。</summary>
    private static (int ExitCode, string Stderr) RunProcess(string fileName, IEnumerable<string> args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return (-1, string.Empty);
        }

        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(60_000))
        {
            process.Kill();
            return (-1, "timeout");
        }

        return (process.ExitCode, stderr);
    }

    /// <summary>把失败原因写入临时诊断日志，便于排查。</summary>
    private static void TryWriteDiagnostics(string detail, string imagePath)
    {
        try
        {
            var log = Path.Combine(Path.GetTempPath(), "kidwall-wallpaper.log");
            File.AppendAllText(
                log,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {imagePath}{Environment.NewLine}{detail}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // 日志写入失败不影响主流程
        }
    }
}
