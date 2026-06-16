using AudioCameraControlPanel.Services;

namespace AudioCameraControlPanel.Tests.Fakes;

public sealed class InMemoryAppSettingsStore : IAppSettingsStore
{
    private AppSettings _settings = new();

    public InMemoryAppSettingsStore()
    {
    }

    public InMemoryAppSettingsStore(AppSettings settings)
    {
        Settings = settings;
    }

    public AppSettings Settings
    {
        get => Clone(_settings);
        set => _settings = Clone(value);
    }

    public int LoadCount { get; private set; }

    public int SaveCount { get; private set; }

    public List<AppSettings> SavedSettings { get; } = new();

    public AppSettings? LastSavedSettings => SavedSettings.Count == 0 ? null : SavedSettings[^1];

    public Exception? LoadException { get; set; }

    public Exception? SaveException { get; set; }

    public Task<AppSettings> LoadAsync()
    {
        LoadCount++;

        return LoadException is null
            ? Task.FromResult(Settings)
            : Task.FromException<AppSettings>(LoadException);
    }

    public Task SaveAsync(AppSettings settings)
    {
        SaveCount++;

        if (SaveException is not null)
        {
            return Task.FromException(SaveException);
        }

        Settings = settings;
        SavedSettings.Add(Clone(settings));
        return Task.CompletedTask;
    }

    public void ResetHistory()
    {
        LoadCount = 0;
        SaveCount = 0;
        SavedSettings.Clear();
    }

    private static AppSettings Clone(AppSettings settings)
    {
        return new AppSettings
        {
            LastOutputDeviceId = settings.LastOutputDeviceId,
            LastInputDeviceId = settings.LastInputDeviceId,
            LastCameraDeviceId = settings.LastCameraDeviceId,
            VolumeStepPercent = settings.VolumeStepPercent
        };
    }
}
