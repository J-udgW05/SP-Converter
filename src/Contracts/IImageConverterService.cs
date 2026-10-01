using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SPConverter.Models;

namespace SPConverter.Contracts;

public interface IImageConverterService
{
    /// <summary>Converts all files; throws ConversionBatchException after the batch if any file failed.</summary>
    Task ConvertFilesAsync(
        IEnumerable<string> filePaths, 
        string outputDirectory, 
        ConversionOptions options, 
        IProgress<ConversionProgress> progress, 
        CancellationToken cancellationToken);

    /// <summary>Converts one file; errors are propagated to the caller.</summary>
    Task ConvertFileAsync(
        string filePath, 
        string outputDirectory, 
        ConversionOptions options, 
        CancellationToken cancellationToken);
}
