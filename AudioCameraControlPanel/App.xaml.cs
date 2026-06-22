using System.Windows;
using AudioCameraControlPanel.Models;
using AudioCameraControlPanel.Services;
using AudioCameraControlPanel.ViewModels;

namespace AudioCameraControlPanel;

public partial class App : Application
{
    private MainViewModel? _viewModel;
    private CompactWidgetWindow? _masterWidget;
    private MainWindow? _controlPanel;
    private bool _isShuttingDown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _viewModel = new MainViewModel(
            new AudioDeviceService(),
            new CameraDeviceService(),
            new SettingsLauncherService(),
            new JsonAppSettingsStore(),
            new WindowsStartupRegistrationService());

        _masterWidget = new CompactWidgetWindow(_viewModel, CompactWidgetKind.Master)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        _viewModel.ControlPanelRequested += OnControlPanelRequested;
        _masterWidget.Closed += OnMasterWidgetClosed;
        MainWindow = _masterWidget;
        _masterWidget.Show();

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex) when (!_isShuttingDown)
        {
            MessageBox.Show(
                $"The master widget could not initialize.\n\n{ex.Message}",
                "Audio Camera Master Widget",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            await ShutdownAsync(1);
        }
    }

    private async void OnMasterWidgetClosed(object? sender, EventArgs e)
    {
        await ShutdownAsync();
    }

    private void OnControlPanelRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_controlPanel is { IsVisible: true })
        {
            if (_controlPanel.WindowState == WindowState.Minimized)
            {
                _controlPanel.WindowState = WindowState.Normal;
            }

            _controlPanel.Activate();
            return;
        }

        _controlPanel = new MainWindow(_viewModel)
        {
            Owner = _masterWidget,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _controlPanel.Closed += OnControlPanelClosed;
        _controlPanel.Show();
    }

    private void OnControlPanelClosed(object? sender, EventArgs e)
    {
        if (_controlPanel is not null)
        {
            _controlPanel.Closed -= OnControlPanelClosed;
            _controlPanel = null;
        }
    }

    private async Task ShutdownAsync(int exitCode = 0)
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;

        if (_masterWidget is not null)
        {
            _masterWidget.Closed -= OnMasterWidgetClosed;
            _masterWidget = null;
        }

        if (_controlPanel is not null)
        {
            _controlPanel.Closed -= OnControlPanelClosed;
            _controlPanel.Close();
            _controlPanel = null;
        }

        if (_viewModel is not null)
        {
            _viewModel.ControlPanelRequested -= OnControlPanelRequested;
            await _viewModel.DisposeAsync();
            _viewModel = null;
        }

        Shutdown(exitCode);
    }
}

