using MouseNavigator.Contracts;

namespace MouseNavigator.Core;

public sealed record ConfigurationLoadResult(NavigatorConfiguration Configuration, string? Warning);

/// <summary>Atomic local replacement, with the previous valid document kept as a backup.</summary>
public sealed class ConfigurationStore(string path)
{
    public string FilePath { get; } = Path.GetFullPath(path);
    public ConfigurationLoadResult Load(Func<NavigatorConfiguration> defaults)
    {
        if (!File.Exists(FilePath)) return new(defaults(), null);
        try { return new(Read(FilePath), null); }
        catch (Exception ex) when (IsFileError(ex))
        {
            try { return new(Read(FilePath + ".bak"), "主配置无法读取，已加载上次备份。原文件保留，保存后才会替换。"); }
            catch (Exception backupError) when (IsFileError(backupError))
            { return new(defaults(), "配置无法读取，暂用默认菜单。原文件保留，保存后才会替换。" + ex.Message); }
        }
    }
    public static NavigatorConfiguration Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > ConfigurationCodec.MaximumBytes) throw new ArgumentException("配置文件不能超过 16 MB。");
        using var reader = new StreamReader(stream);
        return ConfigurationCodec.Deserialize(reader.ReadToEnd());
    }
    public void Save(NavigatorConfiguration configuration)
    {
        var json = ConfigurationCodec.Serialize(configuration);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            if (File.Exists(FilePath))
            {
                var validPrevious = false;
                try { Read(FilePath); validPrevious = true; }
                catch (Exception ex) when (IsFileError(ex)) { }
                File.Replace(temporary, FilePath, validPrevious ? FilePath + ".bak" : null);
            }
            else File.Move(temporary, FilePath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException;
}