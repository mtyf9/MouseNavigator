using Microsoft.UI.Xaml.Media;
using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal static class PreviewPalette
{
    public static global::Windows.UI.Color Color(string? hex,global::Windows.UI.Color fallback)
    {
        if(hex is null)return fallback;var rgb=Convert.ToUInt32(hex[1..],16);
        return global::Windows.UI.Color.FromArgb(255,(byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);
    }
    public static global::Windows.UI.Color Accent=>new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
    private static SolidColorBrush Make(string? hex,global::Windows.UI.Color fallback,double opacity)
    {var c=Color(hex,fallback);c.A=(byte)Math.Round(opacity*255);return new(c);}
    private static global::Windows.UI.Color Shade(double amount)
    {var c=Accent;return global::Windows.UI.Color.FromArgb(255,(byte)(18+c.R*amount),(byte)(18+c.G*amount),(byte)(18+c.B*amount));}
    public static global::Windows.UI.Color RestingAccent=>Shade(.32);
    public static SolidColorBrush Background(WindowPreviewAppearance a)=>Make(a.BackgroundColor,Shade(.12),a.BackgroundOpacity);
    public static SolidColorBrush Card(WindowPreviewAppearance a)=>Make(a.CardColor,Shade(.22),a.CardOpacity);
    public static SolidColorBrush Highlight(WindowPreviewAppearance a)=>Make(a.HighlightColor,Accent,a.CardOpacity);
    public static SolidColorBrush Text(WindowPreviewAppearance a)=>Make(a.TextColor,Microsoft.UI.Colors.White,1);
}