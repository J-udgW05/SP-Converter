using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ImageMagick;
using SPConverter.Models;
using SPConverter.Services;
using SPConverter.ViewModels;
using Xunit;

namespace SPConverter.Tests.Services;

/// <summary>Regression tests for the conversion audit fixes.</summary>
public class ConversionAuditTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _outputDir;
    private readonly MagickImageConverter _sut;

    public ConversionAuditTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SPConverterAudit_" + Guid.NewGuid());
        _outputDir = Path.Combine(_tempDir, "out");
        Directory.CreateDirectory(_tempDir);
        _sut = new MagickImageConverter(new LocalFileService());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task Tga_ShouldBeReadByExtension()
    {
        string source = Path.Combine(_tempDir, "texture.tga");
        using (var image = new MagickImage(MagickColors.Green, 24, 16))
        {
            image.Write(source, MagickFormat.Tga);
        }

        await _sut.ConvertFileAsync(source, _outputDir, new ConversionOptions { TargetFormat = "PNG" }, CancellationToken.None);

        using var result = new MagickImage(Path.Combine(_outputDir, "texture.png"));
        result.Width.Should().Be(24);
        result.Height.Should().Be(16);
    }

    [Fact]
    public async Task OptimizedGif_ShouldExtractFullSizeFrames()
    {
        string source = Path.Combine(_tempDir, "anim.gif");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Blue, 40, 30));
            var second = new MagickImage(MagickColors.Blue, 40, 30);
            second.Draw(new ImageMagick.Drawing.Drawables().FillColor(MagickColors.Yellow).Rectangle(10, 10, 14, 14));
            frames.Add(second);
            frames.Optimize();
            frames[1].Width.Should().BeLessThan(40, "the test needs a delta frame to be meaningful");
            frames.Write(source, MagickFormat.Gif);
        }

        var options = new ConversionOptions { TargetFormat = "PNG", ExtractAllPages = true };
        await _sut.ConvertFileAsync(source, _outputDir, options, CancellationToken.None);

        string[] outputs = Directory.GetFiles(_outputDir, "anim_page*.png").OrderBy(f => f).ToArray();
        outputs.Should().HaveCount(2);
        foreach (string output in outputs)
        {
            using var frame = new MagickImage(output);
            frame.Width.Should().Be(40);
            frame.Height.Should().Be(30);
        }
    }

    [Fact]
    public async Task IcoInput_ShouldUseLargestEntry()
    {
        string source = Path.Combine(_tempDir, "app.ico");
        using (var icon = new MagickImageCollection())
        {
            foreach (uint size in new uint[] { 16, 48, 128 })
            {
                icon.Add(new MagickImage(MagickColors.Purple, size, size));
            }
            icon.Write(source, MagickFormat.Ico);
        }

        await _sut.ConvertFileAsync(source, _outputDir, new ConversionOptions { TargetFormat = "PNG" }, CancellationToken.None);

        using var result = new MagickImage(Path.Combine(_outputDir, "app.png"));
        result.Width.Should().Be(128);
    }

    [Fact]
    public async Task IcoOutput_ShouldNotUpscaleSmallSources()
    {
        string source = Path.Combine(_tempDir, "small.png");
        using (var image = new MagickImage(MagickColors.Orange, 40, 40))
        {
            image.Write(source, MagickFormat.Png);
        }

        await _sut.ConvertFileAsync(source, _outputDir, new ConversionOptions { TargetFormat = "ICO" }, CancellationToken.None);

        using var icon = new MagickImageCollection(Path.Combine(_outputDir, "small.ico"));
        icon.Select(entry => entry.Width).Should().BeEquivalentTo(new uint[] { 32, 16 });
    }

    [Theory]
    [InlineData("PPM")]
    [InlineData("PGM")]
    [InlineData("BMP")]
    public async Task TransparentAreas_ShouldBecomeWhite_ForFormatsWithoutAlpha(string targetFormat)
    {
        string source = Path.Combine(_tempDir, "alpha.png");
        using (var image = new MagickImage(MagickColors.Transparent, 8, 8))
        {
            image.Write(source, MagickFormat.Png);
        }

        await _sut.ConvertFileAsync(source, _outputDir, new ConversionOptions { TargetFormat = targetFormat }, CancellationToken.None);

        string output = Directory.GetFiles(_outputDir).Single();
        using var result = new MagickImage(output);
        IPixel<ushort>? pixel = result.GetPixels().GetPixel(4, 4);
        pixel.Should().NotBeNull();
        pixel!.ToColor()!.ToString().Should().StartWith("#FFFFFFFFFFFF", "transparent pixels must be flattened onto white, not black");
    }

    [Fact]
    public async Task CmykJpeg_ShouldBeConvertedToSrgb_ForScreenFormats()
    {
        string source = CreateCmykJpeg("print.jpg");

        await _sut.ConvertFileAsync(source, _outputDir, new ConversionOptions { TargetFormat = "WEBP" }, CancellationToken.None);

        using var result = new MagickImage(Path.Combine(_outputDir, "print.webp"));
        result.ColorSpace.Should().Be(ColorSpace.sRGB);
    }

    [Fact]
    public async Task CmykJpeg_ShouldStayCmyk_WhenTargetSupportsIt()
    {
        string source = CreateCmykJpeg("print.jpg");

        await _sut.ConvertFileAsync(source, _outputDir, new ConversionOptions { TargetFormat = "TIFF" }, CancellationToken.None);

        using var result = new MagickImage(Path.Combine(_outputDir, "print.tiff"));
        result.ColorSpace.Should().Be(ColorSpace.CMYK);
    }

    [Fact]
    public async Task PreserveFolderStructure_ShouldMirrorSourceSubfolders()
    {
        string sourceRoot = Path.Combine(_tempDir, "photos");
        string first = CreatePng(Path.Combine(sourceRoot, "trip", "img.png"));
        string second = CreatePng(Path.Combine(sourceRoot, "work", "img.png"));
        string top = CreatePng(Path.Combine(sourceRoot, "top.png"));

        var options = new ConversionOptions { TargetFormat = "JPG", SourceRoot = sourceRoot, PreserveFolderStructure = true };
        await _sut.ConvertFilesAsync(new[] { first, second, top }, _outputDir, options, new Progress<ConversionProgress>(), CancellationToken.None);

        File.Exists(Path.Combine(_outputDir, "trip", "img.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "work", "img.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "top.jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task FlatOutput_ShouldKeepBothFilesWithSameName()
    {
        string sourceRoot = Path.Combine(_tempDir, "photos");
        string first = CreatePng(Path.Combine(sourceRoot, "trip", "img.png"));
        string second = CreatePng(Path.Combine(sourceRoot, "work", "img.png"));

        var options = new ConversionOptions { TargetFormat = "JPG", SourceRoot = sourceRoot, PreserveFolderStructure = false };
        await _sut.ConvertFilesAsync(new[] { first, second }, _outputDir, options, new Progress<ConversionProgress>(), CancellationToken.None);

        Directory.GetFiles(_outputDir).Select(Path.GetFileName).Should().BeEquivalentTo(new[] { "img.jpg", "img_1.jpg" });
    }

    [Fact]
    public async Task ReserveUniqueFilePath_ShouldThrowInsteadOfLooping_WhenFolderDoesNotExist()
    {
        var service = new LocalFileService();
        string missingFolder = Path.Combine(_tempDir, "missing");

        Task reserve = Task.Run(() => service.ReserveUniqueFilePath("a.png", missingFolder, ".jpg"));
        Task finished = await Task.WhenAny(reserve, Task.Delay(TimeSpan.FromSeconds(10)));

        finished.Should().BeSameAs(reserve, "a non-collision IO error must not be retried forever");
        reserve.Exception!.InnerException.Should().BeAssignableTo<IOException>();
    }

    [Fact]
    public void ExcludeFilesInOutputFolder_ShouldSkipPreviousResultsInsideSource()
    {
        string source = Path.Combine(_tempDir, "photos");
        Directory.CreateDirectory(source);
        string output = Path.Combine(source, "converted");
        string[] files =
        {
            Path.Combine(source, "a.png"),
            Path.Combine(source, "sub", "b.png"),
            Path.Combine(output, "a.jpg")
        };

        var result = MassConvertViewModel.ExcludeFilesInOutputFolder(files, source, output);

        result.Should().BeEquivalentTo(files.Take(2));
    }

    [Fact]
    public void ExcludeFilesInOutputFolder_ShouldKeepFiles_WhenOutputIsSourceFolder()
    {
        string source = Path.Combine(_tempDir, "photos");
        Directory.CreateDirectory(source);
        string[] files = { Path.Combine(source, "a.png") };

        var result = MassConvertViewModel.ExcludeFilesInOutputFolder(files, source, source);

        result.Should().BeEquivalentTo(files);
    }

    [Fact]
    public async Task Pdf_ShouldBeReadWithBundledGhostscriptAt300Dpi()
    {
        string source = Path.Combine(_tempDir, "doc.pdf");
        using (var pages = new MagickImageCollection())
        {
            // 1 x 1 inch at 72 DPI
            pages.Add(new MagickImage(MagickColors.White, 72, 72) { Density = new Density(72) });
            pages.Add(new MagickImage(MagickColors.Black, 72, 72) { Density = new Density(72) });
            pages.Write(source, MagickFormat.Pdf);
        }

        GhostscriptLocator.Configure();
        GhostscriptLocator.IsAvailable.Should().BeTrue("gsdll64.dll must be copied next to the app");

        await _sut.ConvertFileAsync(source, _outputDir, new ConversionOptions { TargetFormat = "PNG" }, CancellationToken.None);
        using (var firstPage = new MagickImage(Path.Combine(_outputDir, "doc.png")))
        {
            firstPage.Width.Should().BeInRange(295, 305);
        }

        var options = new ConversionOptions { TargetFormat = "PNG", ExtractAllPages = true };
        await _sut.ConvertFileAsync(source, _outputDir, options, CancellationToken.None);
        Directory.GetFiles(_outputDir, "doc_page*.png").Should().HaveCount(2);
    }

    private string CreateCmykJpeg(string name)
    {
        string path = Path.Combine(_tempDir, name);
        using var image = new MagickImage(MagickColors.Red, 16, 16);
        image.ColorSpace = ColorSpace.CMYK;
        image.Write(path, MagickFormat.Jpeg);

        using var check = new MagickImage(path);
        check.ColorSpace.Should().Be(ColorSpace.CMYK, "the fixture must really be CMYK");
        return path;
    }

    private static string CreatePng(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = new MagickImage(MagickColors.Gray, 8, 8);
        image.Write(path, MagickFormat.Png);
        return path;
    }
}
