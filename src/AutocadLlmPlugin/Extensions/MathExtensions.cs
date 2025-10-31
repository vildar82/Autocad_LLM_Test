using System;

namespace AutocadLlmPlugin;

public static class MathExtensions
{
    public const double Tolerance = 0.00001;

    public static bool IsEqualTo(this double value1, double value2) =>
        Math.Abs(value1 - value2) < Tolerance;
}