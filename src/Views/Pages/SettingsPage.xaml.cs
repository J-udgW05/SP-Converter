using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using SPConverter.ViewModels;
using SPConverter.Views;

namespace SPConverter.Views.Pages;

public partial class SettingsPage : Page
{
    private const string GitHubUrl = "https://github.com/J-udgW05/SP-Converter";

    private const string ProjectSiteUrl = "https://j-udgw05.github.io/SP-Converter/";

    private const string ThirdPartyNoticesFileName = "THIRD-PARTY-NOTICES.md";
    private const string ThirdPartyNoticesUrl = GitHubUrl + "/blob/main/" + ThirdPartyNoticesFileName;

    public SettingsPage(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnGitHubLinkClick(object sender, MouseButtonEventArgs e)
    {
        OpenShell(GitHubUrl);
    }

    private void OnProjectSiteClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectSiteUrl)) return;

        OpenShell(ProjectSiteUrl);
    }

    private void OnThirdPartyNoticesClick(object sender, MouseButtonEventArgs e)
    {
        string localNotices = Path.Combine(AppContext.BaseDirectory, ThirdPartyNoticesFileName);
        OpenShell(File.Exists(localNotices) ? localNotices : ThirdPartyNoticesUrl);
    }

    private void OnLanguageButtonClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (LanguageButton.ContextMenu == null) return;

        LanguageButton.ContextMenu.MinWidth = LanguageButton.ActualWidth;
        LanguageButton.ContextMenu.PlacementTarget = LanguageButton;
        LanguageButton.ContextMenu.IsOpen = true;
    }

    private void OnLanguageMenuItemClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel
            || sender is not MenuItem { Tag: string indexText }
            || !int.TryParse(indexText, out int languageIndex))
        {
            return;
        }

        viewModel.CurrentLanguageIndex = languageIndex;
    }

    private void OnPageScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewerWheel.ScrollByFixedStep(sender, e);
    }

    private static void OpenShell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No handler for .md files: fall back to Notepad.
            if (File.Exists(target))
            {
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{target}\"") { UseShellExecute = true });
            }
            else
            {
                Debug.WriteLine($"Could not open {target}: {ex.Message}");
            }
        }
    }
}
