using FluentAssertions;
using SPConverter.ViewModels;
using Xunit;
using System.IO;
using System;

namespace SPConverter.Tests.ViewModels;

public class SettingsViewModelTests : IDisposable
{
    private string _originalSettingsPath;

    public SettingsViewModelTests()
    {
        _originalSettingsPath = SettingsViewModel.SettingsPath;
        SettingsViewModel.SettingsPath = Path.Combine(Path.GetTempPath(), "SPConverterTests_Settings", "test_settings.json");
    }

    public void Dispose()
    {
        if (File.Exists(SettingsViewModel.SettingsPath))
        {
            File.Delete(SettingsViewModel.SettingsPath);
        }
        SettingsViewModel.SettingsPath = _originalSettingsPath;
    }

    [Fact]
    public void Constructor_ShouldSetDefaultValues_IfNoSettingsFileExists()
    {
        if (File.Exists(SettingsViewModel.SettingsPath))
            File.Delete(SettingsViewModel.SettingsPath);

        var sut = new SettingsViewModel();

        sut.CurrentTheme.Should().Be("System");
        sut.CurrentLanguageIndex.Should().BeInRange(0, 1);
        sut.CurrentLanguageName.Should().Be(sut.CurrentLanguageIndex == 1 ? "English" : "Русский");
        sut.DeleteOriginalsByDefault.Should().BeFalse();
        sut.ExtractAllPagesByDefault.Should().BeFalse();
        sut.AlwaysIncludeSubfolders.Should().BeFalse();
        sut.PreserveFolderStructure.Should().BeFalse();
    }

    [Fact]
    public void Properties_ShouldTriggerSave_WhenChanged()
    {
        if (File.Exists(SettingsViewModel.SettingsPath))
            File.Delete(SettingsViewModel.SettingsPath);
        
        var sut = new SettingsViewModel();
        
        sut.CurrentTheme = "Dark";
        
        sut.CurrentLanguageIndex = 1;
        sut.PreserveFolderStructure = true;

        File.Exists(SettingsViewModel.SettingsPath).Should().BeTrue();
        var json = File.ReadAllText(SettingsViewModel.SettingsPath);
        json.Should().Contain("\"CurrentLanguageIndex\":1");
        json.Should().Contain("\"PreserveFolderStructure\":true");
        json.Should().NotContain("ShowSupportButtonInHeader");
    }

    [Fact]
    public void Constructor_ShouldNormalizeInvalidSavedValues()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsViewModel.SettingsPath)!);
        File.WriteAllText(
            SettingsViewModel.SettingsPath,
            "{\"CurrentTheme\":\"Solarized\",\"CurrentLanguageIndex\":99,\"ShowSupportButtonInHeader\":true}");

        var sut = new SettingsViewModel();

        sut.CurrentTheme.Should().Be("System");
        sut.CurrentLanguageIndex.Should().Be(0);
        sut.CurrentLanguageName.Should().Be("Русский");
    }

    [Fact]
    public void Constructor_ShouldMigrateRemovedBlackAndWhiteThemeToSystem()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsViewModel.SettingsPath)!);
        File.WriteAllText(SettingsViewModel.SettingsPath, "{\"CurrentTheme\":\"BW\"}");

        var sut = new SettingsViewModel();

        sut.CurrentTheme.Should().Be("System");
    }

    [Fact]
    public void PreserveFolderStructure_ShouldBeRestoredFromSavedSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsViewModel.SettingsPath)!);
        File.WriteAllText(SettingsViewModel.SettingsPath, "{\"PreserveFolderStructure\":true}");

        var sut = new SettingsViewModel();

        sut.PreserveFolderStructure.Should().BeTrue();
    }

    [Fact]
    public void SaveWindowPlacement_ShouldPersistAndRestoreWindowBounds()
    {
        if (File.Exists(SettingsViewModel.SettingsPath))
            File.Delete(SettingsViewModel.SettingsPath);

        var sut = new SettingsViewModel();
        sut.SaveWindowPlacement(new SettingsViewModel.WindowPlacementData
        {
            Left = 100, Top = 50, Width = 700, Height = 600, IsMaximized = true
        });

        var restored = new SettingsViewModel().WindowPlacement;

        restored.Should().NotBeNull();
        restored!.Width.Should().Be(700);
        restored.Height.Should().Be(600);
        restored.IsMaximized.Should().BeTrue();
    }
}
