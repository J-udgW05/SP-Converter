using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SPConverter.Contracts;
using SPConverter.Models;
using SPConverter.ViewModels;
using Wpf.Ui;
using Xunit;

namespace SPConverter.Tests.ViewModels;

public class SingleConvertViewModelTests
{
    private readonly Mock<IImageConverterService> _converterServiceMock;
    private readonly Mock<ISnackbarService> _snackbarServiceMock;
    
    public SingleConvertViewModelTests()
    {
        _converterServiceMock = new Mock<IImageConverterService>();
        _snackbarServiceMock = new Mock<ISnackbarService>();
    }

    [Fact]
    public void ClearFileCommand_ShouldResetState()
    {
        var sut = new SingleConvertViewModel(_converterServiceMock.Object, _snackbarServiceMock.Object)
        {
            SelectedFile = "C:\\test\\image.jpg"
        };

        sut.ClearFileCommand.Execute(null);

        sut.SelectedFile.Should().BeNullOrEmpty();
        sut.HasSelectedFile.Should().BeFalse();
    }

    [Fact]
    public async Task ConvertAsync_ShouldCallConverterService()
    {
        var sut = new SingleConvertViewModel(_converterServiceMock.Object, _snackbarServiceMock.Object)
        {
            SelectedFile = "C:\\test\\image.png",
            TargetFormat = "JXL",
            Quality = 95
        };

        await sut.ConvertCommand.ExecuteAsync(null);

        _converterServiceMock.Verify(x => x.ConvertFileAsync(
            "C:\\test\\image.png",
            "C:\\test",
            It.Is<ConversionOptions>(o => o.TargetFormat == "JXL" && o.Quality == 95),
            It.IsAny<CancellationToken>()
        ), Times.Once);
        
        sut.IsConverting.Should().BeFalse();
    }

    [Fact]
    public void ConvertCommand_ShouldBeDisabled_ForVideoFile()
    {
        var sut = new SingleConvertViewModel(_converterServiceMock.Object, _snackbarServiceMock.Object)
        {
            SelectedFile = "C:\\test\\clip.mp4"
        };

        sut.ConvertCommand.CanExecute(null).Should().BeFalse();
        sut.ConversionNoticeText.Should().Contain("Video");
    }

    [Fact]
    public void ConversionNoticeText_ShouldWarn_WhenSingleFileMayContainMultipleImages()
    {
        var sut = new SingleConvertViewModel(_converterServiceMock.Object, _snackbarServiceMock.Object)
        {
            SelectedFile = "C:\\test\\animation.gif",
            TargetFormat = "PNG",
            ExtractAllPages = false
        };

        sut.ConversionNoticeText.Should().Contain("Only the first image");
        sut.HasConversionNotice.Should().BeTrue();
    }

    [Fact]
    public void ConversionNoticeText_ShouldHideMultiImageWarning_WhenExtractAllPagesIsEnabled()
    {
        var sut = new SingleConvertViewModel(_converterServiceMock.Object, _snackbarServiceMock.Object)
        {
            SelectedFile = "C:\\test\\animation.gif",
            TargetFormat = "PNG",
            ExtractAllPages = true
        };

        sut.ConversionNoticeText.Should().NotContain("Only the first image");
    }

    [Fact]
    public void ConversionNoticeText_ShouldWarn_WhenAlphaMayBeFlattened()
    {
        var sut = new SingleConvertViewModel(_converterServiceMock.Object, _snackbarServiceMock.Object)
        {
            SelectedFile = "C:\\test\\transparent.png",
            TargetFormat = "JPEG"
        };

        sut.ConversionNoticeText.Should().Contain("white background");
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(0, 1)]
    public void Quality_ShouldClampToSupportedRange(int selectedQuality, int expectedQuality)
    {
        var sut = new SingleConvertViewModel(_converterServiceMock.Object, _snackbarServiceMock.Object);

        sut.Quality = selectedQuality;

        sut.Quality.Should().Be(expectedQuality);
    }
}
