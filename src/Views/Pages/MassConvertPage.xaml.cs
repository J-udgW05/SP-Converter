using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SPConverter.ViewModels;
using SPConverter.Views;

namespace SPConverter.Views.Pages;

public partial class MassConvertPage : Page
{
    public MassConvertPage(MassConvertViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private MassConvertViewModel ViewModel => (MassConvertViewModel)DataContext;

    // Tunnelling events for the whole page: text boxes would otherwise handle file drops.
    private void OnDragEnter(object sender, DragEventArgs e)
    {
        UpdateDragEffects(e);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        UpdateDragEffects(e);
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        // DragLeave also fires when moving between child elements.
        Point position = e.GetPosition(PageScrollViewer);
        if (new Rect(PageScrollViewer.RenderSize).Contains(position))
        {
            return;
        }

        ViewModel.ClearDropPreview();
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        string? dropPath = FirstDropPath(e);
        ViewModel.ClearDropPreview();

        if (dropPath != null && ViewModel.IsIdle)
        {
            await ViewModel.SetSourcePathAsync(dropPath);
        }
    }

    private void UpdateDragEffects(DragEventArgs e)
    {
        string? dropPath = FirstDropPath(e);
        if (dropPath != null && ViewModel.IsIdle)
        {
            ViewModel.PreviewDropPath(dropPath);
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnOtherFormatsClick(object sender, RoutedEventArgs e)
    {
        if (OtherFormatsButton.ContextMenu == null) return;

        OtherFormatsButton.ContextMenu.MinWidth = OtherFormatsButton.ActualWidth;
        OtherFormatsButton.ContextMenu.PlacementTarget = OtherFormatsButton;
        OtherFormatsButton.ContextMenu.IsOpen = true;
    }

    private void OnOtherFormatMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string format })
        {
            ViewModel.SelectTargetFormatCommand.Execute(format);
        }
    }

    private void OnCustomQualityPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsAsciiDigit);
    }

    private void OnCustomQualityPasting(object sender, DataObjectPastingEventArgs e)
    {
        string? text = e.DataObject.GetData(DataFormats.UnicodeText) as string;
        if (string.IsNullOrEmpty(text) || !text.All(char.IsAsciiDigit))
        {
            e.CancelCommand();
        }
    }

    private void OnCustomQualityKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitCustomQuality();
            e.Handled = true;
        }
    }

    private void OnCustomQualityLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitCustomQuality();
    }

    private void CommitCustomQuality()
    {
        BindingExpression? binding = CustomQualityBox.GetBindingExpression(TextBox.TextProperty);
        if (binding == null) return;

        // An empty value would leave a validation error; restore the bound value.
        if (string.IsNullOrWhiteSpace(CustomQualityBox.Text))
        {
            binding.UpdateTarget();
            return;
        }

        binding.UpdateSource();
        binding.UpdateTarget();
    }

    private void OnPageScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewerWheel.ScrollByFixedStep(sender, e);
    }

    private static string? FirstDropPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        return (e.Data.GetData(DataFormats.FileDrop) as string[])?.FirstOrDefault();
    }
}
