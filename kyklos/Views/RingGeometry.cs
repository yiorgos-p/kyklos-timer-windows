using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Kyklos.Views;

/// <summary>Drawing round a ring as the dial does, with angles running clockwise from 12 o'clock.</summary>
internal static class RingGeometry
{
    public static Point PointAt(Point center, double radius, double angle) =>
        new(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius);

    /// <summary>An arc from 12 o'clock, clockwise, round this fraction of the ring.</summary>
    public static Geometry Arc(Point center, double radius, double fraction)
    {
        // An arc can't close on itself, so a full ring (time's up, or the whole hour set) is a circle.
        if (fraction >= 1)
        {
            return new EllipseGeometry { Center = center, RadiusX = radius, RadiusY = radius };
        }

        var figure = new PathFigure { StartPoint = PointAt(center, radius, 0) };
        figure.Segments.Add(new ArcSegment
        {
            Point = PointAt(center, radius, fraction * 2 * Math.PI),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = fraction > 0.5,
        });

        return new PathGeometry { Figures = { figure } };
    }
}
