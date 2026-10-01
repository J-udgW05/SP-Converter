using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace SPConverter.Views;

/// <summary>CSS-style cubic-bezier easing function.</summary>
public sealed class CubicBezierEase : EasingFunctionBase
{
    private const int NewtonIterations = 8;
    private const double Epsilon = 1e-6;

    public CubicBezierEase()
    {
        // EaseIn mode applies the curve as is.
        EasingMode = EasingMode.EaseIn;
    }

    public CubicBezierEase(double x1, double y1, double x2, double y2) : this()
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }

    protected override double EaseInCore(double normalizedTime)
    {
        if (normalizedTime <= 0) return 0;
        if (normalizedTime >= 1) return 1;

        double t = SolveCurveX(normalizedTime);
        return Sample(t, Y1, Y2);
    }

    // Called on the source instance, so control points are copied.
    protected override Freezable CreateInstanceCore() => new CubicBezierEase(X1, Y1, X2, Y2);

    private static double Sample(double t, double p1, double p2)
    {
        // Bezier with P0 = 0 and P3 = 1.
        double u = 1 - t;
        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }

    private static double SampleDerivative(double t, double p1, double p2)
    {
        double u = 1 - t;
        return 3 * u * u * p1 + 6 * u * t * (p2 - p1) + 3 * t * t * (1 - p2);
    }

    private double SolveCurveX(double x)
    {
        // Newton-Raphson with bisection fallback.
        double t = x;
        for (int i = 0; i < NewtonIterations; i++)
        {
            double error = Sample(t, X1, X2) - x;
            if (Math.Abs(error) < Epsilon) return t;

            double slope = SampleDerivative(t, X1, X2);
            if (Math.Abs(slope) < Epsilon) break;

            t -= error / slope;
        }

        double low = 0, high = 1;
        t = x;
        while (high - low > Epsilon)
        {
            double value = Sample(t, X1, X2);
            if (Math.Abs(value - x) < Epsilon) break;
            if (value < x) low = t; else high = t;
            t = (low + high) / 2;
        }

        return t;
    }
}
