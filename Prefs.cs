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

    public const double DefaultFont = 14.5, MinFont = 8, MaxFont = 40;

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
