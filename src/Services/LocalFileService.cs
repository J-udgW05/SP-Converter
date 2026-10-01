using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SPConverter.Contracts;
using SPConverter.Models;

namespace SPConverter.Services;

public class LocalFileService : IFileManagementService
{
    private static readonly object _fileLock = new();
    private const int MaxUniqueNameAttempts = 100_000;

    public ImageScanResult ScanImagesInPath(string path, bool includeSubfolders)
    {
        if (File.Exists(path))
        {
            bool isSupportedFile = IsSupportedImage(path);
            return new ImageScanResult
            {
                SupportedFiles = isSupportedFile ? new[] { path } : Array.Empty<string>(),
                SkippedFiles = isSupportedFile ? 0 : 1,
                SourceExists = true,
                IsSingleFile = true
            };
        }

        if (!Directory.Exists(path))
        {
            return new ImageScanResult();
        }

        return ScanDirectory(path, includeSubfolders);
    }

    public IEnumerable<string> GetImagesInDirectory(string directoryPath, bool includeSubfolders)
    {
        return ScanImagesInPath(directoryPath, includeSubfolders).SupportedFiles;
    }

    private ImageScanResult ScanDirectory(string directoryPath, bool includeSubfolders)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubfolders,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };

        try
        {
            var supportedFiles = new List<string>();
            int skippedFiles = 0;

            foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*.*", options))
            {
                if (IsSupportedImage(filePath))
                {
                    supportedFiles.Add(filePath);
                }
                else
                {
                    skippedFiles++;
                }
            }

            return new ImageScanResult
            {
                SupportedFiles = supportedFiles,
                SkippedFiles = skippedFiles,
                HasNestedFolders = HasNestedFolders(directoryPath),
                SourceExists = true
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException)
        {
            System.Diagnostics.Debug.WriteLine($"Directory enumeration error for {directoryPath}: {ex.Message}");
            return new ImageScanResult
            {
                HasNestedFolders = HasNestedFolders(directoryPath),
                SourceExists = true
            };
        }
    }

    public bool IsSupportedImage(string filePath)
    {
        return ImageFormatRules.IsSupportedInputFile(filePath);
    }

    public string ReserveUniqueFilePath(string originalPath, string outputDirectory, string targetSuffix)
    {
        if (string.IsNullOrWhiteSpace(targetSuffix))
            throw new ArgumentException("Target suffix cannot be empty.", nameof(targetSuffix));

        string originalFileName = Path.GetFileNameWithoutExtension(originalPath);
        string normalizedSuffix = NormalizeTargetSuffix(targetSuffix);
        string targetFileName = originalFileName + normalizedSuffix;
        string targetNameWithoutExtension = Path.GetFileNameWithoutExtension(targetFileName);
        string targetExtension = Path.GetExtension(targetFileName);
        string newFilePath = Path.Combine(outputDirectory, targetFileName);

        lock (_fileLock)
        {
            for (int counter = 1; counter <= MaxUniqueNameAttempts; counter++)
            {
                try
                {
                    new FileStream(newFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None).Dispose();
                    return newFilePath;
                }
                catch (IOException) when (File.Exists(newFilePath) || Directory.Exists(newFilePath))
                {
                    // Retry only on a name collision; other IO errors are reported.
                    newFilePath = Path.Combine(outputDirectory, $"{targetNameWithoutExtension}_{counter}{targetExtension}");
                }
            }
        }

        throw new IOException($"Could not find a free file name for {targetFileName} in {outputDirectory}.");
    }

    public void MoveToRecycleBin(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        var operation = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = fullPath + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
        };

        int result;
        // SHFileOperation calls are serialized across parallel conversions.
        lock (_recycleLock)
        {
            result = SHFileOperation(ref operation);
        }

        if (result != 0 || operation.fAnyOperationsAborted || File.Exists(fullPath))
        {
            throw new IOException($"Could not move {Path.GetFileName(fullPath)} to the Recycle Bin (code {result}).");
        }
    }

    private static readonly object _recycleLock = new();

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

    private static string NormalizeTargetSuffix(string targetSuffix)
    {
        return targetSuffix.StartsWith(".") || targetSuffix.StartsWith("_")
            ? targetSuffix
            : "." + targetSuffix;
    }

    private static bool HasNestedFolders(string directoryPath)
    {
        try
        {
            return Directory.EnumerateDirectories(directoryPath).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException)
        {
            System.Diagnostics.Debug.WriteLine($"Directory subfolder inspection error for {directoryPath}: {ex.Message}");
            return false;
        }
    }
}
