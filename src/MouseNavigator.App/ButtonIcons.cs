using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MouseNavigator.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
namespace MouseNavigator.App;
internal static class ButtonIcons
{
    private static readonly Dictionary<string,BitmapImage> cache=[];
    public static FrameworkElement Create(string? glyph,string? data,double size=24)
    {
        if(data is null) return new FontIcon {Glyph=glyph??"\uE711",FontSize=size};
        if(!cache.TryGetValue(data,out var bitmap))
        {
            bitmap=new BitmapImage(); if(cache.Count>128) cache.Clear(); cache[data]=bitmap;
            Load(bitmap,data);
        }
        return new Image {Source=bitmap,Width=size,Height=size,Stretch=Stretch.Uniform};
    }
    private static async void Load(BitmapImage bitmap,string data)
    {
        try
        {
            ConfigurationCodec.ValidateImage(data);
            using var stream=new InMemoryRandomAccessStream();
            await stream.WriteAsync(Convert.FromBase64String(data).AsBuffer()); stream.Seek(0);
            await bitmap.SetSourceAsync(stream);
        }
        catch { /* Invalid/unsupported image content cannot crash the shared menu renderer. */ }
    }
    public static async Task<string> ImportAsync(StorageFile file)
    {
        if((await file.GetBasicPropertiesAsync()).Size>16*1024*1024) throw new ArgumentException("请选择小于 16 MB 的图片。");
        using var input=await file.OpenReadAsync();
        var decoder=await BitmapDecoder.CreateAsync(input);
        if(decoder.PixelWidth>16000 || decoder.PixelHeight>16000) throw new ArgumentException("图片尺寸过大。");
        var ratio=Math.Min(1,128d/Math.Max(decoder.OrientedPixelWidth,decoder.OrientedPixelHeight));
        var width=Math.Max(1,(uint)Math.Round(decoder.OrientedPixelWidth*ratio));
        var height=Math.Max(1,(uint)Math.Round(decoder.OrientedPixelHeight*ratio));
        var transform=new BitmapTransform {ScaledWidth=Math.Max(1,(uint)Math.Round(decoder.PixelWidth*ratio)),ScaledHeight=Math.Max(1,(uint)Math.Round(decoder.PixelHeight*ratio))};
        var pixels=await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,transform,
            ExifOrientationMode.RespectExifOrientation,ColorManagementMode.DoNotColorManage);
        using var output=new InMemoryRandomAccessStream();
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,output);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,width,height,96,96,pixels.DetachPixelData());
        await encoder.FlushAsync(); output.Seek(0);
        var bytes=new byte[(int)output.Size]; await output.ReadAsync(bytes.AsBuffer(),(uint)bytes.Length,InputStreamOptions.None);
        var result=Convert.ToBase64String(bytes); ConfigurationCodec.ValidateImage(result); return result;
    }
}