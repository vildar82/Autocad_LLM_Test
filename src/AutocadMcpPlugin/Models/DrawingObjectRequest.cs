using System;
using System.Collections.Generic;

namespace AutocadMcpPlugin;

/// <summary>
/// Параметры построения графического объекта.
/// </summary>
public sealed class DrawingObjectRequest
{
    private DrawingObjectRequest(DrawingObjectKind kind, CircleDrawingParameters? circle, LineDrawingParameters? line, PolylineDrawingParameters? polyline)
    {
        Kind = kind;
        Circle = circle;
        Line = line;
        Polyline = polyline;
    }

    public DrawingObjectKind Kind { get; }

    public CircleDrawingParameters? Circle { get; }

    public LineDrawingParameters? Line { get; }

    public PolylineDrawingParameters? Polyline { get; }

    public static DrawingObjectRequest ForCircle(double centerX, double centerY, double radius)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Радиус должен быть больше нуля.");

        return new DrawingObjectRequest(
            DrawingObjectKind.Circle,
            new CircleDrawingParameters(centerX, centerY, radius),
            null,
            null);
    }

    public static DrawingObjectRequest ForLine(double startX, double startY, double endX, double endY)
    {
        return new DrawingObjectRequest(
            DrawingObjectKind.Line,
            null,
            new LineDrawingParameters(startX, startY, endX, endY),
            null);
    }

    public static DrawingObjectRequest ForPolyline(IReadOnlyList<PolylineVertex> vertices, bool closed)
    {
        return new DrawingObjectRequest(
            DrawingObjectKind.Polyline,
            null,
            null,
            new PolylineDrawingParameters(vertices, closed));
    }
}

public sealed class CircleDrawingParameters(double centerX, double centerY, double radius)
{
    public double CenterX { get; } = centerX;

    public double CenterY { get; } = centerY;

    public double Radius { get; } = radius;
}

public sealed class LineDrawingParameters(double startX, double startY, double endX, double endY)
{
    public double StartX { get; } = startX;

    public double StartY { get; } = startY;

    public double EndX { get; } = endX;

    public double EndY { get; } = endY;
}

public sealed class PolylineDrawingParameters
{
    public PolylineDrawingParameters(IReadOnlyList<PolylineVertex> vertices, bool closed)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Closed = closed;
    }

    public IReadOnlyList<PolylineVertex> Vertices { get; }

    public bool Closed { get; }
}
