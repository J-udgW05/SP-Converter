using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using SPConverter;
using System.Linq;
using System;
using System.IO;
using System.Security;
using System.Text.Json;
using Microsoft.Win32;
using System.Globalization;
using System.Windows.Media;

namespace SPConverter.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private static readonly object SettingsFileLock = new();

    public static string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "settings.json"
    );

    private class SettingsData
    {
        public string CurrentTheme { get; set; } = "System";
        public bool UseTransparency { get; set; } = false;
        public int CurrentLanguageIndex { get; set; } = 0;
        public bool DeleteOriginalsByDefault { get; set; } = false;
        public bool ExtractAllPagesByDefault { get; set; } = false;
        public bool AlwaysIncludeSubfolders { get; set; } = false;
        public bool PreserveFolderStructure { get; set; } = false;
        public WindowPlacementData? WindowPlacement { get; set; }
    }

    public sealed class WindowPlacementData
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
    }

    private bool _isLoading;

    [ObservableProperty]
    private string _currentTheme = "System";

    [ObservableProperty]
    private bool _useTransparency = false;

    [ObservableProperty]
    private int _currentLanguageIndex = 0;

    [ObservableProperty]
    private bool _deleteOriginalsByDefault = false;

    [ObservableProperty]
    private bool _extractAllPagesByDefault = false;

    [ObservableProperty]
    private bool _alwaysIncludeSubfolders = false;

    [ObservableProperty]
    private bool _preserveFolderStructure = false;

    public WindowPlacementData? WindowPlacement { get; private set; }

    /// <summary>Raised before theme or accent resources are replaced.</summary>
    public event EventHandler? ThemeChanging;

    public SettingsViewModel()
    {
        LoadSettings();
        
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemEventsUserPreferenceChanged;
    }

    private void OnSystemEventsUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        // Theme and accent changes arrive as WM_SETTINGCHANGE (ImmersiveColorSet).
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General
            || Application.Current?.Dispatcher == null)
        {
            return;
        }

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ThemeChanging?.Invoke(this, EventArgs.Empty);

            if (CurrentTheme == "System")
            {
                bool isDark = IsSystemDark();
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(isDark ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light);
                ApplySystemAccentColor();
                ApplyCustomThemeDictionaries(isDark ? "Dark" : "Light");
                ApplyAccentBrushes(isDark);
                ApplyWindowBackground();
            }
            else
            {
                ApplySystemAccentColor();
                ApplyAccentBrushes(CurrentTheme == "Dark");
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void LoadSettings()
    {
        try
        {
            _isLoading = true;
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var savedSettings = JsonSerializer.Deserialize<SettingsData>(json);
                if (savedSettings != null)
                {
                    CurrentTheme = NormalizeTheme(savedSettings.CurrentTheme);
                    UseTransparency = savedSettings.UseTransparency;
                    CurrentLanguageIndex = NormalizeLanguageIndex(savedSettings.CurrentLanguageIndex);
                    DeleteOriginalsByDefault = savedSettings.DeleteOriginalsByDefault;
                    ExtractAllPagesByDefault = savedSettings.ExtractAllPagesByDefault;
                    AlwaysIncludeSubfolders = savedSettings.AlwaysIncludeSubfolders;
                    PreserveFolderStructure = savedSettings.PreserveFolderStructure;
                    WindowPlacement = savedSettings.WindowPlacement;
                }
            }
            else
            {
                DetectOsDefaults();
            }
            
            ApplyCurrentSettings();
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not load settings: {ex.Message}");
            ApplyCurrentSettings();
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not load settings: {ex.Message}");
            ApplyCurrentSettings();
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not load settings: {ex.Message}");
            ApplyCurrentSettings();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyCurrentSettings()
    {
        OnCurrentThemeChanged(CurrentTheme);
        OnUseTransparencyChanged(UseTransparency);
        OnCurrentLanguageIndexChanged(CurrentLanguageIndex);
    }

    private void DetectOsDefaults()
    {
        CurrentTheme = "System";

        string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        CurrentLanguageIndex = lang.Equals("ru", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private static string NormalizeTheme(string? theme)
    {
        return theme switch
        {
            "System" or "Light" or "Dark" => theme,
            _ => "System"
        };
    }

    private static int NormalizeLanguageIndex(int languageIndex)
    {
        return languageIndex == 1 ? 1 : 0;
    }

    private void SaveSettings()
    {
        if (_isLoading) return;

        try
        {
            string? dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var savedSettings = new SettingsData
            {
                CurrentTheme = CurrentTheme,
                UseTransparency = UseTransparency,
                CurrentLanguageIndex = CurrentLanguageIndex,
                DeleteOriginalsByDefault = DeleteOriginalsByDefault,
                ExtractAllPagesByDefault = ExtractAllPagesByDefault,
                AlwaysIncludeSubfolders = AlwaysIncludeSubfolders,
                PreserveFolderStructure = PreserveFolderStructure,
                WindowPlacement = WindowPlacement
            };

            string json = JsonSerializer.Serialize(savedSettings);
            lock (SettingsFileLock)
            {
                using var stream = new FileStream(SettingsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
            }
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not save settings: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not save settings: {ex.Message}");
        }
    }

    public static bool IsSystemDark()
    {
        try
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key != null)
                {
                    object? appVal = key.GetValue("AppsUseLightTheme");
                    object? sysVal = key.GetValue("SystemUsesLightTheme");
                    
                    if (appVal is int appLight) return appLight == 0;
                    if (sysVal is int sysLight) return sysLight == 0;
                }
            }
        }
        catch (SecurityException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read system theme: {ex.Message}");
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read system theme: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read system theme: {ex.Message}");
        }
        return false;
    }

    private static void ApplySystemAccentColor()
    {
        try
        {
            Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent();
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply system accent color: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply system accent color: {ex.Message}");
        }
    }

    /// <summary>Applies the Windows accent color to the app brushes: dark shade on light theme, light shade on dark theme.</summary>
    private static void ApplyAccentBrushes(bool isDark)
    {
        if (Application.Current == null) return;

        Color[] shades = new[]
            {
                Wpf.Ui.Appearance.ApplicationAccentColorManager.PrimaryAccent,
                Wpf.Ui.Appearance.ApplicationAccentColorManager.SecondaryAccent,
                Wpf.Ui.Appearance.ApplicationAccentColorManager.TertiaryAccent
            }
            .OrderBy(RelativeLuminance)
            .ToArray();

        if (shades.All(color => color.A == 0)) return;

        Color fill = isDark ? shades[1] : shades[2];
        Color hover = Color.FromArgb(0xE6, fill.R, fill.G, fill.B);
        Color foreground = RelativeLuminance(fill) > 0.4 ? Colors.Black : Colors.White;
        Color dropHover = Color.FromArgb(isDark ? (byte)0x26 : (byte)0x1F, fill.R, fill.G, fill.B);

        var resources = Application.Current.Resources;
        resources["AppAccentBrush"] = Frozen(fill);
        resources["AppAccentHoverBrush"] = Frozen(hover);
        resources["AppAccentForegroundBrush"] = Frozen(foreground);
        resources["AppDragDropBorderBrush"] = Frozen(fill);
        resources["AppDragDropForegroundBrush"] = Frozen(fill);
        resources["AppDragDropBackgroundHoverBrush"] = Frozen(dropHover);
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    partial void OnCurrentThemeChanged(string value)
    {
        string normalizedTheme = NormalizeTheme(value);
        if (value != normalizedTheme)
        {
            CurrentTheme = normalizedTheme;
            return;
        }

        if (!_isLoading)
        {
            ThemeChanging?.Invoke(this, EventArgs.Empty);
        }

        Wpf.Ui.Appearance.ApplicationTheme resolvedTheme;
        string dictToApply;

        if (value == "System")
        {
            bool isDark = IsSystemDark();
            resolvedTheme = isDark ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;
            dictToApply = isDark ? "Dark" : "Light";
            
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            ApplySystemAccentColor();
        }
        else if (value == "Dark")
        {
            resolvedTheme = Wpf.Ui.Appearance.ApplicationTheme.Dark;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            ApplySystemAccentColor();
            dictToApply = "Dark";
        }
        else
        {
            resolvedTheme = Wpf.Ui.Appearance.ApplicationTheme.Light;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            ApplySystemAccentColor();
            dictToApply = "Light";
        }

        ApplyCustomThemeDictionaries(dictToApply);
        ApplyAccentBrushes(resolvedTheme == Wpf.Ui.Appearance.ApplicationTheme.Dark);

        // Runs after WPF-UI has updated its resources.
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ApplyWindowBackground();
                SaveSettings();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void ApplyCustomThemeDictionaries(string themeName)
    {
        if (Application.Current == null) return;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existingTheme = dictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Resources/Themes/"));
        if (existingTheme != null)
        {
            dictionaries.Remove(existingTheme);
        }

        string sourcePath = themeName switch
        {
            "Dark" => "Resources/Themes/Dark.xaml",
            "Light" => "Resources/Themes/Light.xaml",
            _ => "Resources/Themes/Light.xaml"
        };
        
        try
        {
            dictionaries.Add(new ResourceDictionary { Source = new Uri(sourcePath, UriKind.Relative) });
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply theme resources: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply theme resources: {ex.Message}");
        }
    }

    partial void OnUseTransparencyChanged(bool value)
    {
        if (Application.Current != null)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OnUseTransparencyChanged(value));
                return;
            }

            ApplyWindowBackground();
            SaveSettings();
        }
    }

    public void ApplyWindowBackground()
    {
        try
        {
            if (Application.Current?.MainWindow is not FluentWindow window)
                return;

            var rootGrid = window.Content as System.Windows.Controls.Grid;

            Version osVersion = Environment.OSVersion.Version;
            bool supportsMica = osVersion.Major >= 10 && osVersion.Build >= 22000;
            bool supportsAcrylic = osVersion.Major >= 10 && osVersion.Build >= 17763;

            if (UseTransparency && supportsAcrylic)
            {
                if (rootGrid != null) rootGrid.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "AppBackgroundBrush");
                window.WindowBackdropType = supportsMica ? WindowBackdropType.Mica : WindowBackdropType.Acrylic;
            }
            else
            {
                window.WindowBackdropType = WindowBackdropType.None;
                if (rootGrid != null) rootGrid.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "AppSolidBackgroundBrush");
            }
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply window background: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply window background: {ex.Message}");
        }
    }

    public string AppVersion
    {
        get
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var prefix = Application.Current?.Resources["Settings_VersionPrefix"] as string ?? "Версия ";
            return $"{prefix}{version?.Major}.{version?.Minor}.{version?.Build}";
        }
    }

    public string CurrentLanguageName => NormalizeLanguageIndex(CurrentLanguageIndex) == 1 ? "English" : "Русский";

    partial void OnCurrentLanguageIndexChanged(int value)
    {
        int normalizedLanguageIndex = NormalizeLanguageIndex(value);
        if (value != normalizedLanguageIndex)
        {
            CurrentLanguageIndex = normalizedLanguageIndex;
            return;
        }

        if (Application.Current != null)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var existingDict = dictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Resources/Strings/"));
            if (existingDict != null)
            {
                dictionaries.Remove(existingDict);
            }

            string sourcePath = value == 1 ? "Resources/Strings/en.xaml" : "Resources/Strings/ru.xaml";
            try
            {
                dictionaries.Add(new ResourceDictionary { Source = new Uri(sourcePath, UriKind.Relative) });
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not apply language resources: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not apply language resources: {ex.Message}");
            }
        }
        SaveSettings();
        OnPropertyChanged(nameof(AppVersion));
        OnPropertyChanged(nameof(CurrentLanguageName));
    }

    partial void OnDeleteOriginalsByDefaultChanged(bool value)
    {
        SaveSettings();
    }

    partial void OnExtractAllPagesByDefaultChanged(bool value)
    {
        SaveSettings();
    }

    partial void OnAlwaysIncludeSubfoldersChanged(bool value)
    {
        SaveSettings();
    }

    partial void OnPreserveFolderStructureChanged(bool value)
    {
        SaveSettings();
    }

    public void SaveWindowPlacement(WindowPlacementData placement)
    {
        WindowPlacement = placement;
        SaveSettings();
    }
}
