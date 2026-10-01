namespace SPConverter.Models;

public class ConversionOptions
{
    public string TargetFormat { get; set; } = "JPEG";
    public int Quality { get; set; } = 100;
    public bool DeleteOriginalFiles { get; set; } = false;
    public bool ExtractAllPages { get; set; }

    /// <summary>Source folder; used to mirror subfolders when PreserveFolderStructure is set.</summary>
    public string? SourceRoot { get; set; }

    public bool PreserveFolderStructure { get; set; }
}
