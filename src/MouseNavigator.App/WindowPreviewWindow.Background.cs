using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal static class PreviewStyle
{
    public static WindowPreviewAppearance Resolve(MenuProfile menu,WindowPreviewAppearance? value=null)
    {
        var appearance=value??menu.PreviewAppearance??new();
        return appearance.FollowMenuBackground?appearance with{BackgroundStyle=menu.VisualStyle??new(),CardColor=menu.NormalColor,HighlightColor=menu.AccentColor,CardOpacity=menu.NormalOpacity*.18}:appearance with{BackgroundStyle=null};
    }
}
internal sealed partial class WindowPreviewWindow
{
    private FrameworkElement? previewBackground;
    private void ApplyBackground()
    {
        if(previewBackground is not null){canvas.Children.Remove(previewBackground);if(previewBackground is MenuBackgroundView oldImage)oldImage.SetActive(false);previewBackground=null;}
        var style=appearance.BackgroundStyle;
        var glass=style?.SkinId=="builtin.glass"&&style.GlassOpacity>0&&!editingAppearance;
        if(glass)
        {
            if(SystemBackdrop is not OverlayAcrylicBackdrop)SystemBackdrop=new OverlayAcrylicBackdrop();
            ((OverlayAcrylicBackdrop)SystemBackdrop).SetOpacity(style!.GlassOpacity);
        }
        else if(SystemBackdrop is not TransparentBackdrop)SystemBackdrop=new TransparentBackdrop();
        canvas.Background=style is null?PreviewPalette.Background(appearance):new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        if(style is null||glass)return;
        if(style.SkinId=="builtin.image")
        {
            var image=new MenuBackgroundView{IsCircular=false};image.Update(style,canvas.Width);previewBackground=image;
        }
        else if(style.SkinId=="builtin.glass")
            previewBackground=new Border{Background=new AcrylicBrush{TintColor=Microsoft.UI.Colors.Gray,TintOpacity=style.GlassOpacity,TintLuminosityOpacity=style.GlassOpacity,FallbackColor=Microsoft.UI.Colors.DimGray},Opacity=style.GlassOpacity==0?0:1};
        else if(style.SkinId=="builtin.color-ring")
        {
            var angular=new AngularBackgroundView{Width=512,Height=512};angular.Update(style);previewBackground=new Viewbox{Stretch=Stretch.Fill,Child=angular};
        }
        else
        {
            var color=PreviewPalette.Color(style.SolidColor,PreviewPalette.RestingAccent);color.A=(byte)Math.Round(style.SolidOpacity*255);
            previewBackground=new Border{Background=new SolidColorBrush(color)};
        }
        previewBackground.Width=canvas.Width;previewBackground.Height=canvas.Height;previewBackground.IsHitTestVisible=false;canvas.Children.Insert(0,previewBackground);
    }
}
