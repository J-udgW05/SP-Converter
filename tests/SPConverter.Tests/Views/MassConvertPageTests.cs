using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace SPConverter.Tests.Views;

public class MassConvertPageTests
{
    [Fact]
    public void MassConvertPage_ShouldOnlyShowWritablePopularTargetFormats()
    {
        XElement root = LoadMassConvertPageRoot();
        string xaml = root.ToString(SaveOptions.DisableFormatting);

        xaml.Should().Contain("ConverterParameter=AVIF");
        xaml.Should().Contain("ConverterParameter=JXL");
        xaml.Should().NotContain("ConverterParameter=HEIC");
        xaml.Should().NotContain("ConverterParameter=HEIF");
    }

    [Fact]
    public void MassConvertPage_ShouldStyleOtherFormatHoverAsSelectedChip()
    {
        XElement root = LoadMassConvertPageRoot();
        string xaml = root.ToString(SaveOptions.DisableFormatting);

        xaml.Should().Contain("OtherFormatButtonStyle");
        xaml.Should().Contain("AppAccentHoverBrush");
        xaml.Should().Contain("MultiDataTrigger");
    }

    [Fact]
    public void MassConvertPage_ShouldBindOtherFormatsToCurrentSelection()
    {
        XElement root = LoadMassConvertPageRoot();
        string xaml = root.ToString(SaveOptions.DisableFormatting);

        xaml.Should().Contain("DataContext=\"{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}\"");
        xaml.Should().Contain("IsCheckable=\"True\"");
        xaml.Should().Contain("IsChecked=\"{Binding TargetFormat, Converter={StaticResource StringMatchConverter}, ConverterParameter=JPEG}\"");
        xaml.Should().Contain("IsChecked=\"{Binding TargetFormat, Converter={StaticResource StringMatchConverter}, ConverterParameter=PBM}\"");
    }

    [Fact]
    public void MassConvertPage_ShouldKeepCustomQualityInputInlineAndCompact()
    {
        XElement root = LoadMassConvertPageRoot();
        string xaml = root.ToString(SaveOptions.DisableFormatting);

        xaml.Should().Contain("QualityChipStyle");
        xaml.Should().Contain("Width=\"44\"");
    }

    [Fact]
    public void MassConvertPage_ShouldUseFixedStepMouseWheelScrolling()
    {
        XElement root = LoadMassConvertPageRoot();
        string xaml = root.ToString(SaveOptions.DisableFormatting);

        xaml.Should().Contain("PreviewMouseWheel=\"OnPageScrollViewerPreviewMouseWheel\"");
    }

    private static XElement LoadMassConvertPageRoot()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string xamlPath = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "Views", "Pages", "MassConvertPage.xaml"));

        Assert.True(File.Exists(xamlPath), $"Could not find MassConvertPage.xaml at {xamlPath}");

        return XDocument.Load(xamlPath).Root ?? throw new InvalidOperationException("MassConvertPage.xaml root element is missing");
    }
}
