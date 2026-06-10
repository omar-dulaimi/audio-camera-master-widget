using AudioCameraControlPanel.Models;
using AudioCameraControlPanel.Services;
using AudioCameraControlPanel.Tests.Fakes;
using AudioCameraControlPanel.ViewModels;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AudioCameraControlPanel.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public async Task InitializeAsyncRestoresSavedOutputInputAndCameraSelections()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 50, isMuted: false)
            .WithOutput("output-2", "Headphones", volumePercent: 25, isMuted: true)
            .WithInput("input-1", "Laptop mic", isDefault: true, volumePercent: 30, isMuted: false)
            .WithInput("input-2", "USB mic", volumePercent: 45, isMuted: true)
            .WithCamera("camera-1", "Integrated camera")
            .WithCamera("camera-2", "USB camera")
            .WithSettings(new AppSettings
            {
                LastOutputDeviceId = "output-2",
                LastInputDeviceId = "input-2",
                LastCameraDeviceId = "camera-2"
            })
            .Build();

        await context.ViewModel.InitializeAsync();

        Assert.AreEqual("output-2", context.ViewModel.SelectedOutputDevice?.Id);
        Assert.AreEqual("input-2", context.ViewModel.SelectedInputDevice?.Id);
        Assert.AreEqual("camera-2", context.ViewModel.SelectedCamera?.Id);
        Assert.AreEqual(25, context.ViewModel.SelectedOutputVolume);
        Assert.AreEqual(45, context.ViewModel.SelectedInputVolume);
        Assert.IsTrue(context.ViewModel.IsOutputMuted);
        Assert.IsTrue(context.ViewModel.IsInputMuted);
    }

    [TestMethod]
    public async Task InitializeAsyncFallsBackToDefaultAudioDevicesWhenSavedIdsAreMissing()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Monitor", volumePercent: 10, isMuted: false)
            .WithOutput("output-2", "Speakers", isDefault: true, volumePercent: 70, isMuted: false)
            .WithInput("input-1", "Webcam mic", volumePercent: 20, isMuted: false)
            .WithInput("input-2", "USB mic", isDefault: true, volumePercent: 60, isMuted: false)
            .WithCamera("camera-1", "Camera")
            .WithSettings(new AppSettings
            {
                LastOutputDeviceId = "missing-output",
                LastInputDeviceId = "missing-input",
                LastCameraDeviceId = "missing-camera"
            })
            .Build();

        await context.ViewModel.InitializeAsync();

        Assert.AreEqual("output-2", context.ViewModel.SelectedOutputDevice?.Id);
        Assert.AreEqual("input-2", context.ViewModel.SelectedInputDevice?.Id);
        Assert.AreEqual("camera-1", context.ViewModel.SelectedCamera?.Id);
        Assert.AreEqual(70, context.ViewModel.SelectedOutputVolume);
        Assert.AreEqual(60, context.ViewModel.SelectedInputVolume);
    }

    [TestMethod]
    public async Task OutputVolumePresetWritesEndpointVolumeWhenSupported()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 20, isMuted: false)
            .Build();
        await context.ViewModel.InitializeAsync();

        context.ViewModel.SetOutputVolume45Command.Execute(null);

        Assert.HasCount(1, context.Audio.SetVolumeCalls);
        Assert.AreEqual("output-1", context.Audio.SetVolumeCalls[0].DeviceId);
        Assert.AreEqual(45, context.Audio.SetVolumeCalls[0].VolumePercent);
        Assert.AreEqual(45, context.Audio.EndpointStates["output-1"].VolumePercent);
    }

    [TestMethod]
    public async Task OutputVolumePresetUnmutesMutedEndpoint()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 80, isMuted: true)
            .Build();
        await context.ViewModel.InitializeAsync();

        context.ViewModel.SetOutputVolume30Command.Execute(null);

        Assert.HasCount(1, context.Audio.SetVolumeCalls);
        Assert.AreEqual(30, context.Audio.SetVolumeCalls[0].VolumePercent);
        Assert.HasCount(1, context.Audio.SetMuteCalls);
        Assert.AreEqual("output-1", context.Audio.SetMuteCalls[0].DeviceId);
        Assert.IsFalse(context.Audio.SetMuteCalls[0].IsMuted);
        Assert.IsFalse(context.ViewModel.IsOutputMuted);
        Assert.AreEqual(30, context.ViewModel.SelectedOutputVolume);
    }

    [TestMethod]
    public async Task ZeroVolumePresetUnmutesMutedEndpoint()
    {
        var context = new TestContextBuilder()
            .WithInput("input-1", "Mic", isDefault: true, volumePercent: 70, isMuted: true)
            .Build();
        await context.ViewModel.InitializeAsync();

        context.ViewModel.SetInputVolume0Command.Execute(null);

        Assert.AreEqual(0, context.ViewModel.SelectedInputVolume);
        Assert.IsFalse(context.ViewModel.IsInputMuted);
        Assert.AreEqual(0, context.Audio.EndpointStates["input-1"].VolumePercent);
        Assert.AreEqual(false, context.Audio.EndpointStates["input-1"].IsMuted);
    }

    [TestMethod]
    public async Task IntermediateVolumePresetsWriteEndpointVolume()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 20, isMuted: false)
            .WithInput("input-1", "Mic", isDefault: true, volumePercent: 20, isMuted: false)
            .Build();
        await context.ViewModel.InitializeAsync();

        context.ViewModel.SetOutputVolume35Command.Execute(null);
        context.ViewModel.SetInputVolume40Command.Execute(null);

        Assert.AreEqual(35, context.Audio.EndpointStates["output-1"].VolumePercent);
        Assert.AreEqual(40, context.Audio.EndpointStates["input-1"].VolumePercent);
        Assert.HasCount(0, context.Audio.SetMuteCalls);
    }

    [TestMethod]
    public async Task VolumePresetSkipsUnmuteWhenMuteControlUnavailable()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 80, isMuted: true, canControlMute: false)
            .Build();
        await context.ViewModel.InitializeAsync();

        context.ViewModel.SetOutputVolume45Command.Execute(null);

        Assert.AreEqual(45, context.ViewModel.SelectedOutputVolume);
        Assert.IsTrue(context.ViewModel.IsOutputMuted);
        Assert.HasCount(0, context.Audio.SetMuteCalls);
    }

    [TestMethod]
    public async Task MuteAllOnlyTouchesEndpointsThatSupportMute()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 30, isMuted: false, canControlMute: true)
            .WithInput("input-1", "Mic", isDefault: true, volumePercent: 30, isMuted: false, canControlMute: false)
            .Build();
        await context.ViewModel.InitializeAsync();

        context.ViewModel.MuteAllCommand.Execute(null);

        Assert.HasCount(1, context.Audio.SetMuteCalls);
        Assert.AreEqual("output-1", context.Audio.SetMuteCalls[0].DeviceId);
        Assert.IsTrue(context.Audio.SetMuteCalls[0].IsMuted);
        Assert.IsTrue(context.ViewModel.IsOutputMuted);
        Assert.IsFalse(context.ViewModel.IsInputMuted);
    }

    [TestMethod]
    public async Task SaveFailureUpdatesStatusWithoutThrowing()
    {
        var saveException = new IOException("settings are locked");
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 20, isMuted: false)
            .WithOutput("output-2", "Headphones", volumePercent: 35, isMuted: false)
            .WithSettingsSaveException(saveException)
            .Build();
        await context.ViewModel.InitializeAsync();

        context.ViewModel.SelectedOutputDevice = context.ViewModel.OutputDevices.Single(device => device.Id == "output-2");
        await context.ViewModel.DisposeAsync();

        StringAssert.Contains(context.ViewModel.OutputStatusText, "Unable to save app settings.");
        StringAssert.Contains(context.ViewModel.OutputStatusText, "settings are locked");
    }

    [TestMethod]
    public async Task CameraPreviewErrorStopsPreviewAndClearsFrame()
    {
        var context = new TestContextBuilder()
            .WithCamera("camera-1", "Camera")
            .Build();
        await context.ViewModel.InitializeAsync();
        await context.ViewModel.ToggleCameraPreviewCommand.ExecuteAsyncForTest();
        context.Camera.RaiseFrameReady(CreateTestFrame());
        Assert.IsTrue(context.ViewModel.IsCameraPreviewRunning);
        Assert.IsNotNull(context.ViewModel.CameraFrame);

        context.Camera.RaisePreviewError("camera disconnected");

        Assert.IsFalse(context.ViewModel.IsCameraPreviewRunning);
        Assert.IsNull(context.ViewModel.CameraFrame);
        StringAssert.Contains(context.ViewModel.CameraStatusText, "camera disconnected");
    }

    [TestMethod]
    public async Task ChildViewModelsStayInSyncWithLegacyBindings()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 20, isMuted: false)
            .WithInput("input-1", "Mic", isDefault: true, volumePercent: 35, isMuted: true)
            .WithCamera("camera-1", "Camera")
            .Build();

        await context.ViewModel.InitializeAsync();

        Assert.AreSame(context.ViewModel.OutputDevices, context.ViewModel.OutputEndpoint.Devices);
        Assert.AreSame(context.ViewModel.InputDevices, context.ViewModel.InputEndpoint.Devices);
        Assert.AreSame(context.ViewModel.Cameras, context.ViewModel.CameraPreview.Cameras);
        Assert.AreSame(context.ViewModel.SelectedOutputDevice, context.ViewModel.OutputEndpoint.SelectedDevice);
        Assert.AreSame(context.ViewModel.SelectedInputDevice, context.ViewModel.InputEndpoint.SelectedDevice);
        Assert.AreSame(context.ViewModel.SelectedCamera, context.ViewModel.CameraPreview.SelectedCamera);

        context.ViewModel.OutputEndpoint.SelectedVolume = 45;
        context.ViewModel.CameraPreview.SelectedCamera = null;

        Assert.AreEqual(45, context.ViewModel.SelectedOutputVolume);
        Assert.IsNull(context.ViewModel.SelectedCamera);
        Assert.IsFalse(context.ViewModel.ToggleCameraPreviewCommand.CanExecute(null));
    }

    [TestMethod]
    public void OpenControlPanelCommandRaisesControlPanelRequest()
    {
        var context = new TestContextBuilder().Build();
        var requestCount = 0;
        context.ViewModel.ControlPanelRequested += (_, _) => requestCount++;

        context.ViewModel.OpenControlPanelCommand.Execute(null);

        Assert.AreEqual(1, requestCount);
    }

    [TestMethod]
    public async Task RapidSelectionChangesCollapseIntoSingleDebouncedSave()
    {
        var context = new TestContextBuilder(TimeSpan.FromMilliseconds(25))
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 20, isMuted: false)
            .WithOutput("output-2", "Headphones", volumePercent: 30, isMuted: false)
            .WithOutput("output-3", "Monitor", volumePercent: 40, isMuted: false)
            .Build();
        await context.ViewModel.InitializeAsync();
        context.SettingsStore.ResetHistory();

        context.ViewModel.SelectedOutputDevice = context.ViewModel.OutputDevices.Single(device => device.Id == "output-2");
        context.ViewModel.SelectedOutputDevice = context.ViewModel.OutputDevices.Single(device => device.Id == "output-3");
        await Task.Delay(100);

        Assert.AreEqual(1, context.SettingsStore.SaveCount);
        Assert.AreEqual("output-3", context.SettingsStore.LastSavedSettings?.LastOutputDeviceId);
    }

    [TestMethod]
    public async Task DisposeAsyncFlushesPendingSettingsSave()
    {
        var context = new TestContextBuilder(TimeSpan.FromMinutes(1))
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 20, isMuted: false)
            .WithOutput("output-2", "Headphones", volumePercent: 30, isMuted: false)
            .Build();
        await context.ViewModel.InitializeAsync();
        context.SettingsStore.ResetHistory();

        context.ViewModel.SelectedOutputDevice = context.ViewModel.OutputDevices.Single(device => device.Id == "output-2");
        await context.ViewModel.DisposeAsync();

        Assert.AreEqual(1, context.SettingsStore.SaveCount);
        Assert.AreEqual("output-2", context.SettingsStore.LastSavedSettings?.LastOutputDeviceId);
    }

    [TestMethod]
    public async Task RapidOutputVolumeChangesOnlyWriteFinalValue()
    {
        var context = new TestContextBuilder(volumeSetDelay: TimeSpan.FromMilliseconds(25))
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 20, isMuted: false)
            .Build();
        await context.ViewModel.InitializeAsync();
        context.Audio.SetVolumeCalls.Clear();

        context.ViewModel.SelectedOutputVolume = 30;
        context.ViewModel.SelectedOutputVolume = 45;
        context.ViewModel.SelectedOutputVolume = 60;
        await Task.Delay(100);

        Assert.HasCount(1, context.Audio.SetVolumeCalls);
        Assert.AreEqual(60, context.Audio.SetVolumeCalls[0].VolumePercent);
    }

    [TestMethod]
    public async Task AudioDeviceNotificationRefreshesAffectedDeviceList()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 20, isMuted: false)
            .WithInput("input-1", "Mic", isDefault: true, volumePercent: 30, isMuted: false)
            .Build();
        await context.ViewModel.InitializeAsync();

        context.Audio.OutputDevices.Clear();
        context.Audio.AddOutputDevice("output-2", "Headphones", isDefault: true);
        context.Audio.SetEndpointState("output-2", 55, false);
        context.Audio.RaiseDevicesChanged(AudioDirection.Output, "output-2", isDefaultChange: true);

        Assert.HasCount(1, context.ViewModel.OutputDevices);
        Assert.AreEqual("output-2", context.ViewModel.SelectedOutputDevice?.Id);
        Assert.AreEqual(55, context.ViewModel.SelectedOutputVolume);
        Assert.HasCount(1, context.ViewModel.InputDevices);
        Assert.AreEqual("input-1", context.ViewModel.SelectedInputDevice?.Id);
    }

    [TestMethod]
    public async Task EndpointStateNotificationRefreshesSelectedEndpoint()
    {
        var context = new TestContextBuilder()
            .WithOutput("output-1", "Speakers", isDefault: true, volumePercent: 20, isMuted: false)
            .Build();
        await context.ViewModel.InitializeAsync();

        context.Audio.SetEndpointState("output-1", 75, true);
        context.Audio.RaiseEndpointStateChanged("output-1");

        Assert.AreEqual(75, context.ViewModel.SelectedOutputVolume);
        Assert.IsTrue(context.ViewModel.IsOutputMuted);
    }

    [TestMethod]
    public async Task SelectingNewCameraWhilePreviewRunsRestartsPreviewOnce()
    {
        var context = new TestContextBuilder()
            .WithCamera("camera-1", "Integrated camera")
            .WithCamera("camera-2", "USB camera")
            .Build();
        await context.ViewModel.InitializeAsync();
        await context.ViewModel.ToggleCameraPreviewCommand.ExecuteAsyncForTest();
        context.Camera.PreviewLifecycleCalls.Clear();

        context.ViewModel.SelectedCamera = context.ViewModel.Cameras.Single(camera => camera.Id == "camera-2");
        await WaitUntilAsync(() => context.Camera.PreviewLifecycleCalls.Count == 2);

        CollectionAssert.AreEqual(
            new[]
            {
                new FakeCameraDeviceService.PreviewLifecycleCall("Stop", "camera-1"),
                new FakeCameraDeviceService.PreviewLifecycleCall("Start", "camera-2")
            },
            context.Camera.PreviewLifecycleCalls);
        Assert.IsTrue(context.ViewModel.IsCameraPreviewRunning);
    }

    private sealed class TestContextBuilder
    {
        private readonly FakeAudioDeviceService _audio = new();
        private readonly FakeCameraDeviceService _camera = new();
        private readonly FakeSettingsLauncherService _settingsLauncher = new();
        private readonly InMemoryAppSettingsStore _settingsStore = new();
        private readonly TimeSpan _settingsSaveDelay;
        private readonly TimeSpan _volumeSetDelay;

        public TestContextBuilder()
            : this(TimeSpan.Zero)
        {
        }

        public TestContextBuilder(TimeSpan settingsSaveDelay = default, TimeSpan volumeSetDelay = default)
        {
            _settingsSaveDelay = settingsSaveDelay;
            _volumeSetDelay = volumeSetDelay;
        }

        public TestContextBuilder WithOutput(
            string id,
            string name,
            bool isDefault = false,
            double? volumePercent = null,
            bool? isMuted = null,
            bool canControlVolume = true,
            bool canControlMute = true)
        {
            _audio.AddOutputDevice(id, name, isDefault);
            _audio.SetEndpointState(id, volumePercent, isMuted, canControlVolume, canControlMute);
            return this;
        }

        public TestContextBuilder WithInput(
            string id,
            string name,
            bool isDefault = false,
            double? volumePercent = null,
            bool? isMuted = null,
            bool canControlVolume = true,
            bool canControlMute = true)
        {
            _audio.AddInputDevice(id, name, isDefault);
            _audio.SetEndpointState(id, volumePercent, isMuted, canControlVolume, canControlMute);
            return this;
        }

        public TestContextBuilder WithCamera(string id, string name)
        {
            _camera.AddCamera(id, name);
            return this;
        }

        public TestContextBuilder WithSettings(AppSettings settings)
        {
            _settingsStore.Settings = settings;
            return this;
        }

        public TestContextBuilder WithSettingsSaveException(Exception exception)
        {
            _settingsStore.SaveException = exception;
            return this;
        }

        public TestContext Build()
        {
            return new TestContext(
                _audio,
                _camera,
                _settingsLauncher,
                _settingsStore,
                new MainViewModel(
                    _audio,
                    _camera,
                    _settingsLauncher,
                    _settingsStore,
                    _settingsSaveDelay,
                    _volumeSetDelay));
        }
    }

    private sealed record TestContext(
        FakeAudioDeviceService Audio,
        FakeCameraDeviceService Camera,
        FakeSettingsLauncherService SettingsLauncher,
        InMemoryAppSettingsStore SettingsStore,
        MainViewModel ViewModel);

    private static BitmapSource CreateTestFrame()
    {
        var frame = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0, 0, 0, 255 },
            4);
        frame.Freeze();
        return frame;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (!predicate())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}

internal static class AsyncRelayCommandTestExtensions
{
    public static Task ExecuteAsyncForTest(this AsyncRelayCommand command)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (command.CanExecute(null))
            {
                command.CanExecuteChanged -= handler;
                completion.TrySetResult();
            }
        };

        command.CanExecuteChanged += handler;
        command.Execute(null);
        return command.CanExecute(null) ? Task.CompletedTask : completion.Task;
    }
}
