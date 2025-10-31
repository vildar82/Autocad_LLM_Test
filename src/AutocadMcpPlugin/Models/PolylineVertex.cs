namespace AutocadMcpPlugin;

/// <summary>
/// Вершина полилинии, задаваемая координатами и опциональным параметром bulge.
/// </summary>
public sealed class PolylineVertex(double x, double y, double bulge = 0)
{
    public double X { get; } = x;

    public double Y { get; } = y;

    public double Bulge { get; } = bulge;
}
