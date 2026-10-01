using System.Collections.Generic;
using SPConverter.Models;

namespace SPConverter.Contracts;

public interface IFileManagementService
{
    /// <summary>Scans a file or folder and returns the supported images with a summary.</summary>
    ImageScanResult ScanImagesInPath(string path, bool includeSubfolders);

    /// <summary>Returns the supported images in a folder.</summary>
    IEnumerable<string> GetImagesInDirectory(string directoryPath, bool includeSubfolders);
    
    /// <summary>Returns true if the file has a supported input extension.</summary>
    bool IsSupportedImage(string filePath);
    
    /// <summary>Atomically reserves a unique output path in the destination folder.</summary>
    string ReserveUniqueFilePath(string originalPath, string outputDirectory, string targetSuffix);

    /// <summary>Moves a file to the Recycle Bin.</summary>
    void MoveToRecycleBin(string filePath);
}
