namespace KidWall.App.Services;

public interface IMainWindowService
{
    void Minimize();

    void ToggleMaximize();

    void CloseToTray();

    void ShowAbout();

    Task<string?> PickLocalFolderAsync();
}
