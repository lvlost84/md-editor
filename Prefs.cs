using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// View preferences saved next to settings.json (%AppData%\MdEditor\view.json): always-on-top, view mode, editor font size.
// Separate from Settings (Theme.cs) on purpose so the two writers never overwrite each other.
class Prefs
{
    public bool Pin;
    public int View = 1;            // 0 = editor only, 1 = split, 2 = preview only
    public double FontSize = 14.5;

    // 옵션 창에서 바꾸는 항목 (view.json에 같이 저장, 없는 필드는 기본값)
    public bool ShowLineNumbers = true, WordWrap = true, HighlightLine = true, Highlight = true;
    public int IndentSize = 2;      // 2~8
    public int AutosaveSec = 20;    // 5~600
    public string FontName = "";    // "" = 기본(Cascadia Mono, Consolas)

    public const double DefaultFont = 14.5, MinFont = 8, MaxFont = 40;
    public const int MinIndent = 2, MaxIndent = 8, MinAutosave = 5, MaxAutosave = 600;

    // 편집·저장 옵션만 기본값으로 (보기 모드/항상 위/글자 크기는 그대로)
    public void ResetOptions()
    {
        ShowLineNumbers = true; WordWrap = true; HighlightLine = true; Highlight = true;
        IndentSize = 2; AutosaveSec = 20; FontName = "";
    }

    public static string PathName { get { return Path.Combine(Log.Dir, "view.json"); } }

    public static Prefs Load()
    {
        var p = new Prefs();
        try
        {
            if (File.Exists(PathName))
            {
                var s = new JavaScriptSerializer().Deserialize<Prefs>(File.ReadAllText(PathName, Encoding.UTF8));
                if (s != null) p = s;
            }
        }
        catch (Exception ex) { Log.Error("prefs load", ex); }
        if (p.View < 0 || p.View > 2) p.View = 1;
        if (!(p.FontSize >= MinFont && p.FontSize <= MaxFont)) p.FontSize = DefaultFont;
        if (p.IndentSize < MinIndent || p.IndentSize > MaxIndent) p.IndentSize = 2;
        if (p.AutosaveSec < MinAutosave || p.AutosaveSec > MaxAutosave) p.AutosaveSec = 20;
        if (p.FontName == null || p.FontName.Length > 64 || p.FontName.IndexOfAny(new[] { ',', '<', '>', '"', '\'' }) >= 0) p.FontName = "";
        return p;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Log.Dir);
            string tmp = PathName + ".tmp";
            File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
            if (File.Exists(PathName)) File.Replace(tmp, PathName, null); else File.Move(tmp, PathName);
        }
        catch (Exception ex) { Log.Error("prefs save", ex); }
    }
}
