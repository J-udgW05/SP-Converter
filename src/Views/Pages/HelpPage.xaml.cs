using System.Windows.Controls;
using System.Windows.Input;

namespace SPConverter.Views.Pages;

public partial class HelpPage : Page
{
    public HelpPage()
    {
        InitializeComponent();
    }

    private void OnPageScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewerWheel.ScrollByFixedStep(sender, e);
    }
}
