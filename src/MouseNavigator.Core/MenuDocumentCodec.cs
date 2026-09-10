using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MouseNavigator.Contracts;
namespace MouseNavigator.Core;
public static class MenuDocumentCodec
{
    private static readonly JsonSerializerOptions Options=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static void Validate(MenuDocument document)
    {
        if(document.SchemaVersion!=1||document.Menu is null||document.Shortcuts is null)throw new ArgumentException("菜单文件格式无效。");
        ConfigurationCodec.Validate(new(3,[document.Menu with{IsGlobalDefault=true}],document.Shortcuts));
    }
    public static string Serialize(MenuDocument document)
    {
        Validate(document);var json=JsonSerializer.Serialize(document,Options);
        if(Encoding.UTF8.GetByteCount(json)>ConfigurationCodec.MaximumBytes)throw new ArgumentException("菜单文件过大。");return json;
    }
    public static MenuDocument Deserialize(string json)
    {
        if(Encoding.UTF8.GetByteCount(json)>ConfigurationCodec.MaximumBytes)throw new ArgumentException("菜单文件过大。");
        try {var result=JsonSerializer.Deserialize<MenuDocument>(json,Options)??throw new ArgumentException("菜单文件为空。");Validate(result);return result;}
        catch(JsonException ex){throw new ArgumentException("菜单文件格式不正确。",ex);}
    }
}