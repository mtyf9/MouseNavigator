using Microsoft.UI.Xaml.Media;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private readonly global::Windows.UI.ViewManagement.UISettings panelTheme=new();
    private readonly SolidColorBrush panelThemeBrush=new(),presetThemeBrush=new();
    private void InitializePanelTheme()
    {
        void Update()
        {
            var c=panelTheme.GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
            panelThemeBrush.Color=global::Windows.UI.Color.FromArgb(255,(byte)(24+c.R*.10),(byte)(24+c.G*.10),(byte)(24+c.B*.10));
            presetThemeBrush.Color=global::Windows.UI.Color.FromArgb(255,(byte)(28+c.R*.14),(byte)(28+c.G*.14),(byte)(28+c.B*.14));
        }
        void Changed(global::Windows.UI.ViewManagement.UISettings sender,object args)=>DispatcherQueue.TryEnqueue(Update);
        Update();Loaded+=(_,_)=>{Update();panelTheme.ColorValuesChanged+=Changed;};
        Unloaded+=(_,_)=>panelTheme.ColorValuesChanged-=Changed;
    }
}