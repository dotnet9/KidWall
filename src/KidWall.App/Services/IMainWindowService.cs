namespace KidWall.App.Services;

public interface IMainWindowService
{
    void Minimize();

    void ToggleMaximize();

    void CloseToTray();

    Task<string?> PickLocalFolderAsync();
}
