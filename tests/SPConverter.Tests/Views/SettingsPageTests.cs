using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace SPConverter.Tests.Views;

public class SettingsPageTests
{
    [Fact]
    public void SettingsPage_ShouldUseCompactLanguageMenu()
    {
        XElement root = LoadSettingsPageRoot();
        string xaml = root.ToString(SaveOptions.DisableFormatting);

        xaml.Should().Contain("LanguageButton");
        xaml.Should().Contain("LanguageSelectorButtonStyle");
        xaml.Should().Contain("Property=\"Height\" Value=\"36\"");
        xaml.Should().Contain("Width=\"112\"");
        xaml.Should().Contain("OnLanguageMenuItemClick");
        xaml.Should().Contain("PreviewMouseWheel=\"OnPageScrollViewerPreviewMouseWheel\"");
        xaml.Should().NotContain("Style=\"{StaticResource SecondaryButtonStyle}\" Click=\"OnLanguageButtonClick\"");
        xaml.Should().NotContain("<ComboBox ");
    }

    private static XElement LoadSettingsPageRoot()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string xamlPath = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "Views", "Pages", "SettingsPage.xaml"));

        Assert.True(File.Exists(xamlPath), $"Could not find SettingsPage.xaml at {xamlPath}");

        return XDocument.Load(xamlPath).Root ?? throw new InvalidOperationException("SettingsPage.xaml root element is missing");
    }
}
