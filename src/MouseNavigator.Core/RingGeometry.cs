namespace MouseNavigator.Core;

/// <summary>Shared by rendering and hit testing. Button zero is centered at the top.</summary>
public static class RingGeometry
{
    public const int MaximumButtons = 12;
    public const double DeadZone = 46;
    public const double GapDegrees = 2;
    public static double Size(int count) => count <= 6 ? 320 : count <= 8 ? 400 : 460;
    public static double OuterRadius(int count) => Size(count) / 2 - 9;
    public static double Angle(int index, int count) => -90 + index * 360d / count;
    public static int? HitTest(double dx, double dy, int count)
    {
        if (count is < 1 or > MaximumButtons) return null;
        var radius = Math.Sqrt(dx * dx + dy * dy);
        if (radius < DeadZone || radius > OuterRadius(count)) return null;
        var angle = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
        var step = 360d / count;
        var index = (int)Math.Floor((angle + step / 2) / step) % count;
        var distance = Math.Abs((angle - index * step + 540) % 360 - 180);
        return distance <= step / 2 - GapDegrees ? index : null;
    }
}