using MouseNavigator.Contracts;

namespace MouseNavigator.Core;

public static class RingGeometry
{
    public const double Size = 320;
    public const double DeadZone = 43;
    public const double OuterRadius = 157;
    public static RingSlot? HitTest(double dx, double dy)
    {
        var radius = Math.Sqrt(dx * dx + dy * dy);
        if (radius < DeadZone || radius > OuterRadius) return null;
        return Math.Abs(dx) > Math.Abs(dy)
            ? dx > 0 ? RingSlot.Right : RingSlot.Left
            : dy > 0 ? RingSlot.Bottom : RingSlot.Top;
    }
}
