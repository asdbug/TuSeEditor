using System.IO;
using Newtonsoft.Json;

namespace TuSeEditor.App.Models;

/// <summary>工程文档:步骤树 + 全局设置,保存为 .tsproj(JSON)</summary>
public class ProjectDoc
{
    /// <summary>整个脚本的循环次数,0 表示无限循环</summary>
    public int ScriptLoopCount { get; set; } = 1;

    public bool ScanCodeMode { get; set; } = true;

    public List<Step> Steps { get; set; } = new();
}

/// <summary>编辑器全局设置(持久化到 %APPDATA%)</summary>
public class AppSettings
{
    /// <summary>auto / dxgi / gdi</summary>
    public string CaptureEngine { get; set; } = "auto";

    public string StartHotkey { get; set; } = "F9";
    public string StopHotkey { get; set; } = "F10";

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TuSeEditor", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch { }
    }
}

/// <summary>工程文件读写</summary>
public static class ProjectService
{
    public static string TemplatesDir(string projectPath) =>
        Path.Combine(Path.GetDirectoryName(projectPath) ?? ".", "templates");

    public static void Save(ProjectDoc doc, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonConvert.SerializeObject(doc, Formatting.Indented));
    }

    public static ProjectDoc Load(string path)
    {
        var doc = JsonConvert.DeserializeObject<ProjectDoc>(File.ReadAllText(path)) ?? new ProjectDoc();
        if (doc.Steps == null) doc.Steps = new List<Step>();
        return doc;
    }

    /// <summary>模板图保存目录,确保存在</summary>
    public static string EnsureTemplatesDir(string projectPath)
    {
        var dir = TemplatesDir(projectPath);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
