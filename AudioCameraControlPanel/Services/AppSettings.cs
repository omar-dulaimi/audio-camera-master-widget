namespace AudioCameraControlPanel.Services;

public sealed class AppSettings
{
    public string? LastOutputDeviceId { get; set; }

    public string? LastInputDeviceId { get; set; }

    public string? LastCameraDeviceId { get; set; }

    public int? VolumeStepPercent { get; set; }
}
