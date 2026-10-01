using System;
using System.Collections.Generic;

namespace SPConverter.Models;

public sealed class ImageScanResult
{
    public IReadOnlyList<string> SupportedFiles { get; init; } = Array.Empty<string>();
    public int SkippedFiles { get; init; }
    public bool HasNestedFolders { get; init; }
    public bool SourceExists { get; init; }
    public bool IsSingleFile { get; init; }
}
