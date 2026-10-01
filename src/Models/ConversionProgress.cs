namespace SPConverter.Models;

public class ConversionProgress
{
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public double Percentage => TotalFiles == 0 ? 0 : ((double)ProcessedFiles / TotalFiles) * 100;
}
