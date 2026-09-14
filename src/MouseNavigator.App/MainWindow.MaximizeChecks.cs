#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    internal async Task CheckMaximizeAsync(List<string> results)
    {
        var sample=new Window{Title="MouseNavigator 最大化检查",Content=new Microsoft.UI.Xaml.Controls.TextBlock{Text="窗口状态回归检查"}};
        try
        {
            sample.AppWindow.Resize(new(640,480));sample.Activate();await Task.Delay(150);
            var size=sample.AppWindow.Size;var handle=WinRT.Interop.WindowNative.GetWindowHandle(sample);
            var presenter=(OverlappedPresenter)sample.AppWindow.Presenter;
            for(var i=0;i<3;i++)
            {
                var result=platform.ToggleMaximize(handle);await Task.Delay(350);
                var maximized=presenter.State==OverlappedPresenterState.Maximized;
                results.Add(result.Succeeded&&maximized==(i%2==0)?"PASS: native maximize toggle "+(i+1):"FAIL: native maximize toggle "+(i+1));
                if(i==1)results.Add(sample.AppWindow.Size.Width==size.Width&&sample.AppWindow.Size.Height==size.Height?"PASS: restored original size":"FAIL: restored size");
            }
        }
        finally{sample.Close();}
    }
}
#endif