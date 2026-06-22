using AudioCameraControlPanel.Services;

namespace AudioCameraControlPanel.Tests;

[TestClass]
public sealed class WindowsStartupRegistrationServiceTests
{
    [TestMethod]
    public void SetEnabledWritesQuotedExecutablePathToRunKey()
    {
        var registry = new InMemoryStartupRegistry();
        var service = new WindowsStartupRegistrationService(
            registry,
            @"C:\Users\User\AppData\Local\Programs\AudioCameraMasterWidget\AudioCameraMasterWidget.exe");

        service.SetEnabled(true);

        Assert.AreEqual(
            @"""C:\Users\User\AppData\Local\Programs\AudioCameraMasterWidget\AudioCameraMasterWidget.exe""",
            registry.GetString(
                WindowsStartupRegistrationService.RunKeyPath,
                WindowsStartupRegistrationService.RunValueName));
    }

    [TestMethod]
    public void IsEnabledMatchesQuotedCurrentExecutablePath()
    {
        var registry = new InMemoryStartupRegistry();
        registry.SetString(
            WindowsStartupRegistrationService.RunKeyPath,
            WindowsStartupRegistrationService.RunValueName,
            @"""C:\Users\User\AppData\Local\Programs\AudioCameraMasterWidget\AudioCameraMasterWidget.exe""");
        var service = new WindowsStartupRegistrationService(
            registry,
            @"C:\Users\User\AppData\Local\Programs\AudioCameraMasterWidget\AudioCameraMasterWidget.exe");

        Assert.IsTrue(service.IsEnabled());
    }

    [TestMethod]
    public void IsEnabledIgnoresDifferentExecutablePath()
    {
        var registry = new InMemoryStartupRegistry();
        registry.SetString(
            WindowsStartupRegistrationService.RunKeyPath,
            WindowsStartupRegistrationService.RunValueName,
            @"""C:\Other\App.exe""");
        var service = new WindowsStartupRegistrationService(
            registry,
            @"C:\Users\User\AppData\Local\Programs\AudioCameraMasterWidget\AudioCameraMasterWidget.exe");

        Assert.IsFalse(service.IsEnabled());
    }

    [TestMethod]
    public void SetEnabledFalseRemovesRunValue()
    {
        var registry = new InMemoryStartupRegistry();
        var service = new WindowsStartupRegistrationService(
            registry,
            @"C:\Users\User\AppData\Local\Programs\AudioCameraMasterWidget\AudioCameraMasterWidget.exe");
        service.SetEnabled(true);

        service.SetEnabled(false);

        Assert.IsNull(registry.GetString(
            WindowsStartupRegistrationService.RunKeyPath,
            WindowsStartupRegistrationService.RunValueName));
    }

    private sealed class InMemoryStartupRegistry : IStartupRegistry
    {
        private readonly Dictionary<(string KeyPath, string ValueName), string> _values = new();

        public string? GetString(string keyPath, string valueName)
        {
            return _values.TryGetValue((keyPath, valueName), out var value) ? value : null;
        }

        public void SetString(string keyPath, string valueName, string value)
        {
            _values[(keyPath, valueName)] = value;
        }

        public void DeleteValue(string keyPath, string valueName)
        {
            _values.Remove((keyPath, valueName));
        }
    }
}
