namespace Kyklos.Tests;

public class DialTests
{
    // The dial is 220 epx across, and the ring runs at radius 89.
    private const double Center = 110;
    private const double Radius = 89;

    [Theory]
    [InlineData(110, 0, 60)]
    [InlineData(220, 110, 15)]
    [InlineData(110, 220, 30)]
    [InlineData(0, 110, 45)]
    public void ClockPositionsAreMinutes(double x, double y, int minutes) =>
        Assert.Equal(minutes, Dial.MinutesAt(x, y, Center, lastDragMinutes: null));

    [Fact]
    public void DraggingPastTwelveFrom58StaysAt60()
    {
        var (x, y) = PointAt(minute: 1);
        Assert.Equal(60, Dial.MinutesAt(x, y, Center, lastDragMinutes: 58));

        (x, y) = PointAt(minute: 2);
        Assert.Equal(60, Dial.MinutesAt(x, y, Center, lastDragMinutes: 60));
    }

    [Fact]
    public void DraggingBackPastTwelveFrom2StaysAt1()
    {
        var (x, y) = PointAt(minute: 59);

        Assert.Equal(1, Dial.MinutesAt(x, y, Center, lastDragMinutes: 2));
    }

    /// <summary>The point on the ring at a minute mark.</summary>
    private static (double X, double Y) PointAt(int minute)
    {
        var angle = minute * 6 * Math.PI / 180 - Math.PI / 2;
        return (Center + Math.Cos(angle) * Radius, Center + Math.Sin(angle) * Radius);
    }
}
