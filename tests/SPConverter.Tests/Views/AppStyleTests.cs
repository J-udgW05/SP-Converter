using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace SPConverter.Tests.Views;

public class AppStyleTests
{
    [Fact]
    public void ComboBoxItemStyle_ShouldUseThemeBrushes()
    {
        XElement appResources = LoadApplicationResources();

        XElement? comboBoxItemStyle = appResources
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Style"
                && element.Attribute("TargetType")?.Value == "ComboBoxItem");

        comboBoxItemStyle.Should().NotBeNull("language dropdown items must follow the selected app theme");

        string styleMarkup = comboBoxItemStyle!.ToString(SaveOptions.DisableFormatting);

        styleMarkup.Should().Contain("AppPopupBrush");
        styleMarkup.Should().Contain("AppTextBrush");
        styleMarkup.Should().Contain("AppToggleInactiveHoverBrush");
        styleMarkup.Should().Contain("AppAccentBrush");
        styleMarkup.Should().Contain("AppAccentForegroundBrush");
    }

    [Fact]
    public void ContextMenuStyle_ShouldUseOpaquePopupBrushes()
    {
        XElement appResources = LoadApplicationResources();

        XElement? contextMenuStyle = appResources
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Style"
                && element.Attribute("TargetType")?.Value == "ContextMenu");

        contextMenuStyle.Should().NotBeNull("format dropdown menu must not use transparent card brushes");

        string styleMarkup = contextMenuStyle!.ToString(SaveOptions.DisableFormatting);

        styleMarkup.Should().Contain("AppPopupBrush");
        styleMarkup.Should().Contain("AppPopupBorderBrush");
        styleMarkup.Should().Contain("OverridesDefaultStyle");
        styleMarkup.Should().Contain("ItemsPresenter");
        styleMarkup.Should().Contain("CornerRadius=\"6\"");
    }

    [Fact]
    public void PopupMenuItems_ShouldSuppressDefaultFocusAndSeparatorArtifacts()
    {
        XElement appResources = LoadApplicationResources();

        XElement? menuItemStyle = appResources
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Style"
                && element.Attribute("TargetType")?.Value == "MenuItem");

        XElement? separatorStyle = appResources
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Style"
                && element.Attribute("TargetType")?.Value == "Separator");

        menuItemStyle.Should().NotBeNull("menu items should not show default WPF focus rectangles");
        separatorStyle.Should().NotBeNull("format menu separators should follow the active theme");

        string menuItemMarkup = menuItemStyle!.ToString(SaveOptions.DisableFormatting);
        string separatorMarkup = separatorStyle!.ToString(SaveOptions.DisableFormatting);

        menuItemMarkup.Should().Contain("FocusVisualStyle");
        separatorMarkup.Should().Contain("AppPopupBorderBrush");
    }

    [Fact]
    public void PopupMenuItems_ShouldShowCheckedSelectionClearly()
    {
        XElement appResources = LoadApplicationResources();

        XElement? menuItemStyle = appResources
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Style"
                && element.Attribute("TargetType")?.Value == "MenuItem");

        menuItemStyle.Should().NotBeNull("the current format in the Other menu must be visually obvious");

        string menuItemMarkup = menuItemStyle!.ToString(SaveOptions.DisableFormatting);

        menuItemMarkup.Should().Contain("checkGlyph");
        menuItemMarkup.Should().Contain("Text=\"✓\"");
        menuItemMarkup.Should().Contain("Property=\"IsChecked\" Value=\"True\"");
        menuItemMarkup.Should().Contain("AppAccentBrush");
        menuItemMarkup.Should().Contain("AppAccentForegroundBrush");
    }

    private static XElement LoadApplicationResources()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string appXamlPath = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "App.xaml"));

        Assert.True(File.Exists(appXamlPath), $"Could not find App.xaml at {appXamlPath}");

        return XDocument.Load(appXamlPath).Root ?? throw new InvalidOperationException("App.xaml root element is missing");
    }
}
