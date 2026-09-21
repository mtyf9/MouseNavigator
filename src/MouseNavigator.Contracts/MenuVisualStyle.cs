namespace MouseNavigator.Contracts;

public enum MenuEntrance { None, Fade, Zoom, Rise, Turn }

/// <summary>Portable skin identity, never an assembly or executable path.
/// Unknown skins retain their settings and render with the built-in fallback.</summary>
public sealed record MenuVisualStyle(
    string SkinId = "builtin.solid",
    MenuEntrance Entrance = MenuEntrance.Zoom,
    int EntranceMilliseconds = 180,
    int HighlightMilliseconds = 120, string GradientStart = "#7864FF", string GradientEnd = "#22D3EE", double GradientAngle = 0, string? BackgroundImage = null, double BackgroundOpacity = 1, double GlassOpacity = .18, bool OutlineEnabled = true, string? OutlineColor = null, string? SolidColor = null, double SolidOpacity = 0);
