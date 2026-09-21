using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace MouseNavigator.App;

/// <summary>Background bytes are embedded verbatim, preserving GIF frames across export.
/// Loading is cancellable by generation; failed image data never escapes an async handler.</summary>
internal sealed class MenuBackgroundView : UserControl
{
    private readonly Border border=new(); private readonly Image image=new(){Stretch=Stretch.UniformToFill};
    private BitmapImage? bitmap;
    private string? data;
    private int generation;
    private bool active=true; public bool IsCircular {get;set;}=true;
#if DEBUG
    internal bool AnimatedForSmoke=>bitmap?.IsAnimatedBitmap==true;
    internal bool PlayingForSmoke=>bitmap?.IsPlaying==true;
#endif
    public MenuBackgroundView()
    {
        border.Child=image;Content=border;IsHitTestVisible=false;
        Loaded+=(_,_)=>Playback();
        Unloaded+=(_,_)=>bitmap?.Stop();
    }
    public void SetActive(bool value){active=value;Playback();}
    private void Playback(){if(bitmap is null)return;if(active&&IsLoaded&&Visibility==Visibility.Visible)bitmap.Play();else bitmap.Stop();}
    public void Update(MenuVisualStyle style,double diameter)
    {
        border.CornerRadius=new(IsCircular?diameter/2:0);Opacity=style.BackgroundOpacity;
        if(data==style.BackgroundImage){Playback();return;}
        data=style.BackgroundImage;var version=++generation;
        bitmap?.Stop();bitmap=null;image.Source=null;
        if(data is not null)_=LoadAsync(data,version);
    }
    private async Task LoadAsync(string encoded,int version)
    {
        try
        {
            ConfigurationCodec.ValidateBackgroundImage(encoded);
            using var stream=new InMemoryRandomAccessStream();
            await stream.WriteAsync(Convert.FromBase64String(encoded).AsBuffer());stream.Seek(0);
            var decoder=await BitmapDecoder.CreateAsync(stream);ValidateDecoder(decoder);stream.Seek(0);
            var next=new BitmapImage{AutoPlay=false};
            await next.SetSourceAsync(stream);
            if(version!=generation){next.Stop();return;}
            bitmap=next;image.Source=next;Playback();
        }
        catch { /* Invalid image/decoder errors leave an empty background, not a crashed UI. */ }
    }
    private static void ValidateDecoder(BitmapDecoder decoder)
    {
        if(decoder.PixelWidth is 0 or >2048||decoder.PixelHeight is 0 or >2048||decoder.FrameCount>300
            ||(ulong)decoder.PixelWidth*decoder.PixelHeight*decoder.FrameCount>100_000_000)
            throw new ArgumentException("背景最大 2048×2048，动图最多 300 帧且总像素不超过一亿。");
    }
    public static async Task<string> ImportAsync(StorageFile file)
    {
        if((await file.GetBasicPropertiesAsync()).Size>4*1024*1024)throw new ArgumentException("背景文件不能超过 4 MB。");
        using var stream=await file.OpenReadAsync();
        var decoder=await BitmapDecoder.CreateAsync(stream);ValidateDecoder(decoder);stream.Seek(0);
        var bytes=new byte[(int)stream.Size];await stream.ReadAsync(bytes.AsBuffer(),(uint)bytes.Length,InputStreamOptions.None);
        var result=Convert.ToBase64String(bytes);ConfigurationCodec.ValidateBackgroundImage(result);return result;
    }
}
