using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace SPConverter.Tests.Views;

public class SingleConvertPageTests
{
    [Fact]
    public void SingleConvertPage_ShouldNotExposeHeicAsOutputTarget()
    {
        XElement root = LoadSingleConvertPageRoot();
        string xaml = root.ToString(SaveOptions.DisableFormatting);

        xaml.Should().NotContain("ConverterParameter=HEIC");
        xaml.Should().Contain("PreviewMouseWheel=\"OnPageScrollViewerPreviewMouseWheel\"");
    }

    private static XElement LoadSingleConvertPageRoot()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string xamlPath = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "Views", "Pages", "SingleConvertPage.xaml"));

        Assert.True(File.Exists(xamlPath), $"Could not find SingleConvertPage.xaml at {xamlPath}");

        return XDocument.Load(xamlPath).Root ?? throw new InvalidOperationException("SingleConvertPage.xaml root element is missing");
    }
}
