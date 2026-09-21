using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
namespace MouseNavigator.App;

/// <summary>The navigation overlay never takes focus. Keep its acrylic active while visible;
/// DesktopAcrylicController still observes the OS transparency/accessibility policy.</summary>
internal sealed class OverlayAcrylicBackdrop : SystemBackdrop
{
    private double opacity=.18;
    public void SetOpacity(double value)
    {
        opacity=Math.Clamp(value,0,1);
        if(controller is not null){controller.TintOpacity=(float)opacity;controller.LuminosityOpacity=(float)opacity;}
    }
    private DesktopAcrylicController? controller;
    private SystemBackdropConfiguration? configuration;
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target,XamlRoot root)
    {
        if(!DesktopAcrylicController.IsSupported())return;
        configuration=new(){IsInputActive=true,Theme=SystemBackdropTheme.Dark};
        controller=new();
        controller.SetSystemBackdropConfiguration(configuration);
        controller.Kind=DesktopAcrylicKind.Thin;
        controller.TintColor=Microsoft.UI.ColorHelper.FromArgb(255,32,32,32);
        controller.TintOpacity=(float)opacity;
        controller.LuminosityOpacity=(float)opacity;
        controller.FallbackColor=Microsoft.UI.ColorHelper.FromArgb(255,40,40,40);
        controller.AddSystemBackdropTarget(target);
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        controller?.RemoveSystemBackdropTarget(target);
        controller?.Dispose();controller=null;configuration=null;
    }
}
