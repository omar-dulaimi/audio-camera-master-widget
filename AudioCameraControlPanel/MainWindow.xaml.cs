using System.Windows;
using AudioCameraControlPanel.Models;
using AudioCameraControlPanel.Services;
using AudioCameraControlPanel.ViewModels;

namespace AudioCameraControlPanel;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Dictionary<CompactWidgetKind, CompactWidgetWindow> _compactWidgets = new();
    private readonly bool _ownsViewModel;
    private bool _isClosing;

    public MainWindow()
        : this(
            new MainViewModel(
                new AudioDeviceService(),
                new CameraDeviceService(),
                new SettingsLauncherService(),
                new JsonAppSettingsStore(),
                new WindowsStartupRegistrationService()),
            ownsViewModel: true)
    {
    }

    public MainWindow(MainViewModel viewModel)
        : this(viewModel, ownsViewModel: false)
    {
    }

    private MainWindow(MainViewModel viewModel, bool ownsViewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _ownsViewModel = ownsViewModel;
        DataContext = _viewModel;
        _viewModel.CompactWidgetRequested += OnCompactWidgetRequested;
        if (_ownsViewModel)
        {
            Loaded += OnLoaded;
        }

        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _viewModel.CompactWidgetRequested -= OnCompactWidgetRequested;
        CloseCompactWidgets();

        if (!_ownsViewModel)
        {
            return;
        }

        e.Cancel = true;
        await _viewModel.DisposeAsync();

        _ = Dispatcher.BeginInvoke(() =>
        {
            Closing -= OnClosing;
            Close();
        });
    }

    private void OnCompactWidgetRequested(object? sender, CompactWidgetKind kind)
    {
        if (_compactWidgets.TryGetValue(kind, out var existingWidget) && existingWidget.IsVisible)
        {
            if (existingWidget.WindowState == WindowState.Minimized)
            {
                existingWidget.WindowState = WindowState.Normal;
            }

            existingWidget.Activate();
            return;
        }

        var widget = new CompactWidgetWindow(_viewModel, kind)
        {
            Owner = this
        };
        widget.Closed += (_, _) => _compactWidgets.Remove(kind);
        _compactWidgets[kind] = widget;
        widget.Show();
    }

    private void CloseCompactWidgets()
    {
        foreach (var widget in _compactWidgets.Values.ToList())
        {
            widget.Close();
        }

        _compactWidgets.Clear();
    }
}
