using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SPConverter.Contracts;
using SPConverter.Models;
using SPConverter.ViewModels;
using System.Linq;
using Xunit;
using System.IO;

namespace SPConverter.Tests.ViewModels;

public class MassConvertViewModelTests : IDisposable
{
    private readonly Mock<IImageConverterService> _converterServiceMock;
    private readonly Mock<IFileManagementService> _fileServiceMock;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly string _originalSettingsPath;

    public MassConvertViewModelTests()
    {
        _originalSettingsPath = SettingsViewModel.SettingsPath;
        SettingsViewModel.SettingsPath = Path.Combine(Path.GetTempPath(), "SPConverterTests_Settings", $"{Guid.NewGuid()}.json");
        _converterServiceMock = new Mock<IImageConverterService>();
        _fileServiceMock = new Mock<IFileManagementService>();
        _settingsViewModel = new SettingsViewModel();
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
    public void ClearSourceCommand_ShouldResetState()
    {
        var sut = CreateViewModel();
        sut.SourceDirectory = "C:\\test";
        sut.ProgressStatus = "Done";
        sut.ProgressMax = 10;
        sut.ProgressValue = 5;
        sut.FoundFiles.Add("C:\\test\\1.png");

        sut.ClearSourceCommand.Execute(null);

        sut.SourceDirectory.Should().BeEmpty();
        sut.FoundFiles.Should().BeEmpty();
        sut.ProgressStatus.Should().BeNull();
        sut.ProgressMax.Should().Be(0);
        sut.ProgressValue.Should().Be(0);
    }

    [Fact]
    public void SourceDirectoryChange_ShouldClearPreviousScanResults()
    {
        var sut = CreateViewModel();
        sut.SourceDirectory = "C:\\old";
        sut.ProgressStatus = "Files found: 1";
        sut.ProgressMax = 1;
        sut.ProgressValue = 1;
        sut.FoundFiles.Add("C:\\old\\image.png");

        sut.SourceDirectory = "C:\\new";

        sut.FoundFiles.Should().BeEmpty();
        sut.ProgressStatus.Should().BeNull();
        sut.ProgressMax.Should().Be(0);
        sut.ProgressValue.Should().Be(0);
    }

    [Fact]
    public async Task ScanDirectoryCommand_ShouldPopulateFoundFiles()
    {
        var sut = CreateViewModel();

        _fileServiceMock.Setup(x => x.ScanImagesInPath("C:\\test", true))
            .Returns(new ImageScanResult
            {
                SupportedFiles = new List<string> { "C:\\test\\1.png", "C:\\test\\sub\\2.jpg" },
                SourceExists = true
            });

        sut.SourceDirectory = "C:\\test";
        sut.IncludeSubfolders = true;
        await sut.ScanDirectoryCommand.ExecuteAsync(null);

        sut.FoundFiles.Should().HaveCount(2);
        sut.ProgressMax.Should().Be(2);
        sut.ProgressValue.Should().Be(0);
    }

    [Fact]
    public void ConversionNoticeText_ShouldWarn_WhenFoundFilesMayContainMultipleImages()
    {
        var sut = CreateViewModel();
        sut.TargetFormat = "PNG";
        sut.ExtractAllPagesForCurrentConversion = false;
        sut.FoundFiles.Add("C:\\test\\icon.ico");

        sut.ConversionNoticeText.Should().Contain("Only the first image");
        sut.HasConversionNotice.Should().BeTrue();
    }

    [Fact]
    public void ConversionNoticeText_ShouldWarn_WhenFoundFilesMayLoseAlpha()
    {
        var sut = CreateViewModel();
        sut.TargetFormat = "JPG";
        sut.FoundFiles.Add("C:\\test\\transparent.png");

        sut.ConversionNoticeText.Should().Contain("white background");
    }

    [Fact]
    public void ShowSubfoldersSuggestion_ShouldBeTrue_WhenFolderHasNestedFoldersAndSettingIsOff()
    {
        var sut = CreateViewModel();

        sut.SourceDirectory = "C:\\photos";
        sut.HasNestedFolders = true;
        sut.IncludeSubfolders = false;

        sut.ShowSubfoldersSuggestion.Should().BeTrue();
    }

    [Fact]
    public async Task ConvertCommand_ShouldUseSettingsForDestructiveOptions()
    {
        var sut = CreateViewModel();
        _settingsViewModel.DeleteOriginalsByDefault = true;
        _settingsViewModel.ExtractAllPagesByDefault = true;
        sut.SourceDirectory = "C:\\photos";
        // Conversion rescans the source first.
        _fileServiceMock.Setup(x => x.ScanImagesInPath("C:\\photos", It.IsAny<bool>()))
            .Returns(new ImageScanResult
            {
                SupportedFiles = new List<string> { "C:\\photos\\image.gif" },
                SourceExists = true
            });

        _converterServiceMock
            .Setup(x => x.ConvertFilesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.ConvertCommand.ExecuteAsync(null);

        _converterServiceMock.Verify(x => x.ConvertFilesAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string>(),
            It.Is<ConversionOptions>(options => options.DeleteOriginalFiles && options.ExtractAllPages),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(0, 1)]
    public void Quality_ShouldClampToSupportedRange(int selectedQuality, int expectedQuality)
    {
        var sut = CreateViewModel();

        sut.Quality = selectedQuality;

        sut.Quality.Should().Be(expectedQuality);
    }

    [Fact]
    public void SelectTargetFormatCommand_ShouldIgnoreUnsupportedWriteTarget()
    {
        var sut = CreateViewModel();
        sut.TargetFormat = "JPG";

        sut.SelectTargetFormatCommand.Execute("HEIC");

        sut.TargetFormat.Should().Be("JPG");
    }

    [Fact]
    public void SettingsChange_ShouldEnableSubfoldersForCurrentViewModel()
    {
        var sut = CreateViewModel();
        sut.IncludeSubfolders = false;

        _settingsViewModel.AlwaysIncludeSubfolders = true;

        sut.IncludeSubfolders.Should().BeTrue();
    }

    [Fact]
    public void SourceDirectory_ShouldStripQuotesFromCopiedPath()
    {
        var sut = CreateViewModel();

        sut.SourceDirectory = "\"C:\\photos\\summer\"";

        sut.SourceDirectory.Should().Be("C:\\photos\\summer");
    }

    [Fact]
    public async Task ConvertCommand_ShouldRescanSource_BeforeEveryConversion()
    {
        var sut = CreateViewModel();
        sut.SourceDirectory = "C:\\photos";
        sut.FoundFiles.Add("C:\\photos\\deleted-by-previous-run.png");
        _fileServiceMock.Setup(x => x.ScanImagesInPath("C:\\photos", It.IsAny<bool>()))
            .Returns(new ImageScanResult
            {
                SupportedFiles = new List<string> { "C:\\photos\\current.png" },
                SourceExists = true
            });
        _converterServiceMock
            .Setup(x => x.ConvertFilesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<ConversionOptions>(), It.IsAny<IProgress<ConversionProgress>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.ConvertCommand.ExecuteAsync(null);

        _converterServiceMock.Verify(x => x.ConvertFilesAsync(
            It.Is<IEnumerable<string>>(files => files.SequenceEqual(new[] { "C:\\photos\\current.png" })),
            It.IsAny<string>(),
            It.IsAny<ConversionOptions>(),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IsIdle_ShouldBeFalse_WhileConverting()
    {
        var sut = CreateViewModel();

        sut.IsConverting = true;

        sut.IsIdle.Should().BeFalse();
    }

    [Fact]
    public async Task TypingExistingPath_ShouldScanAfterTypingPauses()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SPConverterTyping_" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var scanned = new TaskCompletionSource();
            _fileServiceMock.Setup(x => x.ScanImagesInPath(folder, It.IsAny<bool>()))
                .Returns(() =>
                {
                    scanned.TrySetResult();
                    return new ImageScanResult { SourceExists = true };
                });
            var sut = CreateViewModel();

            sut.SourceDirectory = folder;

            Task finished = await Task.WhenAny(scanned.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            finished.Should().BeSameAs(scanned.Task, "a typed or pasted path should be scanned without pressing Enter");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private MassConvertViewModel CreateViewModel()
    {
        return new MassConvertViewModel(
            _converterServiceMock.Object,
            _fileServiceMock.Object,
            _settingsViewModel);
    }
}
