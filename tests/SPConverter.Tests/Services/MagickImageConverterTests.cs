using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ImageMagick;
using Moq;
using SPConverter.Contracts;
using SPConverter.Models;
using SPConverter.Services;
using Xunit;

namespace SPConverter.Tests.Services;

public class MagickImageConverterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IFileManagementService> _fileServiceMock;

    public MagickImageConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SPConverterTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        
        _fileServiceMock = new Mock<IFileManagementService>();
        
        _fileServiceMock.Setup(f => f.ReserveUniqueFilePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string src, string dir, string ext) => Path.Combine(dir, Path.GetFileNameWithoutExtension(src) + ext));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string CreateTestImage(string format = "png", bool hasAlpha = false, int width = 10, int height = 10)
    {
        var filePath = Path.Combine(_tempDir, $"test_image.{format}");
        using var image = new MagickImage(MagickColors.Red, (uint)width, (uint)height);
        
        if (hasAlpha)
        {
            image.Alpha(AlphaOption.Set);
            image.BackgroundColor = MagickColors.Transparent;
        }

        image.Format = format.ToUpperInvariant() switch {
            "PNG" => MagickFormat.Png,
            "JPG" => MagickFormat.Jpeg,
            "JPEG" => MagickFormat.Jpeg,
            "WEBP" => MagickFormat.WebP,
            "PSD" => MagickFormat.Psd,
            _ => MagickFormat.Png
        };
        
        image.Write(filePath);
        return filePath;
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldConvertPsdToAvifAtMaximumQuality()
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("psd", width: 640, height: 480);
        var options = new ConversionOptions
        {
            TargetFormat = "AVIF",
            Quality = 100,
            DeleteOriginalFiles = false
        };

        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        var expectedFile = Path.Combine(_tempDir, "test_image.avif");
        File.Exists(expectedFile).Should().BeTrue();
        using var outputImage = new MagickImage(expectedFile);
        outputImage.Format.Should().Be(MagickFormat.Avif);
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldCreateMultiSizeIcoFromLargeImage()
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png", width: 640, height: 480);
        var options = new ConversionOptions
        {
            TargetFormat = "ICO",
            Quality = 100,
            DeleteOriginalFiles = false
        };

        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        var expectedFile = Path.Combine(_tempDir, "test_image.ico");
        File.Exists(expectedFile).Should().BeTrue();
        using var icons = new MagickImageCollection(expectedFile);
        icons.Count.Should().BeGreaterThan(1);
        icons.Should().Contain(icon => icon.Width == 256 && icon.Height == 256);
        icons.Should().Contain(icon => icon.Width == 16 && icon.Height == 16);
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldConvertPngToJpg()
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png");
        
        var options = new ConversionOptions
        {
            TargetFormat = "JPG",
            Quality = 90,
            DeleteOriginalFiles = false
        };

        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        var expectedFile = Path.Combine(_tempDir, "test_image.jpg");
        File.Exists(expectedFile).Should().BeTrue();
        
        using var outputImage = new MagickImage(expectedFile);
        outputImage.Format.Should().Be(MagickFormat.Jpeg);
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldRemoveAlphaChannel_WhenConvertingToJpg()
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png", hasAlpha: true);
        
        var options = new ConversionOptions
        {
            TargetFormat = "JPG",
            Quality = 90,
            DeleteOriginalFiles = false
        };

        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        var expectedFile = Path.Combine(_tempDir, "test_image.jpg");
        using var outputImage = new MagickImage(expectedFile);
        outputImage.HasAlpha.Should().BeFalse();
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldDeleteOriginal_IfOptionIsSet()
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png");
        
        var options = new ConversionOptions
        {
            TargetFormat = "WEBP",
            Quality = 100,
            DeleteOriginalFiles = true
        };

        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        // Originals go to the Recycle Bin through the file service.
        _fileServiceMock.Verify(f => f.MoveToRecycleBin(sourceFile), Times.Once);
        File.Exists(Path.Combine(_tempDir, "test_image.webp")).Should().BeTrue();
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldKeepOriginal_WhenConversionFails()
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        string brokenFile = Path.Combine(_tempDir, "broken.png");
        File.WriteAllText(brokenFile, "not an image");
        var options = new ConversionOptions { TargetFormat = "JPG", DeleteOriginalFiles = true };

        Func<Task> convert = () => sut.ConvertFileAsync(brokenFile, _tempDir, options, CancellationToken.None);

        await convert.Should().ThrowAsync<Exception>();
        _fileServiceMock.Verify(f => f.MoveToRecycleBin(It.IsAny<string>()), Times.Never);
        File.Exists(brokenFile).Should().BeTrue();
    }
    [Theory]
    [InlineData("JXL", MagickFormat.Jxl)]
    [InlineData("AVIF", MagickFormat.Avif)]
    [InlineData("BMP", MagickFormat.Bmp)]
    [InlineData("TGA", MagickFormat.Tga)]
    [InlineData("TIFF", MagickFormat.Tiff)]
    [InlineData("PDF", MagickFormat.Pdf)]
    [InlineData("ICO", MagickFormat.Ico)]
    [InlineData("GIF", MagickFormat.Gif)]
    public async Task ConvertFileAsync_ShouldSupportNewFormats(string targetFormat, MagickFormat expectedFormat)
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png");
        
        var options = new ConversionOptions
        {
            TargetFormat = targetFormat,
            Quality = 90,
            DeleteOriginalFiles = false
        };

        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        var expectedFile = Path.Combine(_tempDir, "test_image." + targetFormat.ToLowerInvariant());
        File.Exists(expectedFile).Should().BeTrue();
        
        if (targetFormat != "PDF")
        {
            using var outputImage = new MagickImage(expectedFile);
            outputImage.Format.Should().Be(expectedFormat);
        }
    }

    [Theory]
    [InlineData("HEIC")]
    [InlineData("HEIF")]
    public async Task ConvertFileAsync_ShouldRejectTargetsWithoutWriteEncoder(string targetFormat)
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png");
        var options = new ConversionOptions
        {
            TargetFormat = targetFormat,
            Quality = 90,
            DeleteOriginalFiles = false
        };

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None));

        exception.Message.Should().Contain("cannot be written");
        _fileServiceMock.Verify(
            service => service.ReserveUniqueFilePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldClampQuality()
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png");
        
        var options = new ConversionOptions
        {
            TargetFormat = "JPEG",
            Quality = 150,
            DeleteOriginalFiles = false
        };

        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        var expectedFile = Path.Combine(_tempDir, "test_image.jpeg");
        using var outputImage = new MagickImage(expectedFile);
        Assert.True(outputImage.Quality <= 100);
    }

    [Fact]
    public async Task ConvertFilesAsync_ShouldThrowBatchException_WhenAnyFileFails()
    {
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png");
        var missingFile = Path.Combine(_tempDir, "missing.png");
        var options = new ConversionOptions
        {
            TargetFormat = "JPG",
            Quality = 90
        };

        var exception = await Assert.ThrowsAsync<ConversionBatchException>(() =>
            sut.ConvertFilesAsync(
                new[] { sourceFile, missingFile },
                _tempDir,
                options,
                new Progress<ConversionProgress>(),
                CancellationToken.None));

        exception.Failures.Should().ContainSingle(failure => failure.FilePath == missingFile);
        File.Exists(Path.Combine(_tempDir, "test_image.jpg")).Should().BeTrue();
    }
}
