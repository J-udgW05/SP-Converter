using System.Windows.Controls;
using System.Windows.Input;
using SPConverter.ViewModels;
using SPConverter.Views;

namespace SPConverter.Views.Pages;

public partial class SingleConvertPage : Page
{
    public SingleConvertPage(SingleConvertViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0 && DataContext is SingleConvertViewModel vm)
            {
                vm.SelectedFile = files[0];
            }
        }
    }

    private void OnPageScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewerWheel.ScrollByFixedStep(sender, e);
    }
}
