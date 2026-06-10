using System.Collections.ObjectModel;
using AudioCameraControlPanel.Models;
using AudioCameraControlPanel.Services;

namespace AudioCameraControlPanel.ViewModels;

public sealed class AudioEndpointViewModel : ObservableObject
{
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly AudioEndpointText _text;
    private readonly TimeSpan _volumeSetDelay;
    private readonly object _volumeSetSync = new();
    private AudioDeviceInfo? _selectedDevice;
    private double _selectedVolume;
    private bool _isMuted;
    private bool _canControlVolume;
    private bool _canControlMute;
    private bool _isUpdatingState;
    private double _peakLevel;
    private string _defaultText = "Default: unavailable";
    private string _statusText;
    private CancellationTokenSource? _volumeSetDebounce;
    private Task _pendingVolumeSetTask = Task.CompletedTask;
    private string? _pendingVolumeDeviceId;
    private double? _pendingVolumePercent;

    public AudioEndpointViewModel(
        IAudioDeviceService audioDeviceService,
        AudioDirection direction,
        TimeSpan? volumeSetDelay = null)
    {
        _audioDeviceService = audioDeviceService;
        Direction = direction;
        _volumeSetDelay = volumeSetDelay ?? TimeSpan.FromMilliseconds(100);
        _text = AudioEndpointText.For(direction);
        _statusText = _text.NotLoadedStatusText;

        ToggleMuteCommand = new RelayCommand(ToggleMute, () => SelectedDevice is not null && CanControlMute);
        SetVolume0Command = new RelayCommand(() => SetVolumePreset(0), CanSetVolumePreset);
        SetVolume30Command = new RelayCommand(() => SetVolumePreset(30), CanSetVolumePreset);
        SetVolume35Command = new RelayCommand(() => SetVolumePreset(35), CanSetVolumePreset);
        SetVolume40Command = new RelayCommand(() => SetVolumePreset(40), CanSetVolumePreset);
        SetVolume45Command = new RelayCommand(() => SetVolumePreset(45), CanSetVolumePreset);
        SetVolume60Command = new RelayCommand(() => SetVolumePreset(60), CanSetVolumePreset);
    }

    public event EventHandler? SelectedDeviceChanged;

    public AudioDirection Direction { get; }

    public ObservableCollection<AudioDeviceInfo> Devices { get; } = new();

    public RelayCommand ToggleMuteCommand { get; }

    public RelayCommand SetVolume0Command { get; }

    public RelayCommand SetVolume30Command { get; }

    public RelayCommand SetVolume35Command { get; }

    public RelayCommand SetVolume40Command { get; }

    public RelayCommand SetVolume45Command { get; }

    public RelayCommand SetVolume60Command { get; }

    public AudioDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                CancelPendingVolumeSet();
                SelectedDeviceChanged?.Invoke(this, EventArgs.Empty);
                RefreshEndpointState();
                ToggleMuteCommand.RaiseCanExecuteChanged();
                RaiseVolumePresetCanExecuteChanged();
            }
        }
    }

    public double SelectedVolume
    {
        get => _selectedVolume;
        set
        {
            var bounded = Math.Clamp(Math.Round(value, 0), 0, 100);
            if (SetProperty(ref _selectedVolume, bounded) &&
                !_isUpdatingState &&
                SelectedDevice is not null &&
                CanControlVolume)
            {
                QueueVolumeSet(SelectedDevice.Id, bounded);
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (SetProperty(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(MuteButtonText));
            }
        }
    }

    public bool CanControlVolume
    {
        get => _canControlVolume;
        private set
        {
            if (SetProperty(ref _canControlVolume, value))
            {
                RaiseVolumePresetCanExecuteChanged();
            }
        }
    }

    public bool CanControlMute
    {
        get => _canControlMute;
        private set
        {
            if (SetProperty(ref _canControlMute, value))
            {
                ToggleMuteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double PeakLevel
    {
        get => _peakLevel;
        private set => SetProperty(ref _peakLevel, value);
    }

    public string DefaultText
    {
        get => _defaultText;
        private set => SetProperty(ref _defaultText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string MuteButtonText => IsMuted ? "Unmute" : "Mute";

    public Task RefreshDevicesAsync(string? preferredDeviceId)
    {
        try
        {
            var devices = GetDevices();
            Replace(Devices, devices);
            DefaultText = GetDefaultText(_text.DefaultLabel, devices);
            StatusText = devices.Count == 0
                ? _text.EmptyDevicesStatusText
                : $"{devices.Count} {_text.AvailableDevicesStatusText}";
            SelectedDevice = PickPreferred(devices, preferredDeviceId);
        }
        catch (Exception ex)
        {
            Devices.Clear();
            SelectedDevice = null;
            CanControlVolume = false;
            CanControlMute = false;
            StatusText = $"{_text.EnumerationFailurePrefix} {ex.Message}";
        }

        return Task.CompletedTask;
    }

    public void RefreshPeakLevel()
    {
        PeakLevel = SelectedDevice is null
            ? 0
            : _audioDeviceService.GetPeakLevelPercent(SelectedDevice.Id) ?? 0;
    }

    public void RefreshEndpointState()
    {
        _isUpdatingState = true;
        try
        {
            if (SelectedDevice is null)
            {
                CanControlVolume = false;
                CanControlMute = false;
                SelectedVolume = 0;
                IsMuted = false;
                PeakLevel = 0;
                return;
            }

            var state = _audioDeviceService.GetEndpointState(SelectedDevice.Id);
            CanControlVolume = state.CanControlVolume;
            CanControlMute = state.CanControlMute;
            SelectedVolume = state.VolumePercent ?? 0;
            IsMuted = state.IsMuted ?? false;
            StatusText = state.ErrorMessage ??
                (CanControlVolume || CanControlMute
                    ? _text.ControlsAvailableStatusText
                    : _text.ControlsUnavailableStatusText);
        }
        finally
        {
            _isUpdatingState = false;
        }
    }

    public bool SetMuteState(bool targetState)
    {
        if (SelectedDevice is null)
        {
            return false;
        }

        if (_audioDeviceService.TrySetMute(SelectedDevice.Id, targetState, out var errorMessage))
        {
            IsMuted = targetState;
            StatusText = targetState ? _text.MutedStatusText : _text.UnmutedStatusText;
            return true;
        }

        StatusText = errorMessage ?? _text.MuteFailureText;
        return false;
    }

    public void SetStatusText(string statusText)
    {
        StatusText = statusText;
    }

    public async Task FlushPendingVolumeSetAsync()
    {
        Task pendingVolumeSet;
        string? deviceId;
        double? volumePercent;

        lock (_volumeSetSync)
        {
            _volumeSetDebounce?.Cancel();
            _volumeSetDebounce?.Dispose();
            _volumeSetDebounce = null;
            pendingVolumeSet = _pendingVolumeSetTask;
            _pendingVolumeSetTask = Task.CompletedTask;
            deviceId = _pendingVolumeDeviceId;
            volumePercent = _pendingVolumePercent;
            _pendingVolumeDeviceId = null;
            _pendingVolumePercent = null;
        }

        await pendingVolumeSet;

        if (deviceId is not null && volumePercent is not null)
        {
            SetVolumeCore(deviceId, volumePercent.Value);
        }
    }

    private IReadOnlyList<AudioDeviceInfo> GetDevices()
    {
        return Direction == AudioDirection.Output
            ? _audioDeviceService.GetOutputDevices()
            : _audioDeviceService.GetInputDevices();
    }

    private void ToggleMute()
    {
        SetMuteState(!IsMuted);
    }

    private void SetVolumePreset(double volumePercent)
    {
        var device = SelectedDevice;
        if (device is null)
        {
            return;
        }

        // Write the volume immediately (bypassing the slider debounce) so the
        // unmute below never plays audio at the previous volume.
        CancelPendingVolumeSet();
        _isUpdatingState = true;
        try
        {
            SelectedVolume = volumePercent;
        }
        finally
        {
            _isUpdatingState = false;
        }

        SetVolumeCore(device.Id, volumePercent);

        if (IsMuted && CanControlMute)
        {
            SetMuteState(false);
        }
    }

    private bool CanSetVolumePreset()
    {
        return SelectedDevice is not null && CanControlVolume;
    }

    private void RaiseVolumePresetCanExecuteChanged()
    {
        SetVolume0Command.RaiseCanExecuteChanged();
        SetVolume30Command.RaiseCanExecuteChanged();
        SetVolume35Command.RaiseCanExecuteChanged();
        SetVolume40Command.RaiseCanExecuteChanged();
        SetVolume45Command.RaiseCanExecuteChanged();
        SetVolume60Command.RaiseCanExecuteChanged();
    }

    private static AudioDeviceInfo? PickPreferred(IReadOnlyList<AudioDeviceInfo> devices, string? savedId)
    {
        if (devices.Count == 0)
        {
            return null;
        }

        return devices.FirstOrDefault(device => device.Id == savedId)
            ?? devices.FirstOrDefault(device => device.IsDefault)
            ?? devices[0];
    }

    private static string GetDefaultText(string label, IReadOnlyList<AudioDeviceInfo> devices)
    {
        var defaultDevice = devices.FirstOrDefault(device => device.IsDefault);
        return defaultDevice is null
            ? $"{label}: none reported"
            : $"{label}: {defaultDevice.Name}";
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private void QueueVolumeSet(string deviceId, double volumePercent)
    {
        if (_volumeSetDelay <= TimeSpan.Zero)
        {
            SetVolumeCore(deviceId, volumePercent);
            return;
        }

        CancellationTokenSource debounce;
        lock (_volumeSetSync)
        {
            _volumeSetDebounce?.Cancel();
            _volumeSetDebounce?.Dispose();
            debounce = new CancellationTokenSource();
            _volumeSetDebounce = debounce;
            _pendingVolumeDeviceId = deviceId;
            _pendingVolumePercent = volumePercent;
            _pendingVolumeSetTask = SetVolumeAfterDelayAsync(deviceId, volumePercent, debounce.Token);
        }
    }

    private async Task SetVolumeAfterDelayAsync(string deviceId, double volumePercent, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_volumeSetDelay, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SetVolumeCore(deviceId, volumePercent);

            lock (_volumeSetSync)
            {
                if (string.Equals(_pendingVolumeDeviceId, deviceId, StringComparison.OrdinalIgnoreCase) &&
                    _pendingVolumePercent == volumePercent)
                {
                    _pendingVolumeDeviceId = null;
                    _pendingVolumePercent = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelPendingVolumeSet()
    {
        lock (_volumeSetSync)
        {
            _volumeSetDebounce?.Cancel();
            _volumeSetDebounce?.Dispose();
            _volumeSetDebounce = null;
            _pendingVolumeDeviceId = null;
            _pendingVolumePercent = null;
        }
    }

    private void SetVolumeCore(string deviceId, double volumePercent)
    {
        if (!_audioDeviceService.TrySetVolume(deviceId, volumePercent, out var errorMessage))
        {
            StatusText = errorMessage ?? _text.VolumeFailureText;
        }
    }

    private sealed record AudioEndpointText(
        string NotLoadedStatusText,
        string DefaultLabel,
        string EmptyDevicesStatusText,
        string AvailableDevicesStatusText,
        string EnumerationFailurePrefix,
        string ControlsAvailableStatusText,
        string ControlsUnavailableStatusText,
        string VolumeFailureText,
        string MuteFailureText,
        string MutedStatusText,
        string UnmutedStatusText)
    {
        public static AudioEndpointText For(AudioDirection direction)
        {
            return direction == AudioDirection.Output
                ? new AudioEndpointText(
                    "Output devices have not been loaded.",
                    "Default output",
                    "No active output devices were found.",
                    "output device(s) available.",
                    "Unable to enumerate output devices.",
                    "Output endpoint controls are available.",
                    "The selected output device does not expose endpoint volume or mute control.",
                    "Unable to set output volume.",
                    "Unable to change output mute state.",
                    "Output muted.",
                    "Output unmuted.")
                : new AudioEndpointText(
                    "Input devices have not been loaded.",
                    "Default input",
                    "No active microphone/input devices were found.",
                    "input device(s) available.",
                    "Unable to enumerate input devices.",
                    "Microphone endpoint controls are available.",
                    "The selected input device does not expose endpoint volume or mute control.",
                    "Unable to set input volume.",
                    "Unable to change input mute state.",
                    "Input muted.",
                    "Input unmuted.");
        }
    }
}
