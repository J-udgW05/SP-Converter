using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SPConverter.Models;

public static class ImageFormatRules
{
    private static readonly string[] SupportedInputExtensionList =
    {
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".bmp", ".tiff", ".tif",
        ".tga", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".heic", ".heif", ".psd",
        ".svg", ".gif", ".ico", ".jxl", ".pdf", ".dds", ".exr", ".ppm", ".pgm", ".pbm"
    };

    private static readonly HashSet<string> SupportedInputExtensionSet = new(
        SupportedInputExtensionList,
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> MultiImageExtensionSet = new(
        new[]
        {
            ".gif", ".webp", ".avif", ".tiff", ".tif", ".pdf", ".ico", ".heic", ".heif"
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AlphaCapableInputExtensionSet = new(
        new[]
        {
            ".png", ".webp", ".avif", ".bmp", ".tiff", ".tif", ".tga",
            ".heic", ".heif", ".psd", ".svg", ".gif", ".ico", ".jxl", ".pdf", ".dds", ".exr"
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AlphaFlatteningTargetFormatSet = new(
        new[] { "JPG", "JPEG", "BMP", "PPM", "PGM", "PBM" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> QualityTargetFormatSet = new(
        new[] { "JPG", "JPEG", "WEBP", "AVIF", "JXL" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SupportedTargetFormatSet = new(
        new[]
        {
            "JPG", "JPEG", "PNG", "WEBP", "GIF", "PDF", "TIFF", "BMP",
            "ICO", "AVIF", "JXL", "TGA", "SVG", "PSD", "DDS", "EXR",
            "PPM", "PGM", "PBM"
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> VideoExtensionSet = new(
        new[]
        {
            ".mp4", ".mov", ".mkv", ".avi", ".webm", ".wmv",
            ".m4v", ".mpg", ".mpeg", ".3gp", ".flv"
        },
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> SupportedInputExtensions => SupportedInputExtensionList;

    public static string SupportedInputExtensionsDisplay =>
        string.Join(", ", SupportedInputExtensionList.Select(extension => extension.TrimStart('.').ToUpperInvariant()));

    public static bool IsSupportedInputFile(string filePath)
    {
        return IsSupportedInputExtension(Path.GetExtension(filePath));
    }

    public static bool IsSupportedInputExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && SupportedInputExtensionSet.Contains(extension);
    }

    public static bool IsVideoFile(string filePath)
    {
        return VideoExtensionSet.Contains(Path.GetExtension(filePath));
    }

    public static bool MayContainMultipleImages(string filePath)
    {
        return MultiImageExtensionSet.Contains(Path.GetExtension(filePath));
    }

    public static bool SourceMayContainAlpha(string filePath)
    {
        return AlphaCapableInputExtensionSet.Contains(Path.GetExtension(filePath));
    }

    public static bool TargetFormatReplacesAlphaWithWhite(string targetFormat)
    {
        return AlphaFlatteningTargetFormatSet.Contains(targetFormat);
    }

    public static bool TargetFormatUsesQuality(string targetFormat)
    {
        return QualityTargetFormatSet.Contains(targetFormat);
    }

    public static bool IsSupportedTargetFormat(string targetFormat)
    {
        return SupportedTargetFormatSet.Contains(targetFormat);
    }
}
