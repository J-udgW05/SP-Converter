using FluentAssertions;
using SPConverter.Models;
using Xunit;

namespace SPConverter.Tests.Models;

public class ImageFormatRulesTests
{
    [Theory]
    [InlineData("image.png")]
    [InlineData("animation.gif")]
    [InlineData("icon.ico")]
    [InlineData("document.pdf")]
    [InlineData("raw.cr3")]
    [InlineData("photo.heif")]
    [InlineData("texture.dds")]
    [InlineData("linear.exr")]
    public void IsSupportedInputFile_ShouldRecognizeSupportedImages(string fileName)
    {
        ImageFormatRules.IsSupportedInputFile(fileName).Should().BeTrue();
    }

    [Theory]
    [InlineData("clip.mp4")]
    [InlineData("movie.mkv")]
    [InlineData("recording.webm")]
    public void IsVideoFile_ShouldRecognizeUnsupportedVideoContainers(string fileName)
    {
        ImageFormatRules.IsVideoFile(fileName).Should().BeTrue();
        ImageFormatRules.IsSupportedInputFile(fileName).Should().BeFalse();
    }

    [Theory]
    [InlineData("animation.gif")]
    [InlineData("animated.webp")]
    [InlineData("multipage.tiff")]
    [InlineData("icon.ico")]
    [InlineData("document.pdf")]
    public void MayContainMultipleImages_ShouldRecognizeFrameAndPageContainers(string fileName)
    {
        ImageFormatRules.MayContainMultipleImages(fileName).Should().BeTrue();
    }

    [Theory]
    [InlineData("JPG")]
    [InlineData("JPEG")]
    [InlineData("BMP")]
    public void TargetFormatReplacesAlphaWithWhite_ShouldRecognizeFlatteningTargets(string targetFormat)
    {
        ImageFormatRules.TargetFormatReplacesAlphaWithWhite(targetFormat).Should().BeTrue();
    }

    [Theory]
    [InlineData("JPG", true)]
    [InlineData("WEBP", true)]
    [InlineData("AVIF", true)]
    [InlineData("JXL", true)]
    [InlineData("HEIC", false)]
    [InlineData("HEIF", false)]
    [InlineData("PNG", false)]
    [InlineData("PDF", false)]
    public void TargetFormatUsesQuality_ShouldOnlyRecognizeCompressionQualityTargets(string targetFormat, bool expected)
    {
        ImageFormatRules.TargetFormatUsesQuality(targetFormat).Should().Be(expected);
    }

    [Theory]
    [InlineData("JPG", true)]
    [InlineData("AVIF", true)]
    [InlineData("JXL", true)]
    [InlineData("HEIC", false)]
    [InlineData("HEIF", false)]
    public void IsSupportedTargetFormat_ShouldOnlyRecognizeWritableTargets(string targetFormat, bool expected)
    {
        ImageFormatRules.IsSupportedTargetFormat(targetFormat).Should().Be(expected);
    }
}
