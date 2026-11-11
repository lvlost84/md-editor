using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

// 선 아이콘 모음 (Lucide/Feather 계열 24x24 경로, ISC/MIT). 글꼴 의존 없이 항상 같은 모양으로 그려지고 테마 색을 따른다.
static class Icons
{
    static readonly Dictionary<string, string> D = new Dictionary<string, string>
    {
        { "new",      "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M12 18v-6 M9 15h6" },
        { "open",     "M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" },
        { "save",     "M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z M17 21v-8H7v8 M7 3v5h8" },
        { "saveas",   "M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v4 M17 21v-3 M7 21v-8h5 M7 3v5h8 M18 14l3 3-6 6h-3v-3z" },
        { "print",    "M6 9V2h12v7 M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2 M6 14h12v8H6z" },
        { "export",   "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3" },
        { "undo",     "M3 7v6h6 M21 17a9 9 0 0 0-9-9 9 9 0 0 0-6 2.3L3 13" },
        { "redo",     "M21 7v6h-6 M3 17a9 9 0 0 1 9-9 9 9 0 0 1 6 2.3l3 2.7" },
        { "bold",     "M6 4h8a4 4 0 0 1 0 8H6z M6 12h9a4 4 0 0 1 0 8H6z" },
        { "italic",   "M19 4h-9 M14 20H5 M15 4L9 20" },
        { "strike",   "M16 4H9a3 3 0 0 0-2.83 4 M14 12a4 4 0 0 1 0 8H6 M4 12h16" },
        { "heading",  "M6 12h12 M6 4v16 M18 4v16" },
        { "ul",       "M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01" },
        { "ol",       "M10 6h11 M10 12h11 M10 18h11 M4 6h1v4 M4 10h2 M6 18H4c0-1 2-2 2-3s-1-1.5-2-1" },
        { "quote",    "M3 21c3 0 7-1 7-8V5c0-1.25-.756-2.017-2-2H4c-1.25 0-2 .75-2 1.972V11c0 1.25.75 2 2 2 1 0 1 0 1 1v1c0 1-1 2-2 2s-1 .008-1 1.031V20c0 1 0 1 1 1z M15 21c3 0 7-1 7-8V5c0-1.25-.757-2.017-2-2h-4c-1.25 0-2 .75-2 1.972V11c0 1.25.75 2 2 2h.75c0 2.25.25 4-2.75 4v3c0 1 0 1 1 1z" },
        { "code",     "M16 18l6-6-6-6 M8 6l-6 6 6 6" },
        { "codeblock","M3 3h18v18H3z M10 9l-3 3 3 3 M14 9l3 3-3 3" },
        { "link",     "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71 M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" },
        { "table",    "M3 3h18v18H3z M3 9h18 M3 15h18 M9 3v18 M15 3v18" },
        { "rowadd",   "M3 3h18v10H3z M3 8h18 M12 3v10 M12 16v6 M9 19h6" },
        { "coladd",   "M3 3h10v18H3z M8 3v18 M3 12h10 M16 12h6 M19 9v6" },
        { "footnote", "M12 3v14 M5.5 6.5l13 7 M18.5 6.5l-13 7 M4 21h16" },
        { "hr",       "M4 12h16 M8 6l4-3 4 3 M8 18l4 3 4-3" },
        { "search",   "M11 3a8 8 0 1 0 0 16 8 8 0 0 0 0-16z M21 21l-4.35-4.35" },
        { "replace",  "M3 7h13l-3-3 M21 17H8l3 3 M3 12a9 9 0 0 1 3-6.7" },
        { "view",     "M3 4h18v16H3z M12 4v16" },
        { "moon",     "M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" },
        { "sun",      "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M12 2v2 M12 20v2 M4.93 4.93l1.41 1.41 M17.66 17.66l1.41 1.41 M2 12h2 M20 12h2 M4.93 19.07l1.41-1.41 M17.66 6.34l1.41-1.41" },
        { "pin",      "M12 17v5 M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 17 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z" },
        { "help",     "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3 M12 17h.01" },
        { "settings", "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z" },
        { "more",     "M12 12h.01 M19 12h.01 M5 12h.01" },
    };

    public static bool Has(string name) { return D.ContainsKey(name); }
    public static IEnumerable<string> Names { get { return D.Keys; } }

    // 24x24 선 아이콘. 색은 'IconBrush' 리소스(없으면 InkSoftBrush)를 따라가므로 테마가 바뀌면 같이 바뀐다.
    public static FrameworkElement Make(string name, double size = 18, double stroke = 1.8, string brushKey = "IconBrush")
    {
        var p = new Path
        {
            Data = Geometry.Parse(D[name]),
            StrokeThickness = stroke,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent,
            Stretch = Stretch.None,
        };
        p.SetResourceReference(Shape.StrokeProperty, brushKey);
        var host = new System.Windows.Controls.Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Child = new System.Windows.Controls.Canvas { Width = 24, Height = 24, Children = { p } } };
        host.SnapsToDevicePixels = false;
        return host;
    }
}
