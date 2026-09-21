using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MouseNavigator.Contracts;
using System.Runtime.InteropServices.WindowsRuntime;

namespace MouseNavigator.App;

/// <summary>A seamless angular gradient over a filled disc, including the center.
/// Rasterized only when colors or phase change; resizing uses the same texture.</summary>
internal sealed class AngularBackgroundView : UserControl
{
    private readonly Image image=new(){Stretch=Stretch.Fill,IsHitTestVisible=false};
    private (string,string,double)? key;
#if DEBUG
    internal WriteableBitmap? BitmapForSmoke=>image.Source as WriteableBitmap;
#endif
    public AngularBackgroundView(){Content=image;IsHitTestVisible=false;}
    public void Update(MenuVisualStyle style)
    {
        var next=(style.GradientStart,style.GradientEnd,style.GradientAngle);
        if(key==next)return;key=next;
        const int size=512;
        var a=Convert.ToUInt32(style.GradientStart[1..],16);var b=Convert.ToUInt32(style.GradientEnd[1..],16);
        var data=new byte[size*size*4];
        for(var y=0;y<size;y++)for(var x=0;x<size;x++)
        {
            var dx=x+.5-size/2d;var dy=y+.5-size/2d;var radius=Math.Sqrt(dx*dx+dy*dy);
            var alpha=Math.Clamp(size/2d-radius,0,1);
            var angle=Math.Atan2(dy,dx)-style.GradientAngle*Math.PI/180;
            var t=(1-Math.Cos(angle))/2;
            for(var c=0;c<3;c++){var from=(byte)(a>>(8*c));var to=(byte)(b>>(8*c));data[(y*size+x)*4+c]=(byte)Math.Round((from+(to-from)*t)*alpha);}
            data[(y*size+x)*4+3]=(byte)Math.Round(255*alpha);
        }
        var bitmap=new WriteableBitmap(size,size);
        using(var stream=bitmap.PixelBuffer.AsStream())stream.Write(data);
        bitmap.Invalidate();image.Source=bitmap;
    }
}
