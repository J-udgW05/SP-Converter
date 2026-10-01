using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SPConverter.Models;

public sealed class ConversionBatchException : Exception
{
    public ConversionBatchException(IReadOnlyList<ConversionFailure> failures)
        : base(BuildMessage(failures))
    {
        Failures = failures.ToArray();
    }

    public IReadOnlyList<ConversionFailure> Failures { get; }

    private static string BuildMessage(IReadOnlyList<ConversionFailure> failures)
    {
        if (failures.Count == 1)
        {
            return $"Failed to convert {Path.GetFileName(failures[0].FilePath)}: {failures[0].Message}";
        }

        string firstMessage = failures.FirstOrDefault()?.Message ?? "Unknown error";
        return $"Failed to convert {failures.Count} files. First error: {firstMessage}";
    }
}
