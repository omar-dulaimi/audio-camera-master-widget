namespace AudioCameraControlPanel.Services;

public interface IStartupRegistry
{
    string? GetString(string keyPath, string valueName);

    void SetString(string keyPath, string valueName, string value);

    void DeleteValue(string keyPath, string valueName);
}
