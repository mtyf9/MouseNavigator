using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;

namespace MouseNavigator.App;

internal sealed class RingWindow : Window
{
    private readonly RadialMenuView surface = new();
    private readonly nint hwnd;
    private OverlayPlacement placement;
    private double sizeScale = 1;
    public RingWindow()
    {
        Title = "MouseNavigator Ring";
        Content = new Microsoft.UI.Xaml.Controls.Viewbox { Child = surface };
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        OverlayWindow.Configure(hwnd);
    }
    public void SetMenu(MenuProfile menu, ActionRegistry registry)
    {
        sizeScale = menu.SizeScale;
        surface.SetMenu(menu, id => registry.Find(id)?.Descriptor);
    }
    public void ShowAt(int x, int y)
    {
        Highlight(null);
        placement = OverlayWindow.Show(hwnd, x, y, surface.Diameter * sizeScale);
        placement = placement with { Scale = placement.Scale * sizeScale };
    }
    public string? HitTest(int x, int y) => surface.HitTest((x - placement.CenterX) / placement.Scale, (y - placement.CenterY) / placement.Scale);
    public void HideRing() => OverlayWindow.Hide(hwnd);
    public void Highlight(string? selected) => surface.Highlight(selected);
#if DEBUG
    internal RadialMenuView ViewForSmoke => surface;
    internal OverlayPlacement PlacementForSmoke => placement;
#endif
}