using System.Text.Encodings.Web;
using System.Text.Json;

namespace LabelTool;

/// <summary>绑定打印机的共用排版参数（打印偏移单独存于 AppState.PrinterOffsets）</summary>
public class PrinterSharedParams
{
    public double FontPt { get; set; } = 11;
    public string FontFamily { get; set; } = "宋体";
    public double LineSpacing { get; set; }
    public double CharSpacing { get; set; }
    public double Weight { get; set; }
    public string PrintMode { get; set; } = "Sheet";
    public string? PaperName { get; set; }
    public double Rotation { get; set; } = 0;
    public string HAlign { get; set; } = "Center";
    public string VAlign { get; set; } = "Middle";
}

/// <summary>标签预设：名称、宽、高、绑定打印机为独有参数，其余排版参数按绑定打印机共用或自定义</summary>
public class Preset
{
    /// <summary>名称（独有）</summary>
    public string Name { get; set; } = "";
    /// <summary>宽（独有，mm）</summary>
    public double WidthMm { get; set; } = 50;
    /// <summary>高（独有，mm）</summary>
    public double HeightMm { get; set; } = 14;
    /// <summary>绑定的打印机名（独有，空表示不绑定）</summary>
    public string? PrinterName { get; set; }

    /// <summary>是否使用当前标签自定义参数（false=使用绑定打印机共用参数，true=单独自定义）</summary>
    public bool UseCustomParams { get; set; } = false;

    // 以下为共用参数；UseCustomParams=true 时使用此处的自定义值，否则引用绑定打印机的 PrinterSharedParams
    public double FontPt { get; set; } = 11;
    public string FontFamily { get; set; } = "宋体";
    public double LineSpacing { get; set; }
    public double CharSpacing { get; set; }
    public double Weight { get; set; }
    public string PrintMode { get; set; } = "Sheet";
    public string? PaperName { get; set; }
    public double Rotation { get; set; } = 0;
    public double OffsetX { get; set; } = 0;
    public double OffsetY { get; set; } = 0;
    public string HAlign { get; set; } = "Center";
    public string VAlign { get; set; } = "Middle";
}

/// <summary>某个标签类型（预设）在当前柜子里的标签内容与字体覆盖</summary>
public class TypeData
{
    public List<string> Labels { get; set; } = new();

    /// <summary>字体覆盖（null=用预设）</summary>
    public string? FontFamily { get; set; }
    /// <summary>字号覆盖（0=用预设）</summary>
    public double FontSize { get; set; }
    /// <summary>自动缩小以适应框</summary>
    public bool AutoFit { get; set; }
    /// <summary>自适应字号（放大到刚好填满框且不越界）</summary>
    public bool AutoSize { get; set; }

    /// <summary>行距覆盖（mm，0=用预设）</summary>
    public double LineSpacing { get; set; }
    /// <summary>字距覆盖（mm，0=用预设）</summary>
    public double CharSpacing { get; set; }
    /// <summary>加粗覆盖（mm，0=用预设）</summary>
    public double Weight { get; set; }
}

/// <summary>柜子：一个项目下的一个柜子，按标签类型存标签内容，独立存为一个文件</summary>
public class Cabinet
{
    public string Project { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>标签类型名(预设名) -> 内容</summary>
    public Dictionary<string, TypeData> Types { get; set; } = new();
    /// <summary>上次打开的标签类型</summary>
    public string CurrentType { get; set; } = "";
}

public class ProjectInfo
{
    public string Name { get; set; } = "";
    public List<string> Cabinets { get; set; } = new();
}

public class PaperSetting
{
    public string Name { get; set; } = "A4";
    public double WidthMm { get; set; } = 210;
    public double HeightMm { get; set; } = 297;
}

/// <summary>程序全部持久化状态</summary>
public class AppState
{
    public PaperSetting Paper { get; set; } = new();
    public List<Preset> Presets { get; set; } = new();
    public List<ProjectInfo> Projects { get; set; } = new();
    public string CurrentProject { get; set; } = "";
    public string CurrentCabinet { get; set; } = "";
    public string CurrentPresetName { get; set; } = "";
    public List<string> Words { get; set; } = new();

    /// <summary>“二合一/三合一粘贴”合并多行时的连接符（默认一个空格）</summary>
    public string MergeSeparator { get; set; } = " ";
    /// <summary>打印机别名：真实打印机名 -&gt; 别名（预览里显示别名）</summary>
    public Dictionary<string, string> PrinterAliases { get; set; } = new();

    /// <summary>按打印机保存的打印偏移(mm)：真实打印机名-&gt; 偏移</summary>
    public Dictionary<string, PrintOffset> PrinterOffsets { get; set; } = new();

    /// <summary>按绑定打印机统一的共用排版参数：真实打印机名-&gt; 共用参数</summary>
    public Dictionary<string, PrinterSharedParams> PrinterSharedParams { get; set; } = new();
}

/// <summary>某台打印机的打印偏移(mm)，用于校正打印位置</summary>
public class PrintOffset
{
    public double X { get; set; }
    public double Y { get; set; }
}

public static class Storage
{
    public static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");
    private static string StatePath => Path.Combine(DataDir, "state.json");
    public static string CabinetsDir => Path.Combine(DataDir, "cabinets");

    private static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static AppState Load()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                var json = File.ReadAllText(StatePath);
                var s = JsonSerializer.Deserialize<AppState>(json, Opt);
                if (s != null)
                {
                    if (s.Presets.Count == 0) s.Presets = DefaultPresets();
                    // 旧版行距为倍数（默认为1.0），新版为mm；把遗留的1.0迁移为0
                    foreach (var p in s.Presets)
                        if (Math.Abs(p.LineSpacing - 1.0) < 1e-6) p.LineSpacing = 0;

                    if (s.PrinterSharedParams == null) s.PrinterSharedParams = new();
                    foreach (var p in s.Presets)
                    {
                        var pn = p.PrinterName;
                        if (string.IsNullOrWhiteSpace(pn)) continue;
                        if (!s.PrinterSharedParams.ContainsKey(pn))
                        {
                            s.PrinterSharedParams[pn] = new PrinterSharedParams
                            {
                                FontPt = p.FontPt,
                                FontFamily = string.IsNullOrEmpty(p.FontFamily) ? "宋体" : p.FontFamily,
                                LineSpacing = p.LineSpacing,
                                CharSpacing = p.CharSpacing,
                                Weight = p.Weight,
                                PrintMode = string.IsNullOrEmpty(p.PrintMode) ? "Sheet" : p.PrintMode,
                                PaperName = p.PaperName,
                                Rotation = p.Rotation,
                                HAlign = string.IsNullOrEmpty(p.HAlign) ? "Center" : p.HAlign,
                                VAlign = string.IsNullOrEmpty(p.VAlign) ? "Middle" : p.VAlign,
                            };
                        }
                        p.UseCustomParams = false;
                    }
                    return s;
                }
            }
        }
        catch { }
        var fresh = new AppState();
        fresh.Presets = DefaultPresets();
        return fresh;
    }

    public static void Save(AppState s)
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(s, Opt));
    }

    // ---------- 柜子文件 ----------
    private static string San(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));

    public static string CabinetPath(string project, string name) =>
        Path.Combine(CabinetsDir, San(project), San(name) + ".json");

    public static Cabinet LoadCabinet(string project, string name)
    {
        try
        {
            string p = CabinetPath(project, name);
            if (File.Exists(p))
            {
                var c = JsonSerializer.Deserialize<Cabinet>(File.ReadAllText(p), Opt);
                if (c != null) { c.Project = project; c.Name = name; return c; }
            }
        }
        catch { }
        return new Cabinet { Project = project, Name = name };
    }

    public static void SaveCabinet(Cabinet c)
    {
        string p = CabinetPath(c.Project, c.Name);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, JsonSerializer.Serialize(c, Opt));
    }

    public static void DeleteCabinet(string project, string name)
    {
        try { string p = CabinetPath(project, name); if (File.Exists(p)) File.Delete(p); } catch { }
    }

    private static List<Preset> DefaultPresets() => new()
    {
        new Preset { Name = "大标签", WidthMm = 50, HeightMm = 14, FontPt = 13 },
        new Preset { Name = "小标签", WidthMm = 31.5, HeightMm = 14, FontPt = 13 },
        new Preset { Name = "标识框", WidthMm = 39, HeightMm = 8, FontPt = 13 },
    };
}
