using Avalonia;

namespace KidWall.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--verify-runtime", var directory])
        {
            RuntimeVerification.Run(directory);
            return;
        }
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "kidwall-crash.log"),
                ex.ToString());
            throw;
        }
    }

    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
