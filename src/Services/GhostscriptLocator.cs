using System;
using System.IO;
using ImageMagick;

namespace SPConverter.Services;

/// <summary>Points ImageMagick to the bundled Ghostscript (ghostscript\gsdll64.dll) for PDF input.</summary>
public static class GhostscriptLocator
{
    public const string FolderName = "ghostscript";
    private const string LibraryName = "gsdll64.dll";

    private static readonly object SyncRoot = new();
    private static bool _configured;

    public static bool IsAvailable { get; private set; }

    public static void Configure()
    {
        lock (SyncRoot)
        {
            if (_configured) return;
            _configured = true;

            string directory = Path.Combine(AppContext.BaseDirectory, FolderName);
            if (!File.Exists(Path.Combine(directory, LibraryName)))
            {
                return;
            }

            try
            {
                MagickNET.SetGhostscriptDirectory(directory);
                IsAvailable = true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine($"Could not configure Ghostscript: {ex.Message}");
            }
        }
    }
}
