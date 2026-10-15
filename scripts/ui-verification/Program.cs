using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Ursa.Controls;
using CodeWF.Avalonia.Controls.Themes;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Core.UpdateChecking;
using KidWall.App;
using KidWall.App.Services;
using KidWall.App.ViewModels;
using KidWall.App.Views;
using KidWall.Core.Models;
using KidWall.Core.Services;

var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/ui-verification");
Directory.CreateDirectory(output);
AppBuilder.Configure<App>().UseSkia().WithInterFont()
    .With(new FontManagerOptions { FontFallbacks = [new FontFallback { FontFamily = new FontFamily("Microsoft YaHei UI") }] })
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var preferences = new AppPreferences { AutoStart = false, AutoRotate = false };
var store = new AppPreferencesStore(Path.Combine(output,"preferences"));
var samples = Path.Combine(output,"samples");
Directory.CreateDirectory(Path.Combine(samples,"cartoon"));
File.WriteAllText(Path.Combine(samples,"wallpapers.json"), """
{"categories":[{"directory":"cartoon","category":"cartoon"}],"items":{"cartoon/Stars":{"name":"Stars","recommended":true}}}
""");
File.WriteAllBytes(Path.Combine(samples,"cartoon","Stars.png"),Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jE1cAAAAASUVORK5CYII="));
var repositorySample=Path.Combine("res","cartoon","01-dino-balloons.jpg");
if(File.Exists(repositorySample))File.Copy(repositorySample,Path.Combine(samples,"cartoon","Stars.png"),overwrite:true);
var wallpaper = new FakeWallpaper();
var fake = new FakeUpdates();
using var model = new MainViewModel(preferences,store,wallpaper,new FakeDynamic(),new FakeWindow(),samples,fake,_=>{},()=>throw new InvalidOperationException("Unexpected exit"));
var window = new MainWindow { DataContext = model, AllowCloseToExit = true };
window.Show(); Complete(model.LoadCommand.ExecuteAsync(null)); Pump();
Check(model.Wallpapers.Count == 1,"Real wallpaper catalog loads through generated JSON metadata");
Check(wallpaper.Calls == 0,"Verification never changes desktop wallpaper");
Check(model.Appearance.Modes.Count == 3 && model.Appearance.Palettes.Count == 8,"Three independent modes and eight palettes");
var paletteSelector=window.GetVisualDescendants().OfType<ListBox>().Single(control=>control.Classes.Contains("title-palettes"));
Check(paletteSelector.Items.Count==8 && !window.GetVisualDescendants().OfType<ComboBox>().Any(),"Title bar uses eight palette dots with no appearance dropdowns");
var themeToggle=window.GetVisualDescendants().OfType<ThemeToggleButton>().Single();
model.Appearance.ThemeMode=StudioThemeMode.System;Pump();
foreach(var expected in new[]{StudioThemeMode.Light,StudioThemeMode.Dark,StudioThemeMode.System})
{
    themeToggle.GetVisualDescendants().OfType<Button>().Single(button=>button.Name=="PART_ThemeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
    Check(model.Appearance.ThemeMode==expected && store.Load().ThemeMode==expected.ToString(),"Actual Ursa theme click persists "+expected);
}
var sample=model.Wallpapers[0];
model.SearchText="missing-wallpaper";Pump();Check(model.Wallpapers.Count==0,"Search filters actual wallpaper data");
model.SearchText="Stars";Pump();Check(model.Wallpapers.Count==1,"Search restores the matching wallpaper");
model.SearchText=string.Empty;
model.OpenPreviewCommand.Execute(sample);Pump();Check(model.IsPreviewOpen && sample.PreviewDialogOpen,"Preview opens for the selected wallpaper");
model.ClosePreviewCommand.Execute(null);Pump();Check(!model.IsPreviewOpen && !sample.PreviewDialogOpen,"Preview closes without changing the wallpaper");
model.ToggleFavoriteCommand.Execute(sample);Check(sample.IsFavorite,"Favorite action changes the selected card");
model.ToggleFavoriteCommand.Execute(sample);Check(!sample.IsFavorite,"Favorite action can be reversed");
foreach(var palette in model.Appearance.Palettes.ToArray())
{
    paletteSelector.SelectedItem=palette;
    foreach(var dark in new[]{false,true})
    {
        model.Appearance.SelectedMode=model.Appearance.Modes.Single(item=>item.Key==(dark?"Dark":"Light"));Pump();
        Check(App.ThemeController.Palette.ToString()==palette.Key,"Actual palette: "+palette.Key);
        Check(store.Load().Palette==palette.Key && store.Load().ThemeMode==(dark?"Dark":"Light"),"Native preference round trip");
        var categories=window.GetVisualDescendants().OfType<Button>().Where(button=>button.Classes.Contains("category")).ToArray();
        Check(categories.Length==model.Categories.Count,"Every category has a real button");
        foreach(var category in categories)
        {
            var item=(CategoryItemViewModel)category.DataContext!;
            var label=category.GetVisualDescendants().OfType<TextBlock>().Single(text=>text.Text==item.DisplayName);
            Check(label.Foreground is ISolidColorBrush { Color.A: 255 },"Category label remains visible in both modes");
            if (item.IsSelected)
                Check(category.GetVisualDescendants().OfType<TextBlock>().Single(text=>text.Classes.Contains("category-count")).Foreground is ISolidColorBrush { Color.R: 255, Color.G: 255, Color.B: 255 },
                    "Selected category count stays legible on the solid accent");
        }
        Capture($"kidwall-{palette.Key}-{(dark?"dark":"light")}.png");
    }
}
model.Appearance.SelectedMode=model.Appearance.Modes.Single(item=>item.Key=="System");
Check(store.Load().ThemeMode=="System","System mode persisted independently");
foreach(var language in model.Appearance.Languages)
{
    var languageButton=window.GetVisualDescendants().OfType<Button>().Single(button=>button.Flyout is MenuFlyout);
    var languageMenu=(MenuFlyout)languageButton.Flyout!;languageMenu.ShowAt(languageButton);Pump();
    var languageItem=languageMenu.ItemsSource!.Cast<LanguageOption>().Single(option=>option.Key==language.Key);
    Check(languageMenu.IsOpen && languageItem.SelectCommand is not null,"Language popup opens with the actual language catalog");
    languageItem.SelectCommand!.Execute(null);languageMenu.Hide();Pump();
    foreach(var mode in new[]{StudioThemeMode.Light,StudioThemeMode.Dark,StudioThemeMode.System})
    {
        model.Appearance.ThemeMode=mode;Pump();
        var key=mode switch{StudioThemeMode.Light=>Localization.Appearance.Light,StudioThemeMode.Dark=>Localization.Appearance.Dark,_=>Localization.Appearance.System};
        var themeButton=themeToggle.GetVisualDescendants().OfType<Button>().Single(button=>button.Name=="PART_ThemeButton");
        Check(ToolTip.GetTip(themeButton) is TextBlock text && text.Text==I18nManager.Instance.GetResource(key),"Actual Ursa tooltip follows "+language.Key+" / "+mode);
    }
    Check(store.Load().Language==language.Key && I18nManager.Instance.Culture.Name==language.Key,"Requested language stays active and survives JSON persistence");
    Check(model.Wallpapers[0].CategoryDisplayName==I18nManager.Instance.GetResource(Localization.Main.Categories.Cartoon),"Existing wallpaper card updates its language");
    var card=window.GetVisualDescendants().OfType<KidWall.App.Controls.DynamicWallpaperPreviewView>().First();
    Check(card.PreviewText==model.PreviewButtonText && card.ApplyText==model.SetWallpaperButtonText,"Existing card actions update language");
    model.IsSettingsOpen=true;Pump();Capture("settings-"+language.Key+".png");
    Check(window.GetVisualDescendants().OfType<ComboBox>().Count()==0,"Settings do not repeat title-bar appearance selectors");
    Check(model.Version==UpdateVersion.Display(typeof(App).Assembly) && model.CompileTime!="—","Actual application metadata survives native publication");
    model.IsSettingsOpen=false;
}
var about = new AboutWindow { DataContext = model };
about.Show();
foreach(var language in model.Appearance.Languages)
{
    model.Appearance.SelectedLanguage=language;Pump();
    Check(I18nManager.Instance.Culture.Name==language.Key && about.Title==I18nManager.Instance.GetResource(Localization.About.Title),"Existing About window keeps the requested language");
    Check(about.GetVisualDescendants().OfType<TextBlock>().Any(text=>text.Text==model.Version),"About shows actual assembly version");
    using var image=about.CaptureRenderedFrame()??throw new InvalidOperationException("Missing About image");
    using var stream=File.Create(Path.Combine(output,"about-"+language.Key+".png"));image.Save(stream);
}
about.Close();
VerifyUpdates();
Check(wallpaper.Calls==0,"All tests kept the user's wallpaper untouched");
window.Close();
VerifyPersistence();
// 用仓库真实静态资源检查完整图库，依然使用隔离偏好和壁纸替身。
var gallerySamples=Path.Combine(output,"gallery-samples");
Directory.CreateDirectory(gallerySamples);
File.Copy(Path.Combine("res","wallpapers.json"),Path.Combine(gallerySamples,"wallpapers.json"),true);
foreach(var category in new[]{"cartoon","starry","illustration"})
{
    var target=Path.Combine(gallerySamples,category);Directory.CreateDirectory(target);
    foreach(var file in Directory.EnumerateFiles(Path.Combine("res",category)))
        File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
}
using(var gallery=new MainViewModel(new AppPreferences{AutoStart=false,AutoRotate=false},
    new AppPreferencesStore(Path.Combine(output,"gallery-preferences")),wallpaper,new FakeDynamic(),new FakeWindow(),gallerySamples,fake,_=>{},()=>{}))
{
    var galleryWindow=new MainWindow{DataContext=gallery,AllowCloseToExit=true};galleryWindow.Show();
    Complete(gallery.LoadCommand.ExecuteAsync(null));
    Check(gallery.Wallpapers.Count==12,"Repository gallery loads all twelve static wallpapers");
    gallery.Appearance.SelectedLanguage=gallery.Appearance.Languages.Single(item=>item.Key=="zh-CN");
    gallery.Appearance.SelectedPalette=gallery.Appearance.Palettes.Single(item=>item.Key=="DotNet");
    foreach(var mode in new[]{"Light","Dark"})
    {
        gallery.Appearance.SelectedMode=gallery.Appearance.Modes.Single(item=>item.Key==mode);Pump();
        Check(!galleryWindow.GetVisualDescendants().OfType<Border>().Any(border=>
            border.Classes.Contains("badge") && border.IsVisible && border.DataContext is WallpaperItemViewModel),
            "Static wallpapers never display a dynamic badge, including decorative animations");
        Check(galleryWindow.GetVisualDescendants().OfType<TextBlock>().Where(text=>text.Classes.Contains("card-tag"))
                .All(text=>text.Foreground is ISolidColorBrush { Color.R: 255, Color.G: 255, Color.B: 255 }),
            "Image overlay labels keep a light foreground in both theme modes");
        using var image=galleryWindow.CaptureRenderedFrame()??throw new InvalidOperationException("Missing gallery image");
        using var stream=File.Create(Path.Combine(output,"gallery-"+mode+".png"));image.Save(stream);
    }
    galleryWindow.Close();
}
Console.WriteLine("KidWall UI verification passed: "+output);

void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Console.WriteLine("PASS "+message);}
void Pump(){var t=Stopwatch.StartNew();do{Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();Thread.Sleep(10);}while(t.ElapsedMilliseconds<220);}
void Complete(Task task){var timer=Stopwatch.StartNew();while(!task.IsCompleted&&timer.ElapsedMilliseconds<10000)Pump();Check(task.IsCompleted,"Async operation completes");task.GetAwaiter().GetResult();}
void Capture(string file){Pump();using var image=window.CaptureRenderedFrame()??throw new InvalidOperationException("Missing image");using var stream=File.Create(Path.Combine(output,file));image.Save(stream);}
void VerifyPersistence()
{
    var persistenceStore=new AppPreferencesStore(Path.Combine(output,"persistence-preferences"));
    var localDirectory=Path.Combine(output,"imported-wallpapers");Directory.CreateDirectory(localDirectory);
    File.Copy(Path.Combine(samples,"cartoon","Stars.png"),Path.Combine(localDirectory,"LocalStars.png"),true);
    var acceptedWallpaper=new AcceptedWallpaper();
    string favoriteId;
    using(var first=new MainViewModel(new AppPreferences{AutoStart=false},persistenceStore,acceptedWallpaper,
        new FakeDynamic(),new FakeWindow(),samples,fake,_=>{},()=>{}))
    {
        Complete(first.LoadCommand.ExecuteAsync(null));
        Complete(first.AddLocalFolderAsync(localDirectory));
        Complete(first.AddLocalFolderAsync(localDirectory+Path.DirectorySeparatorChar));
        Complete(first.AddLocalFolderAsync(Path.Combine(localDirectory,"missing-folder")));
        Check(persistenceStore.Load().LocalFolders.Count==1,"Local folders persist with normalized duplicates and missing folders excluded");
        var local=first.Wallpapers.Single(item=>item.IsLocal);favoriteId=local.Id;
        first.OpenPreviewCommand.Execute(local);
        var favoriteCategory=first.Categories.Single(item=>item.Key=="favorites");
        var countChanged=false;favoriteCategory.PropertyChanged+=(_,change)=>countChanged|=change.PropertyName==nameof(CategoryItemViewModel.CountText);
        first.ToggleFavoriteCommand.Execute(local);
        Check(countChanged && favoriteCategory.CountText=="1","Favorite count label refreshes immediately");
        Check(first.PreviewItem==local && local.IsFavorite,"Favorite keeps the selected preview open");
        Complete(first.LoadCommand.ExecuteAsync(null));
        Check(first.PreviewItem?.Id==favoriteId && first.PreviewItem.IsFavorite,"Refreshing catalog keeps preview and favorite state");
        first.Categories.Single(item=>item.Key=="favorites").SelectCommand.Execute(null);
        Check(first.Wallpapers.Count==1 && first.Wallpapers[0].Id==favoriteId,"Favorites category filters actual saved favorites");
        first.Categories.Single(item=>item.Key=="local").SelectCommand.Execute(null);
        Check(first.Wallpapers.Count==1 && first.Wallpapers[0].IsLocal,"Local category exposes imported folder images");
        first.ApplyWallpaperCommand.Execute(first.Wallpapers[0]);
        Check(first.Wallpapers.Single().IsCurrent && persistenceStore.Load().LastAppliedWallpaperId==favoriteId,
            "Controlled wallpaper application updates unique current state and persisted identity");
    }
    using(var reopened=new MainViewModel(persistenceStore.Load(),persistenceStore,acceptedWallpaper,
        new FakeDynamic(),new FakeWindow(),samples,fake,_=>{},()=>{}))
    {
        Complete(reopened.LoadCommand.ExecuteAsync(null));
        var restored=reopened.Wallpapers.Single(item=>item.Id==favoriteId);
        Check(restored.IsLocal && restored.IsFavorite && restored.IsCurrent,"Restart restores imported images, favorite, and current wallpaper");
        reopened.Categories.Single(item=>item.Key=="favorites").SelectCommand.Execute(null);
        reopened.ToggleFavoriteCommand.Execute(restored);
        Check(reopened.Wallpapers.Count==0 && persistenceStore.Load().FavoriteWallpaperIds.Count==0,
            "Removing favorite immediately updates filtered view and saved preference");
        Complete(reopened.LoadCommand.ExecuteAsync(null));
        Check(reopened.Wallpapers.Count==0,"Removed favorite stays removed after reload");
    }
    Check(acceptedWallpaper.Calls==2,"Current wallpaper restored only through isolated service substitute");
}
void VerifyUpdates()
{
    Complete(model.Update.CheckAsync());Check(model.Update.State==UpdateState.Current,"Current release state");
    fake.Result=UpdateCheckResult.Failed("offline");Complete(model.Update.CheckAsync());Check(model.Update.StatusText.Contains("offline"),"Visible failure");
    fake.Result=new(new UpdateInfo(new Version(9,0),"v9.0","KidWall","Notes","https://example.test/release","https://example.test/setup.exe","setup.exe","https://example.test/setup.exe.sha256"),true,null);
    Complete(model.Update.CheckAsync());Complete(model.Update.AdvanceAsync());Check(model.Update.State==UpdateState.Downloaded,"Verified download enables installation");
    Complete(model.Update.AdvanceAsync());Check(model.Update.State==UpdateState.Installed,"Manual installation keeps application open");
    fake.Delay=new TaskCompletionSource<UpdateCheckResult>();var pending=model.Update.CheckAsync();
    Check(model.Update.IsBusy && !model.Update.ActionCommand.CanExecute(null),"Busy action disabled");
    model.Update.CancelCommand.Execute(null);Complete(pending);Check(model.Update.State==UpdateState.Canceled,"Cancel restores usable state");
    using var closing=new UpdateViewModel(fake,_=>{},()=>{});var unfinished=closing.CheckAsync();closing.Dispose();Complete(unfinished);Check(!closing.ActionCommand.CanExecute(null),"Closing cancels safely");
}
sealed class FakeWallpaper:IDesktopWallpaperService{public int Calls{get;private set;}public bool SetWallpaper(string path){Calls++;throw new InvalidOperationException("Unexpected wallpaper operation");}}
sealed class AcceptedWallpaper:IDesktopWallpaperService{public int Calls{get;private set;}public bool SetWallpaper(string path){Calls++;return File.Exists(path);}}
sealed class FakeDynamic:IDynamicWallpaperService{public bool IsSupported=>false;public bool IsRunning=>false;public bool Show(string path)=>throw new InvalidOperationException("Unexpected dynamic wallpaper");public void Stop(){}public void Dispose(){}}
sealed class FakeWindow:IMainWindowService{public void Minimize(){}public void ToggleMaximize(){}public void CloseToTray(){}public void ShowAbout(){}public Task<string?> PickLocalFolderAsync()=>Task.FromResult<string?>(null);}
sealed class FakeUpdates:IUpdateService
{
    public UpdateServiceOptions Options{get;}=new(){Repository="KidWall",ProductName="KidWall"};
    public Version CurrentVersion=>new(1,0);public string CurrentVersionText=>"1.0";
    public UpdateCheckResult Result{get;set;}=UpdateCheckResult.Latest();
    public TaskCompletionSource<UpdateCheckResult>? Delay{get;set;}
    public Task<UpdateCheckResult> CheckAsync(Version? current=null,CancellationToken cancellationToken=default)=>Delay?.Task.WaitAsync(cancellationToken)??Task.FromResult(Result);
    public Task<UpdateDownloadResult> DownloadAsync(UpdateInfo update,IProgress<UpdateDownloadProgress>? progress=null,CancellationToken cancellationToken=default)=>Task.FromResult(new UpdateDownloadResult("setup.exe","setup.exe",100));
    public Task<UpdateInstallResult> InstallAsync(UpdateDownloadResult download,CancellationToken cancellationToken=default)=>Task.FromResult(new UpdateInstallResult(UpdateInstallKind.Manual,"Manual installation"){ShouldExitApplication=false});
}
