using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using SPConverter.Contracts;
using SPConverter.Models;

namespace SPConverter.Services;

public class MagickImageConverter : IImageConverterService
{
    private sealed record TargetFormatSpec(string Name, string Extension, MagickFormat Format);

    private static readonly uint[] IcoOutputSizes = { 256, 128, 64, 48, 32, 16 };

    // Ghostscript renders at 72 DPI by default.
    private const double PdfRasterDensity = 300;

    private static readonly IReadOnlyDictionary<string, TargetFormatSpec> TargetFormats =
        new Dictionary<string, TargetFormatSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["JPG"] = new("JPG", ".jpg", MagickFormat.Jpeg),
            ["JPEG"] = new("JPEG", ".jpeg", MagickFormat.Jpeg),
            ["PNG"] = new("PNG", ".png", MagickFormat.Png),
            ["WEBP"] = new("WEBP", ".webp", MagickFormat.WebP),
            ["AVIF"] = new("AVIF", ".avif", MagickFormat.Avif),
            ["BMP"] = new("BMP", ".bmp", MagickFormat.Bmp),
            ["TGA"] = new("TGA", ".tga", MagickFormat.Tga),
            ["HEIC"] = new("HEIC", ".heic", MagickFormat.Heic),
            ["HEIF"] = new("HEIF", ".heif", MagickFormat.Heic),
            ["TIFF"] = new("TIFF", ".tiff", MagickFormat.Tiff),
            ["ICO"] = new("ICO", ".ico", MagickFormat.Ico),
            ["JXL"] = new("JXL", ".jxl", MagickFormat.Jxl),
            ["PDF"] = new("PDF", ".pdf", MagickFormat.Pdf),
            ["GIF"] = new("GIF", ".gif", MagickFormat.Gif),
            ["PSD"] = new("PSD", ".psd", MagickFormat.Psd),
            ["SVG"] = new("SVG", ".svg", MagickFormat.Svg),
            ["DDS"] = new("DDS", ".dds", MagickFormat.Dds),
            ["EXR"] = new("EXR", ".exr", MagickFormat.Exr),
            ["PPM"] = new("PPM", ".ppm", MagickFormat.Ppm),
            ["PGM"] = new("PGM", ".pgm", MagickFormat.Pgm),
            ["PBM"] = new("PBM", ".pbm", MagickFormat.Pbm)
        };

    // No reliable signature; TIFF-based RAW would otherwise decode as TIFF (embedded preview only).
    private static readonly IReadOnlyDictionary<string, MagickFormat> ExplicitInputFormats =
        new Dictionary<string, MagickFormat>(StringComparer.OrdinalIgnoreCase)
        {
            [".tga"] = MagickFormat.Tga,
            [".cr2"] = MagickFormat.Cr2,
            [".cr3"] = MagickFormat.Cr3,
            [".nef"] = MagickFormat.Nef,
            [".arw"] = MagickFormat.Arw,
            [".dng"] = MagickFormat.Dng,
            [".svg"] = MagickFormat.Svg,
            [".dds"] = MagickFormat.Dds,
            [".ico"] = MagickFormat.Ico
        };

    // Animation frames are stored as deltas and must be coalesced before extraction.
    private static readonly HashSet<string> AnimatedInputExtensions = new(
        new[] { ".gif", ".webp", ".avif" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<MagickFormat> CmykCapableTargets = new()
    {
        MagickFormat.Jpeg, MagickFormat.Tiff, MagickFormat.Psd, MagickFormat.Pdf
    };

    private readonly IFileManagementService _fileService;

    static MagickImageConverter()
    {
        GhostscriptLocator.Configure();
    }

    public MagickImageConverter(IFileManagementService fileService)
    {
        _fileService = fileService;
    }

    public async Task ConvertFilesAsync(
        IEnumerable<string> filePaths,
        string outputDirectory,
        ConversionOptions options,
        IProgress<ConversionProgress> progress,
        CancellationToken cancellationToken)
    {
        var filesList = filePaths.ToList();
        int totalFiles = filesList.Count;
        int processedFiles = 0;
        var conversionFailures = new ConcurrentBag<ConversionFailure>();
        TargetFormatSpec targetFormat = ResolveTargetFormat(options.TargetFormat);

        if (totalFiles == 0) return;

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        // ImageMagick already uses several threads per image.
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(filesList, parallelOptions, async (filePath, token) =>
        {
            try
            {
                await ConvertFileInternalAsync(filePath, outputDirectory, options, targetFormat, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                conversionFailures.Add(new ConversionFailure(filePath, DescribeFailure(filePath, ex)));
                System.Diagnostics.Debug.WriteLine($"Error converting {filePath}: {ex.Message}");
            }
            finally
            {
                int processed = Interlocked.Increment(ref processedFiles);
                progress?.Report(new ConversionProgress
                {
                    TotalFiles = totalFiles,
                    ProcessedFiles = processed,
                    CurrentFileName = Path.GetFileName(filePath)
                });
            }
        });

        if (!conversionFailures.IsEmpty)
        {
            throw new ConversionBatchException(conversionFailures.OrderBy(failure => failure.FilePath).ToList());
        }
    }

    public async Task ConvertFileAsync(
        string filePath,
        string outputDirectory,
        ConversionOptions options,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        TargetFormatSpec targetFormat = ResolveTargetFormat(options.TargetFormat);
        await ConvertFileInternalAsync(filePath, outputDirectory, options, targetFormat, cancellationToken);
    }

    private async Task ConvertFileInternalAsync(
        string filePath,
        string outputDirectory,
        ConversionOptions options,
        TargetFormatSpec targetFormat,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string targetExt = targetFormat.Extension;
        MagickFormat format = targetFormat.Format;
        string fileOutputDirectory = ResolveFileOutputDirectory(filePath, outputDirectory, options);

        bool flattensAlpha = ImageFormatRules.TargetFormatReplacesAlphaWithWhite(targetFormat.Name);
        bool isQualityFormat = ImageFormatRules.TargetFormatUsesQuality(targetFormat.Name);

        int effectiveQuality = EffectiveQualityForFormat(targetFormat, options.Quality);
        var generatedFiles = new List<string>();

        void PrepareImageForOutput(IMagickImage<ushort> img)
        {
            img.AutoOrient();
            ConvertCmykForScreenFormats(img, format);
            img.Format = format;
            if (flattensAlpha && img.HasAlpha)
            {
                img.BackgroundColor = MagickColors.White;
                img.Alpha(AlphaOption.Remove);
            }
            if (isQualityFormat)
            {
                img.Quality = (uint)effectiveQuality;
            }
        }

        void ProcessAndSave(IMagickImage<ushort> img, string savePath)
        {
            if (format == MagickFormat.Ico)
            {
                SaveIconSet(img, savePath);
                return;
            }

            PrepareImageForOutput(img);
            img.Write(savePath);
        }

        void SaveReservedOutput(IMagickImage<ushort> img, string savePath)
        {
            bool saved = false;
            try
            {
                ProcessAndSave(img, savePath);
                saved = true;
                generatedFiles.Add(savePath);
            }
            finally
            {
                if (!saved)
                {
                    DeleteReservedOutput(savePath);
                }
            }
        }

        await Task.Run(() =>
        {
            if (!Directory.Exists(fileOutputDirectory))
                Directory.CreateDirectory(fileOutputDirectory);

            using var fileStream = File.OpenRead(filePath);
            string sourceExtension = Path.GetExtension(filePath);

            if (options.ExtractAllPages)
            {
                using var collection = new MagickImageCollection(fileStream, CreateReadSettings(sourceExtension, singleFrame: false));
                if (collection.Count > 1 && AnimatedInputExtensions.Contains(sourceExtension))
                {
                    collection.Coalesce();
                }

                int count = collection.Count;
                int index = 1;
                foreach (var img in collection)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string pageSuffix = count > 1 ? $"_page{index}{targetExt}" : targetExt;
                    string targetFile = _fileService.ReserveUniqueFilePath(filePath, fileOutputDirectory, pageSuffix);
                    SaveReservedOutput(img, targetFile);
                    index++;
                }
            }
            else if (string.Equals(sourceExtension, ".ico", StringComparison.OrdinalIgnoreCase))
            {
                // ICO entries are often ordered smallest first.
                using var collection = new MagickImageCollection(fileStream, CreateReadSettings(sourceExtension, singleFrame: false));
                using var largest = collection.OrderByDescending(img => (long)img.Width * img.Height).First().Clone();
                string targetFile = _fileService.ReserveUniqueFilePath(filePath, fileOutputDirectory, targetExt);
                SaveReservedOutput(largest, targetFile);
            }
            else
            {
                using var image = new MagickImage(fileStream, CreateReadSettings(sourceExtension, singleFrame: true));
                string targetFile = _fileService.ReserveUniqueFilePath(filePath, fileOutputDirectory, targetExt);
                SaveReservedOutput(image, targetFile);
            }

        }, cancellationToken);

        // Originals are removed only after every output file was written.
        if (options.DeleteOriginalFiles && File.Exists(filePath))
        {
            string fullSource = Path.GetFullPath(filePath);

            bool safeToDelete = generatedFiles.All(gf =>
                !string.Equals(fullSource, Path.GetFullPath(gf), StringComparison.OrdinalIgnoreCase) &&
                File.Exists(gf));

            if (safeToDelete && generatedFiles.Count > 0)
            {
                _fileService.MoveToRecycleBin(filePath);
            }
        }
    }

    private static MagickReadSettings CreateReadSettings(string sourceExtension, bool singleFrame)
    {
        var settings = new MagickReadSettings();

        if (ExplicitInputFormats.TryGetValue(sourceExtension, out MagickFormat inputFormat))
        {
            settings.Format = inputFormat;
        }

        if (string.Equals(sourceExtension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            settings.Density = new Density(PdfRasterDensity, PdfRasterDensity);
        }

        if (singleFrame)
        {
            // Decode only the first page or frame.
            settings.FrameIndex = 0;
            settings.FrameCount = 1;
        }

        return settings;
    }

    /// <summary>Mirrors the source subfolder in the output when PreserveFolderStructure is set.</summary>
    private static string ResolveFileOutputDirectory(string filePath, string outputDirectory, ConversionOptions options)
    {
        if (!options.PreserveFolderStructure || string.IsNullOrWhiteSpace(options.SourceRoot))
        {
            return outputDirectory;
        }

        string? sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (sourceDirectory == null)
        {
            return outputDirectory;
        }

        string relativeDirectory = Path.GetRelativePath(Path.GetFullPath(options.SourceRoot), sourceDirectory);
        bool isOutsideSourceRoot = relativeDirectory == ".."
                                   || relativeDirectory.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                                   || Path.IsPathRooted(relativeDirectory);

        return isOutsideSourceRoot || relativeDirectory == "."
            ? outputDirectory
            : Path.Combine(outputDirectory, relativeDirectory);
    }

    /// <summary>Converts CMYK to sRGB for formats that are displayed on screen.</summary>
    private static void ConvertCmykForScreenFormats(IMagickImage<ushort> img, MagickFormat targetFormat)
    {
        if (img.ColorSpace != ColorSpace.CMYK || CmykCapableTargets.Contains(targetFormat))
        {
            return;
        }

        if (img.GetColorProfile() != null)
        {
            img.TransformColorSpace(ColorProfiles.SRGB);
        }
        else
        {
            img.ColorSpace = ColorSpace.sRGB;
        }
    }

    private static string DescribeFailure(string filePath, Exception exception)
    {
        if (string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase)
            && !GhostscriptLocator.IsAvailable)
        {
            return "Ghostscript was not found next to SPConverter.exe, PDF files cannot be read.";
        }

        return exception.Message;
    }

    private static void DeleteReservedOutput(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not delete reserved output file {filePath}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not delete reserved output file {filePath}: {ex.Message}");
        }
    }

    private static int EffectiveQualityForFormat(TargetFormatSpec targetFormat, int requestedQuality)
    {
        int clampedQuality = Math.Clamp(requestedQuality, 1, 100);

        // AVIF quality 100 means lossless, which the bundled encoder rejects.
        return targetFormat.Format == MagickFormat.Avif && clampedQuality == 100
            ? 99
            : clampedQuality;
    }

    private static void SaveIconSet(IMagickImage<ushort> sourceImage, string savePath)
    {
        sourceImage.AutoOrient();
        ConvertCmykForScreenFormats(sourceImage, MagickFormat.Ico);

        // Sizes larger than the source are skipped to avoid upscaling.
        uint largestSide = Math.Max(sourceImage.Width, sourceImage.Height);
        uint[] sizes = IcoOutputSizes.Where(size => size <= largestSide).ToArray();
        if (sizes.Length == 0)
        {
            sizes = new[] { IcoOutputSizes[^1] };
        }

        using var icons = new MagickImageCollection();
        foreach (uint size in sizes)
        {
            IMagickImage<ushort> icon = sourceImage.Clone();
            icon.Format = MagickFormat.Ico;
            icon.Alpha(AlphaOption.Set);
            icon.BackgroundColor = MagickColors.Transparent;
            icon.Resize(new MagickGeometry(size, size)
            {
                IgnoreAspectRatio = false
            });
            icon.Extent(size, size, Gravity.Center, MagickColors.Transparent);
            icons.Add(icon);
        }

        icons.Write(savePath);
    }

    private static TargetFormatSpec ResolveTargetFormat(string targetFormat)
    {
        if (string.IsNullOrWhiteSpace(targetFormat)
            || !TargetFormats.TryGetValue(targetFormat.Trim(), out TargetFormatSpec? targetFormatSpec))
        {
            throw new NotSupportedException($"Output format '{targetFormat}' is not supported.");
        }

        var formatInfo = MagickFormatInfo.Create(targetFormatSpec.Format);
        if (formatInfo?.SupportsWriting != true)
        {
            throw new NotSupportedException(
                $"Output format '{targetFormatSpec.Name}' cannot be written by this build of SP Converter.");
        }

        return targetFormatSpec;
    }
}
