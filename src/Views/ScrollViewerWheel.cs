using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace SPConverter.Views;

/// <summary>Scrolls by WheelStep per wheel notch, proportionally for smaller touchpad deltas.</summary>
internal static class ScrollViewerWheel
{
    private const double WheelStep = 48;
    private const double WheelDeltaPerNotch = 120;

    public static void ScrollByFixedStep(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.Delta == 0)
        {
            return;
        }

        double targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset - e.Delta / WheelDeltaPerNotch * WheelStep,
            0,
            scrollViewer.ScrollableHeight);

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }
}
