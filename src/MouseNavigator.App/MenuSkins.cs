using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Contracts;

namespace MouseNavigator.App;

/// <summary>Shared background providers. These alter decoration only; the ring layout and
/// hit testing stay unchanged. SkinId remains the serialized key for compatibility.</summary>
internal interface IMenuSkin
{
    string Id { get; }
    string Name { get; }
    FrameworkElement? CreateBackdrop(double diameter);
    double TintStrength(bool selected);
    void UpdateBackdrop(FrameworkElement element,MenuVisualStyle style,double diameter) {}
}

internal static class MenuSkins
{
    internal static IReadOnlyList<IMenuSkin> Available { get; } = [new SolidSkin(), new GlassSkin(),new ColorRingSkin(),new ImageBackground()];
    internal static IMenuSkin Resolve(string? id) => id=="example.moon-cat"?new CatSkin():Available.FirstOrDefault(s=>s.Id==id) ?? Available[0];

    private sealed class ColorRingSkin : IMenuSkin
    {
        public string Id=>"builtin.color-ring";
        public string Name=>"渐变色环";
        public double TintStrength(bool selected)=>selected?.55:.06;
        public FrameworkElement CreateBackdrop(double diameter)=>new AngularBackgroundView();
        public void UpdateBackdrop(FrameworkElement element,MenuVisualStyle style,double diameter)=>
            ((AngularBackgroundView)element).Update(style);
    }
    private sealed class ImageBackground : IMenuSkin
    {
        public string Id=>"builtin.image";
        public string Name=>"自定义图片";
        public double TintStrength(bool selected)=>selected?.55:.12;
        public FrameworkElement CreateBackdrop(double diameter)=>new MenuBackgroundView();
        public void UpdateBackdrop(FrameworkElement element,MenuVisualStyle style,double diameter)=>
            ((MenuBackgroundView)element).Update(style,diameter);
    }
    private sealed class CatSkin : IMenuSkin
    {
        public string Id=>"example.moon-cat";
        public string Name=>"背景示例 · 月光猫";
        public double TintStrength(bool selected)=>selected?.55:.15;
        public FrameworkElement CreateBackdrop(double diameter)
        {
            // Vector art scales with every ring count; it needs no external image resource.
            var art=new Canvas{Width=300,Height=300,IsHitTestVisible=false};
            var blue=new SolidColorBrush(Parse("#334878"));
            art.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse{Width=300,Height=300,Fill=new SolidColorBrush(Parse("#192541"))});
            var face=new Microsoft.UI.Xaml.Shapes.Ellipse{Width=110,Height=100,Fill=blue};Canvas.SetLeft(face,95);Canvas.SetTop(face,36);art.Children.Add(face);
            foreach(var left in new[]{95d,171d})
            {
                var ear=new Microsoft.UI.Xaml.Shapes.Polygon{Fill=blue,Points=new(){new(left,60),new(left+3,14),new(left+34,48)}};
                art.Children.Add(ear);
            }
            foreach(var left in new[]{119d,169d})
            {
                var eye=new Microsoft.UI.Xaml.Shapes.Ellipse{Width=12,Height=18,Fill=new SolidColorBrush(Parse("#FDE7AC"))};
                Canvas.SetLeft(eye,left);Canvas.SetTop(eye,70);art.Children.Add(eye);
            }
            var nose=new TextBlock{Text="ω",FontSize=26,Foreground=new SolidColorBrush(Parse("#F5B9D6"))};
            Canvas.SetLeft(nose,139);Canvas.SetTop(nose,84);art.Children.Add(nose);
            return new Viewbox{Child=art,IsHitTestVisible=false};
        }
    }
    private static global::Windows.UI.Color Parse(string hex)
    {
        var rgb=Convert.ToUInt32(hex[1..],16);
        return Microsoft.UI.ColorHelper.FromArgb(255,(byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);
    }
    private sealed class SolidSkin : IMenuSkin
    {
        public string Id=>"builtin.solid";
        public string Name=>"纯色";
        public FrameworkElement CreateBackdrop(double diameter)=>new Microsoft.UI.Xaml.Shapes.Ellipse{IsHitTestVisible=false};
        public void UpdateBackdrop(FrameworkElement element,MenuVisualStyle style,double diameter)
        {
            var color=style.SolidColor is {} hex?Parse(hex):PreviewPalette.RestingAccent;color.A=(byte)Math.Round(style.SolidOpacity*255);
            ((Microsoft.UI.Xaml.Shapes.Ellipse)element).Fill=new SolidColorBrush(color);
        }
        public double TintStrength(bool selected)=>1;
    }
    private sealed class GlassSkin : IMenuSkin
    {
        public string Id=>"builtin.glass";
        public string Name=>"毛玻璃";
        public FrameworkElement CreateBackdrop(double diameter)=>new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width=diameter,Height=diameter,IsHitTestVisible=false,
            Fill=new AcrylicBrush{TintColor=Microsoft.UI.ColorHelper.FromArgb(255,32,32,32),TintOpacity=.12,TintLuminosityOpacity=.18,
                FallbackColor=Microsoft.UI.ColorHelper.FromArgb(255,40,40,40)}
        };
        public void UpdateBackdrop(FrameworkElement element,MenuVisualStyle style,double diameter)
        {
            var ellipse=(Microsoft.UI.Xaml.Shapes.Ellipse)element;
            if(ellipse.Fill is AcrylicBrush brush){brush.TintOpacity=style.GlassOpacity;brush.TintLuminosityOpacity=style.GlassOpacity;}
            ellipse.Opacity=style.GlassOpacity==0?0:1;
        }
        public double TintStrength(bool selected)=>selected?.48:.08;
    }
}
