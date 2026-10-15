using Avalonia.Markup.Xaml;
using CodeWF.Avalonia.Controls.Controls;

namespace KidWall.App.Views;

public partial class AboutWindow : CodeWFWindow
{
    public AboutWindow() => AvaloniaXamlLoader.Load(this);
}
