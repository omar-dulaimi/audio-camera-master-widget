using AudioCameraControlPanel.Services;

namespace AudioCameraControlPanel.Tests.Fakes;

public sealed class FakeStartupRegistrationService : IStartupRegistrationService
{
    public bool IsEnabledValue { get; set; }

    public Exception? ReadException { get; set; }

    public Exception? WriteException { get; set; }

    public List<bool> SetEnabledCalls { get; } = new();

    public bool IsEnabled()
    {
        if (ReadException is not null)
        {
            throw ReadException;
        }

        return IsEnabledValue;
    }

    public void SetEnabled(bool enabled)
    {
        SetEnabledCalls.Add(enabled);

        if (WriteException is not null)
        {
            throw WriteException;
        }

        IsEnabledValue = enabled;
    }
}
