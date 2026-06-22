using System.IO;
using Microsoft.Win32;

namespace AudioCameraControlPanel.Services;

public sealed class WindowsStartupRegistrationService : IStartupRegistrationService
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValueName = "AudioCameraMasterWidget";

    private readonly IStartupRegistry _registry;
    private readonly string _executablePath;

    public WindowsStartupRegistrationService()
        : this(new CurrentUserStartupRegistry(), GetCurrentExecutablePath())
    {
    }

    public WindowsStartupRegistrationService(IStartupRegistry registry, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        _registry = registry;
        _executablePath = Path.GetFullPath(executablePath);
    }

    public bool IsEnabled()
    {
        var command = _registry.GetString(RunKeyPath, RunValueName);
        var registeredPath = ExtractExecutablePath(command);
        return registeredPath is not null
            && string.Equals(
                Path.GetFullPath(registeredPath),
                _executablePath,
                StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            _registry.SetString(RunKeyPath, RunValueName, QuoteExecutablePath(_executablePath));
            return;
        }

        _registry.DeleteValue(RunKeyPath, RunValueName);
    }

    private static string GetCurrentExecutablePath()
    {
        return Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to determine the current executable path.");
    }

    private static string QuoteExecutablePath(string executablePath)
    {
        return $"\"{executablePath}\"";
    }

    private static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var trimmed = command.Trim();
        if (trimmed[0] == '"')
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            return closingQuote > 1 ? trimmed[1..closingQuote] : null;
        }

        var firstSpace = trimmed.IndexOf(' ');
        return firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
    }

    private sealed class CurrentUserStartupRegistry : IStartupRegistry
    {
        public string? GetString(string keyPath, string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName) as string;
        }

        public void SetString(string keyPath, string valueName, string value)
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
            key.SetValue(valueName, value, RegistryValueKind.String);
        }

        public void DeleteValue(string keyPath, string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
