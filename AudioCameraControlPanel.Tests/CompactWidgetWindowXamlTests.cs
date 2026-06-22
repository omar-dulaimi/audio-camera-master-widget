using System.IO;
using System.Xml.Linq;

namespace AudioCameraControlPanel.Tests;

[TestClass]
public sealed class CompactWidgetWindowXamlTests
{
    [TestMethod]
    public void MasterWidgetSurfacesStartWithWindowsToggle()
    {
        var document = XDocument.Load(FindRepoFile("AudioCameraControlPanel/CompactWidgetWindow.xaml"));
        var masterGrid = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Grid"
                && GetAttribute(element, "Visibility")?.Contains("IsMaster", StringComparison.Ordinal) == true);

        var startupToggle = masterGrid
            .Descendants()
            .SingleOrDefault(element =>
                element.Name.LocalName == "CheckBox"
                && GetAttribute(element, "IsChecked")?.Contains("Main.StartWithWindows", StringComparison.Ordinal) == true);

        Assert.IsNotNull(startupToggle);
    }

    private static string? GetAttribute(XElement element, string localName)
    {
        return element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Unable to find {relativePath} from {AppContext.BaseDirectory}.");
    }
}
