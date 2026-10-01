using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace SPConverter.Tests.Views;

public class MainWindowTests
{
    [Fact]
    public void MainWindow_ShouldStayCompact_ForLaptopScreens()
    {
        XElement root = LoadMainWindowRoot();

        var heightAttr = root.Attribute("Height")?.Value;
        var minHeightAttr = root.Attribute("MinHeight")?.Value;

        double height = double.Parse(heightAttr ?? "0");
        double minHeight = double.Parse(minHeightAttr ?? "0");

        height.Should().BeLessThanOrEqualTo(680, "unified UI should fit better on laptop screens");
        minHeight.Should().BeLessThanOrEqualTo(540, "minimum height should not force the app off small screens");
    }

    [Fact]
    public void MainWindow_ShouldUseManualStartupLocation_ForCustomCentering()
    {
        XElement root = LoadMainWindowRoot();

        string startupLocation = root.Attribute("WindowStartupLocation")?.Value ?? string.Empty;

        startupLocation.Should().Be("Manual", "startup position is calculated from the screen work area and window size");
    }

    [Fact]
    public void MainWindow_ShouldRemoveModeTabs()
    {
        XElement root = LoadMainWindowRoot();
        string xaml = root.ToString(SaveOptions.DisableFormatting);

        xaml.Should().NotContain("Nav_MassConvert");
        xaml.Should().NotContain("Nav_SingleConvert");
        xaml.Should().NotContain("Support_Project");
        xaml.Should().Contain("Nav_Help");
        xaml.Should().Contain("Nav_Settings");
    }

    [Fact]
    public void MainWindow_ShouldNotShowIconInTitleBar()
    {
        XElement root = LoadMainWindowRoot();
        string xaml = root.ToString(SaveOptions.DisableFormatting);

        xaml.Should().NotContain("TitleBar.Icon");
        xaml.Should().Contain("Icon=\"pack://application:,,,/Assets/Icon.ico\"", "the taskbar still needs the window icon");
    }

    private static XElement LoadMainWindowRoot()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string xamlPath = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "Views", "MainWindow.xaml"));

        Assert.True(File.Exists(xamlPath), $"Could not find MainWindow.xaml at {xamlPath}");

        return XDocument.Load(xamlPath).Root ?? throw new InvalidOperationException("MainWindow.xaml root element is missing");
    }
}
