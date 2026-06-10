using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AudioCameraControlPanel.Models;
using AudioCameraControlPanel.Services;

namespace AudioCameraControlPanel.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly DispatcherTimer _inputMeterTimer;
    private readonly TimeSpan _settingsSaveDelay;
    private readonly object _settingsSaveSync = new();
    private AppSettings _appSettings = new();
    private CancellationTokenSource? _settingsSaveDebounce;
    private Task _pendingSettingsSaveTask = Task.CompletedTask;

    public event EventHandler<CompactWidgetKind>? CompactWidgetRequested;

    public event EventHandler? ControlPanelRequested;

    public MainViewModel(
        IAudioDeviceService audioDeviceService,
        ICameraDeviceService cameraDeviceService,
        ISettingsLauncherService settingsLauncherService,
        IAppSettingsStore settingsStore,
        TimeSpan? settingsSaveDelay = null,
        TimeSpan? volumeSetDelay = null)
    {
        _audioDeviceService = audioDeviceService;
        _settingsStore = settingsStore;
        _settingsSaveDelay = settingsSaveDelay ?? TimeSpan.FromMilliseconds(250);

        OutputEndpoint = new AudioEndpointViewModel(audioDeviceService, AudioDirection.Output, volumeSetDelay);
        InputEndpoint = new AudioEndpointViewModel(audioDeviceService, AudioDirection.Input, volumeSetDelay);
        CameraPreview = new CameraPreviewViewModel(cameraDeviceService);
        SettingsShortcuts = new SettingsShortcutsViewModel(settingsLauncherService);

        RefreshOutputDevicesCommand = new AsyncRelayCommand(RefreshOutputDevicesAsync);
        RefreshInputDevicesCommand = new AsyncRelayCommand(RefreshInputDevicesAsync);
        RefreshCamerasCommand = new AsyncRelayCommand(RefreshCamerasAsync);
        RefreshAllDevicesCommand = new AsyncRelayCommand(RefreshAllDevicesAsync);
        MuteAllCommand = new RelayCommand(MuteAll, () => CanControlOutputMute || CanControlInputMute);

        ToggleOutputMuteCommand = OutputEndpoint.ToggleMuteCommand;
        ToggleInputMuteCommand = InputEndpoint.ToggleMuteCommand;
        SetOutputVolume0Command = OutputEndpoint.SetVolume0Command;
        SetOutputVolume30Command = OutputEndpoint.SetVolume30Command;
        SetOutputVolume35Command = OutputEndpoint.SetVolume35Command;
        SetOutputVolume40Command = OutputEndpoint.SetVolume40Command;
        SetOutputVolume45Command = OutputEndpoint.SetVolume45Command;
        SetOutputVolume60Command = OutputEndpoint.SetVolume60Command;
        SetInputVolume0Command = InputEndpoint.SetVolume0Command;
        SetInputVolume30Command = InputEndpoint.SetVolume30Command;
        SetInputVolume35Command = InputEndpoint.SetVolume35Command;
        SetInputVolume40Command = InputEndpoint.SetVolume40Command;
        SetInputVolume45Command = InputEndpoint.SetVolume45Command;
        SetInputVolume60Command = InputEndpoint.SetVolume60Command;
        ToggleCameraPreviewCommand = CameraPreview.TogglePreviewCommand;
        OpenSoundSettingsCommand = SettingsShortcuts.OpenSoundSettingsCommand;
        OpenAppVolumeSettingsCommand = SettingsShortcuts.OpenAppVolumeSettingsCommand;
        OpenCameraSettingsCommand = SettingsShortcuts.OpenCameraSettingsCommand;
        OpenCameraPrivacyCommand = SettingsShortcuts.OpenCameraPrivacyCommand;
        OpenMicrophonePrivacyCommand = SettingsShortcuts.OpenMicrophonePrivacyCommand;

        OpenMasterWidgetCommand = new RelayCommand(() => RequestCompactWidget(CompactWidgetKind.Master));
        OpenOutputWidgetCommand = new RelayCommand(() => RequestCompactWidget(CompactWidgetKind.Output));
        OpenInputWidgetCommand = new RelayCommand(() => RequestCompactWidget(CompactWidgetKind.Input));
        OpenCameraWidgetCommand = new RelayCommand(() => RequestCompactWidget(CompactWidgetKind.Camera));
        OpenSettingsWidgetCommand = new RelayCommand(() => RequestCompactWidget(CompactWidgetKind.Settings));
        OpenControlPanelCommand = new RelayCommand(RequestControlPanel);

        OutputEndpoint.PropertyChanged += OnOutputEndpointPropertyChanged;
        InputEndpoint.PropertyChanged += OnInputEndpointPropertyChanged;
        CameraPreview.PropertyChanged += OnCameraPreviewPropertyChanged;
        SettingsShortcuts.LaunchFailed += OnSettingsLaunchFailed;
        _audioDeviceService.DevicesChanged += OnAudioDevicesChanged;
        _audioDeviceService.EndpointStateChanged += OnAudioEndpointStateChanged;
        OutputEndpoint.SelectedDeviceChanged += OnOutputDeviceChanged;
        InputEndpoint.SelectedDeviceChanged += OnInputDeviceChanged;
        CameraPreview.SelectedCameraChanged += OnSelectedCameraChanged;

        _inputMeterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _inputMeterTimer.Tick += OnInputMeterTick;
        _inputMeterTimer.Start();
    }

    public AudioEndpointViewModel OutputEndpoint { get; }

    public AudioEndpointViewModel InputEndpoint { get; }

    public CameraPreviewViewModel CameraPreview { get; }

    public SettingsShortcutsViewModel SettingsShortcuts { get; }

    public ObservableCollection<AudioDeviceInfo> OutputDevices => OutputEndpoint.Devices;

    public ObservableCollection<AudioDeviceInfo> InputDevices => InputEndpoint.Devices;

    public ObservableCollection<CameraDeviceInfo> Cameras => CameraPreview.Cameras;

    public AsyncRelayCommand RefreshOutputDevicesCommand { get; }

    public AsyncRelayCommand RefreshInputDevicesCommand { get; }

    public AsyncRelayCommand RefreshCamerasCommand { get; }

    public RelayCommand ToggleOutputMuteCommand { get; }

    public RelayCommand ToggleInputMuteCommand { get; }

    public RelayCommand MuteAllCommand { get; }

    public AsyncRelayCommand RefreshAllDevicesCommand { get; }

    public RelayCommand SetOutputVolume0Command { get; }

    public RelayCommand SetOutputVolume30Command { get; }

    public RelayCommand SetOutputVolume35Command { get; }

    public RelayCommand SetOutputVolume40Command { get; }

    public RelayCommand SetOutputVolume45Command { get; }

    public RelayCommand SetOutputVolume60Command { get; }

    public RelayCommand SetInputVolume0Command { get; }

    public RelayCommand SetInputVolume30Command { get; }

    public RelayCommand SetInputVolume35Command { get; }

    public RelayCommand SetInputVolume40Command { get; }

    public RelayCommand SetInputVolume45Command { get; }

    public RelayCommand SetInputVolume60Command { get; }

    public AsyncRelayCommand ToggleCameraPreviewCommand { get; }

    public RelayCommand OpenMasterWidgetCommand { get; }

    public RelayCommand OpenOutputWidgetCommand { get; }

    public RelayCommand OpenInputWidgetCommand { get; }

    public RelayCommand OpenCameraWidgetCommand { get; }

    public RelayCommand OpenSettingsWidgetCommand { get; }

    public RelayCommand OpenControlPanelCommand { get; }

    public RelayCommand OpenSoundSettingsCommand { get; }

    public RelayCommand OpenAppVolumeSettingsCommand { get; }

    public RelayCommand OpenCameraSettingsCommand { get; }

    public RelayCommand OpenCameraPrivacyCommand { get; }

    public RelayCommand OpenMicrophonePrivacyCommand { get; }

    public AudioDeviceInfo? SelectedOutputDevice
    {
        get => OutputEndpoint.SelectedDevice;
        set => OutputEndpoint.SelectedDevice = value;
    }

    public AudioDeviceInfo? SelectedInputDevice
    {
        get => InputEndpoint.SelectedDevice;
        set => InputEndpoint.SelectedDevice = value;
    }

    public CameraDeviceInfo? SelectedCamera
    {
        get => CameraPreview.SelectedCamera;
        set => CameraPreview.SelectedCamera = value;
    }

    public double SelectedOutputVolume
    {
        get => OutputEndpoint.SelectedVolume;
        set => OutputEndpoint.SelectedVolume = value;
    }

    public double SelectedInputVolume
    {
        get => InputEndpoint.SelectedVolume;
        set => InputEndpoint.SelectedVolume = value;
    }

    public bool IsOutputMuted => OutputEndpoint.IsMuted;

    public bool IsInputMuted => InputEndpoint.IsMuted;

    public bool CanControlOutputVolume => OutputEndpoint.CanControlVolume;

    public bool CanControlInputVolume => InputEndpoint.CanControlVolume;

    public bool CanControlOutputMute => OutputEndpoint.CanControlMute;

    public bool CanControlInputMute => InputEndpoint.CanControlMute;

    public bool IsCameraPreviewRunning => CameraPreview.IsPreviewRunning;

    public bool IsCameraPreviewStopped => CameraPreview.IsPreviewStopped;

    public double InputPeakLevel => InputEndpoint.PeakLevel;

    public BitmapSource? CameraFrame => CameraPreview.Frame;

    public string DefaultOutputText => OutputEndpoint.DefaultText;

    public string DefaultInputText => InputEndpoint.DefaultText;

    public string OutputStatusText => OutputEndpoint.StatusText;

    public string InputStatusText => InputEndpoint.StatusText;

    public string CameraStatusText => CameraPreview.StatusText;

    public string OutputMuteButtonText => OutputEndpoint.MuteButtonText;

    public string InputMuteButtonText => InputEndpoint.MuteButtonText;

    public string CameraPreviewButtonText => CameraPreview.PreviewButtonText;

    public string CameraPreviewStateText => CameraPreview.PreviewStateText;

    public async Task InitializeAsync()
    {
        _appSettings = await _settingsStore.LoadAsync();
        await RefreshAllDevicesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _inputMeterTimer.Stop();
        _inputMeterTimer.Tick -= OnInputMeterTick;

        OutputEndpoint.PropertyChanged -= OnOutputEndpointPropertyChanged;
        InputEndpoint.PropertyChanged -= OnInputEndpointPropertyChanged;
        CameraPreview.PropertyChanged -= OnCameraPreviewPropertyChanged;
        SettingsShortcuts.LaunchFailed -= OnSettingsLaunchFailed;
        _audioDeviceService.DevicesChanged -= OnAudioDevicesChanged;
        _audioDeviceService.EndpointStateChanged -= OnAudioEndpointStateChanged;
        OutputEndpoint.SelectedDeviceChanged -= OnOutputDeviceChanged;
        InputEndpoint.SelectedDeviceChanged -= OnInputDeviceChanged;
        CameraPreview.SelectedCameraChanged -= OnSelectedCameraChanged;

        await CameraPreview.DisposeAsync();
        await OutputEndpoint.FlushPendingVolumeSetAsync();
        await InputEndpoint.FlushPendingVolumeSetAsync();
        await FlushSettingsSaveAsync();
        _audioDeviceService.Dispose();
    }

    private Task RefreshOutputDevicesAsync()
    {
        return OutputEndpoint.RefreshDevicesAsync(_appSettings.LastOutputDeviceId);
    }

    private Task RefreshInputDevicesAsync()
    {
        return InputEndpoint.RefreshDevicesAsync(_appSettings.LastInputDeviceId);
    }

    private Task RefreshCamerasAsync()
    {
        return CameraPreview.RefreshCamerasAsync(_appSettings.LastCameraDeviceId);
    }

    private async Task RefreshAllDevicesAsync()
    {
        await RefreshOutputDevicesAsync();
        await RefreshInputDevicesAsync();
        await RefreshCamerasAsync();
    }

    private void MuteAll()
    {
        var changedAny = false;

        if (CanControlOutputMute && SelectedOutputDevice is not null)
        {
            changedAny |= OutputEndpoint.SetMuteState(true);
        }

        if (CanControlInputMute && SelectedInputDevice is not null)
        {
            changedAny |= InputEndpoint.SetMuteState(true);
        }

        if (!changedAny)
        {
            OutputEndpoint.SetStatusText("No selected audio endpoint exposes mute control.");
        }
    }

    private void OnInputMeterTick(object? sender, EventArgs e)
    {
        InputEndpoint.RefreshPeakLevel();
    }

    private void OnOutputDeviceChanged(object? sender, EventArgs e)
    {
        _appSettings.LastOutputDeviceId = SelectedOutputDevice?.Id;
        _ = QueueSaveSettingsAsync();
    }

    private void OnInputDeviceChanged(object? sender, EventArgs e)
    {
        _appSettings.LastInputDeviceId = SelectedInputDevice?.Id;
        _ = QueueSaveSettingsAsync();
    }

    private void OnSelectedCameraChanged(object? sender, EventArgs e)
    {
        _appSettings.LastCameraDeviceId = SelectedCamera?.Id;
        _ = QueueSaveSettingsAsync();
    }

    private void OnAudioDevicesChanged(object? sender, AudioDevicesChangedEventArgs e)
    {
        _ = RunOnApplicationDispatcherAsync(async () =>
        {
            if (e.Direction is null or AudioDirection.Output)
            {
                await RefreshOutputDevicesAsync();
            }

            if (e.Direction is null or AudioDirection.Input)
            {
                await RefreshInputDevicesAsync();
            }
        });
    }

    private void OnAudioEndpointStateChanged(object? sender, AudioEndpointChangedEventArgs e)
    {
        _ = RunOnApplicationDispatcherAsync(() =>
        {
            if (string.Equals(SelectedOutputDevice?.Id, e.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                OutputEndpoint.RefreshEndpointState();
            }

            if (string.Equals(SelectedInputDevice?.Id, e.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                InputEndpoint.RefreshEndpointState();
            }

            return Task.CompletedTask;
        });
    }

    private void OnSettingsLaunchFailed(object? sender, string message)
    {
        CameraPreview.SetStatusText(message);
    }

    private void OnOutputEndpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AudioEndpointViewModel.SelectedDevice):
                OnPropertyChanged(nameof(SelectedOutputDevice));
                MuteAllCommand.RaiseCanExecuteChanged();
                break;
            case nameof(AudioEndpointViewModel.SelectedVolume):
                OnPropertyChanged(nameof(SelectedOutputVolume));
                break;
            case nameof(AudioEndpointViewModel.IsMuted):
                OnPropertyChanged(nameof(IsOutputMuted));
                break;
            case nameof(AudioEndpointViewModel.CanControlVolume):
                OnPropertyChanged(nameof(CanControlOutputVolume));
                break;
            case nameof(AudioEndpointViewModel.CanControlMute):
                OnPropertyChanged(nameof(CanControlOutputMute));
                MuteAllCommand.RaiseCanExecuteChanged();
                break;
            case nameof(AudioEndpointViewModel.DefaultText):
                OnPropertyChanged(nameof(DefaultOutputText));
                break;
            case nameof(AudioEndpointViewModel.StatusText):
                OnPropertyChanged(nameof(OutputStatusText));
                break;
            case nameof(AudioEndpointViewModel.MuteButtonText):
                OnPropertyChanged(nameof(OutputMuteButtonText));
                break;
        }
    }

    private void OnInputEndpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AudioEndpointViewModel.SelectedDevice):
                OnPropertyChanged(nameof(SelectedInputDevice));
                MuteAllCommand.RaiseCanExecuteChanged();
                break;
            case nameof(AudioEndpointViewModel.SelectedVolume):
                OnPropertyChanged(nameof(SelectedInputVolume));
                break;
            case nameof(AudioEndpointViewModel.IsMuted):
                OnPropertyChanged(nameof(IsInputMuted));
                break;
            case nameof(AudioEndpointViewModel.CanControlVolume):
                OnPropertyChanged(nameof(CanControlInputVolume));
                break;
            case nameof(AudioEndpointViewModel.CanControlMute):
                OnPropertyChanged(nameof(CanControlInputMute));
                MuteAllCommand.RaiseCanExecuteChanged();
                break;
            case nameof(AudioEndpointViewModel.PeakLevel):
                OnPropertyChanged(nameof(InputPeakLevel));
                break;
            case nameof(AudioEndpointViewModel.DefaultText):
                OnPropertyChanged(nameof(DefaultInputText));
                break;
            case nameof(AudioEndpointViewModel.StatusText):
                OnPropertyChanged(nameof(InputStatusText));
                break;
            case nameof(AudioEndpointViewModel.MuteButtonText):
                OnPropertyChanged(nameof(InputMuteButtonText));
                break;
        }
    }

    private void OnCameraPreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CameraPreviewViewModel.SelectedCamera):
                OnPropertyChanged(nameof(SelectedCamera));
                break;
            case nameof(CameraPreviewViewModel.IsPreviewRunning):
                OnPropertyChanged(nameof(IsCameraPreviewRunning));
                OnPropertyChanged(nameof(IsCameraPreviewStopped));
                OnPropertyChanged(nameof(CameraPreviewButtonText));
                OnPropertyChanged(nameof(CameraPreviewStateText));
                break;
            case nameof(CameraPreviewViewModel.IsPreviewStopped):
                OnPropertyChanged(nameof(IsCameraPreviewStopped));
                break;
            case nameof(CameraPreviewViewModel.Frame):
                OnPropertyChanged(nameof(CameraFrame));
                break;
            case nameof(CameraPreviewViewModel.StatusText):
                OnPropertyChanged(nameof(CameraStatusText));
                break;
            case nameof(CameraPreviewViewModel.PreviewButtonText):
                OnPropertyChanged(nameof(CameraPreviewButtonText));
                break;
            case nameof(CameraPreviewViewModel.PreviewStateText):
                OnPropertyChanged(nameof(CameraPreviewStateText));
                break;
        }
    }

    private void RequestCompactWidget(CompactWidgetKind kind)
    {
        CompactWidgetRequested?.Invoke(this, kind);
    }

    private void RequestControlPanel()
    {
        ControlPanelRequested?.Invoke(this, EventArgs.Empty);
    }

    private Task QueueSaveSettingsAsync()
    {
        CancellationTokenSource debounce;
        lock (_settingsSaveSync)
        {
            _settingsSaveDebounce?.Cancel();
            _settingsSaveDebounce?.Dispose();
            debounce = new CancellationTokenSource();
            _settingsSaveDebounce = debounce;
            _pendingSettingsSaveTask = SaveSettingsAfterDelayAsync(debounce.Token);
            return _pendingSettingsSaveTask;
        }
    }

    private async Task SaveSettingsAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_settingsSaveDelay > TimeSpan.Zero)
            {
                await Task.Delay(_settingsSaveDelay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await SaveSettingsSafelyAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task FlushSettingsSaveAsync()
    {
        Task pendingSave;
        lock (_settingsSaveSync)
        {
            _settingsSaveDebounce?.Cancel();
            _settingsSaveDebounce?.Dispose();
            _settingsSaveDebounce = null;
            pendingSave = _pendingSettingsSaveTask;
            _pendingSettingsSaveTask = Task.CompletedTask;
        }

        await pendingSave;
        await SaveSettingsSafelyAsync();
    }

    private async Task SaveSettingsSafelyAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_appSettings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            OutputEndpoint.SetStatusText($"Unable to save app settings. {ex.Message}");
        }
    }

    private static Task RunOnApplicationDispatcherAsync(Func<Task> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.InvokeAsync(action).Task.Unwrap();
    }
}
