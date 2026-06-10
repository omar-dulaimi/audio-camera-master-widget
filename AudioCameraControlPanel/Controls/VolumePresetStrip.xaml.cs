using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AudioCameraControlPanel.Controls;

public partial class VolumePresetStrip : UserControl
{
    public static readonly DependencyProperty ZeroCommandProperty = RegisterCommand(nameof(ZeroCommand));
    public static readonly DependencyProperty ThirtyCommandProperty = RegisterCommand(nameof(ThirtyCommand));
    public static readonly DependencyProperty ThirtyFiveCommandProperty = RegisterCommand(nameof(ThirtyFiveCommand));
    public static readonly DependencyProperty FortyCommandProperty = RegisterCommand(nameof(FortyCommand));
    public static readonly DependencyProperty FortyFiveCommandProperty = RegisterCommand(nameof(FortyFiveCommand));
    public static readonly DependencyProperty SixtyCommandProperty = RegisterCommand(nameof(SixtyCommand));

    public VolumePresetStrip()
    {
        InitializeComponent();
    }

    public ICommand? ZeroCommand
    {
        get => (ICommand?)GetValue(ZeroCommandProperty);
        set => SetValue(ZeroCommandProperty, value);
    }

    public ICommand? ThirtyCommand
    {
        get => (ICommand?)GetValue(ThirtyCommandProperty);
        set => SetValue(ThirtyCommandProperty, value);
    }

    public ICommand? ThirtyFiveCommand
    {
        get => (ICommand?)GetValue(ThirtyFiveCommandProperty);
        set => SetValue(ThirtyFiveCommandProperty, value);
    }

    public ICommand? FortyCommand
    {
        get => (ICommand?)GetValue(FortyCommandProperty);
        set => SetValue(FortyCommandProperty, value);
    }

    public ICommand? FortyFiveCommand
    {
        get => (ICommand?)GetValue(FortyFiveCommandProperty);
        set => SetValue(FortyFiveCommandProperty, value);
    }

    public ICommand? SixtyCommand
    {
        get => (ICommand?)GetValue(SixtyCommandProperty);
        set => SetValue(SixtyCommandProperty, value);
    }

    private static DependencyProperty RegisterCommand(string propertyName)
    {
        return DependencyProperty.Register(
            propertyName,
            typeof(ICommand),
            typeof(VolumePresetStrip),
            new PropertyMetadata(null));
    }
}
