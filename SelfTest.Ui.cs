// UI-level checks via MainWindow.TestHooks (window parked at Left=-3000, rendered with RenderTargetBitmap only).
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

static partial class SelfTest
{
    static void Pump(int ms)
    {
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < end)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            System.Threading.Thread.Sleep(10);
        }
    }

    static void UiTests(string work)
    {
        MainWindow w = Program.CreateForTest();
        w.Left = -3000; w.Top = 0; w.ShowInTaskbar = false;
        w.Show();
        Pump(600);

        // --- initial state
        Check("ui initial text is sample, clean", w.GetText().Contains("새 문서") && !w.IsDirty());
        Check("ui initial file name box", w.GetFileNameBox() == "untitled.md" && w.CurrentPath == null);
        Check("ui first preview rendered", w.PreviewHtml().Contains("<table>") && w.PreviewHtml().Contains("footnotes"), w.PreviewHtml().Length.ToString());

        // --- toolbar inventory
        var tb = (IList)typeof(MainWindow).GetField("toolbar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(w);
        string[] labels = { "H1", "H2", "B", "I", "• 목록", "1. 목록", "\" 인용", "</>", "코드블록", "링크", "표 만들기", "행 추가", "열 추가", "각주" };
        Check("ui toolbar has exactly the expected 14 buttons", tb.Count == labels.Length, tb.Count.ToString());
        foreach (var l in labels)
        {
            bool found = false;
            foreach (object kv in tb) if ((string)kv.GetType().GetProperty("Key").GetValue(kv) == l) found = true;
            Check("ui toolbar label exists: " + l, found);
        }

        // --- toolbar click -> text change
        Func<string, string, int, int, string> click = (label, text, cs, cl) =>
        {
            w.SetText(text); w.SetCaret(cs, cl);
            bool ok = w.ClickToolbar(label);
            return ok ? w.GetText() : "<<no button>>";
        };
        string o;
        o = click("H1", "hello\nworld", 8, 0); Check("tb H1", o == "hello\n# world", o);
        o = click("H2", "hello\nworld", 1, 0); Check("tb H2", o == "## hello\nworld", o);
        o = click("B", "abc", 0, 3); Check("tb B selection", o == "**abc**", o);
        o = click("B", "abc", 3, 0); Check("tb B no selection placeholder", o == "abc**텍스트**", o);
        o = click("I", "abc", 1, 1); Check("tb I", o == "a*b*c", o);
        o = click("• 목록", "abc\ndef", 5, 0); Check("tb bullet", o == "abc\n- def", o);
        o = click("1. 목록", "abc", 0, 0); Check("tb numbered", o == "1. abc", o);
        o = click("\" 인용", "abc", 2, 0); Check("tb quote", o == "> abc", o);
        o = click("</>", "abc", 0, 3); Check("tb inline code", o == "`abc`", o);
        o = click("코드블록", "abc", 3, 0); Check("tb codeblock", o.Contains("```") && o.Contains("코드 입력") && o.StartsWith("abc"), o);
        o = click("링크", "abc", 0, 3); Check("tb link", o == "[abc](https://)", o);
        o = click("각주", "abc", 3, 0); Check("tb footnote", o == "abc[^1]\n\n[^1]: ", o);
        MainWindow.SetTableSizeForTest(2, 3);
        o = click("표 만들기", "", 0, 0);
        MainWindow.SetTableSizeForTest(0, 0);
        Check("tb table (modal bypassed) inserts 2x3", Count(Md.ToHtml(o), "<th[ >]") == 3 && Count(Md.ToHtml(o), "<tr>") == 3, o);
        string t3 = "| a | b |\n| --- | --- |\n| 1 | 2 |";
        o = click("행 추가", t3, 3, 0); Check("tb add row", o == t3 + "\n|  |  |", o);
        o = click("열 추가", t3, 3, 0);
        Check("tb add col", o.Split('\n').All(l => l.Count(ch => ch == '|') == 4) && Count(Md.ToHtml(o), "<th[ >]") == 3, o);
        string t3c = t3.Replace("\n", "\r\n");
        o = click("열 추가", t3c, 3, 0);
        Check("tb add col on CRLF editor text keeps 3 lines", o.Replace("\r\n", "\n").Split('\n').Length == 3 && o.Replace("\r\n", "\n").Split('\n').All(l => l.Count(ch => ch == '|') == 4), o);
        Check("tb unknown label returns false", !w.ClickToolbar("없는버튼"));
        // selection stays on the inserted text after B
        w.SetText("xyz"); w.SetCaret(0, 3); w.ClickToolbar("B");
        Check("tb keeps selection on wrapped text", w.GetSelection() == "xyz", w.GetSelection());
        w.SetText("a"); w.SetCaret(1, 0); w.ClickToolbar("B"); w.ClickToolbar("B");
        Check("tb B twice nests markers (no crash)", w.GetText().Contains("텍스트"), w.GetText());

        // --- dirty flag
        string p1 = Path.Combine(work, "한글 문서.md");
        w.SetText("한글 테스트 ✓ 가나다\n둘째 줄");
        Check("ui edit -> dirty", w.IsDirty());
        Check("ui dirty shows ● in footer and title", w.GetStatusText().Contains("●") && w.Title.Contains("●"), w.GetStatusText() + " / " + w.Title);
        bool saved = w.SaveForTest(p1);
        Check("ui save ok", saved && File.Exists(p1));
        Check("ui save -> dirty cleared", !w.IsDirty());
        Check("ui save clears ● in footer and title", !w.GetStatusText().Contains("●") && !w.Title.Contains("●"), w.GetStatusText() + " / " + w.Title);
        Check("ui save sets path + file name box", w.CurrentPath == p1 && w.GetFileNameBox() == "한글 문서.md");
        Check("ui save flashes status", w.GetStatusText().Contains("저장됨"), w.GetStatusText());
        Pump(1700);
        Check("ui status flash disappears", !w.GetStatusText().Contains("저장됨"), w.GetStatusText());
        w.SetText(w.GetText() + "!");
        Check("ui edit after save -> dirty again", w.IsDirty());
        w.SetText("한글 테스트 ✓ 가나다\n둘째 줄");
        Check("ui edit back to saved text -> clean", !w.IsDirty());

        // --- UTF-8 no BOM, LF only
        byte[] bytes = File.ReadAllBytes(p1);
        Check("save: no UTF-8 BOM", !(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF));
        Check("save: UTF-8 content exact", Encoding.UTF8.GetString(bytes) == "한글 테스트 ✓ 가나다\n둘째 줄", Encoding.UTF8.GetString(bytes));
        string p2 = Path.Combine(work, "crlf.md");
        w.SetText("a\r\nb\r\nc");
        w.SaveForTest(p2);
        Check("save: CRLF in editor written as LF", !File.ReadAllBytes(p2).Contains((byte)13), BitConverter.ToString(File.ReadAllBytes(p2)));
        // open roundtrip
        w.SetText("changed");
        Check("open ok", w.OpenFileForTest(p1));
        Check("open: text roundtrip", w.GetText() == "한글 테스트 ✓ 가나다\n둘째 줄", w.GetText());
        Check("open: clean + path + filename", !w.IsDirty() && w.CurrentPath == p1 && w.GetFileNameBox() == "한글 문서.md");
        string p3 = Path.Combine(work, "bom.md");
        File.WriteAllBytes(p3, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("BOM 파일\r\n둘째")).ToArray());
        w.OpenFileForTest(p3);
        Check("open: BOM stripped from text", w.GetText().Length > 0 && w.GetText()[0] == 'B', w.GetText().Length > 0 ? ((int)w.GetText()[0]).ToString() : "empty");
        Check("open: BOM file is clean", !w.IsDirty());

        // --- filename box vs path independence
        w.OpenFileForTest(p1);
        w.SetFileNameBox("renamed.md");
        Check("filename edit does not change path", w.CurrentPath == p1);
        Check("filename edit does not mark dirty", !w.IsDirty());
        Check("filename edit shows in footer + title", w.GetStatusText().Contains("DOC renamed.md") && w.Title.Contains("renamed.md"), w.GetStatusText() + " / " + w.Title);
        w.SaveForTest(p2);
        Check("save as updates filename box from path", w.GetFileNameBox() == "crlf.md" && w.CurrentPath == p2);

        // --- footer count + debounce
        w.SetText("안녕 하세요 abc");
        w.ForcePreviewRefresh();
        Check("footer count text", w.GetStatusText().Contains("10자 · 3단어"), w.GetStatusText());
        w.SetText("");
        w.ForcePreviewRefresh();
        Check("footer count empty", w.GetStatusText().Contains("0자 · 0단어"), w.GetStatusText());
        w.SetText("| ZZQ9 | b |\n| --- | --- |\n| 1 | 2 |");
        Check("preview is debounced (not rendered synchronously on edit)", !w.PreviewHtml().Contains("ZZQ9"));
        Pump(900);
        Check("preview debounce fires and renders table", w.PreviewHtml().Contains("ZZQ9") && w.PreviewHtml().Contains("<table>") && w.PreviewHtml().Contains("<th>"), w.PreviewHtml().Length.ToString());
        Check("footer updated after debounce", Regex.IsMatch(w.GetStatusText(), @"\d+자 · \d+단어") && !w.GetStatusText().Contains("0자 · 0단어"), w.GetStatusText());
        w.SetText("| QQ7 | b |\n| --- | --- |\n| 1 | 2 |");
        w.ForcePreviewRefresh();
        Check("ForcePreviewRefresh renders table immediately", w.PreviewHtml().Contains("QQ7") && w.PreviewHtml().Contains("<table>"));
        w.SetText("x"); w.SetText("xy"); w.SetText("xyz");
        w.SetText("| BURST | b |\n| --- | --- |");
        Pump(900);
        Check("rapid edits end with last text in preview", w.PreviewHtml().Contains("BURST"));

        // --- dark toggle
        w.SetText("# Dark"); w.ForcePreviewRefresh();
        string light = w.PreviewHtml();
        w.SetDark(true);
        string dark = w.PreviewHtml();
        Check("dark toggle changes PreviewHtml", dark != light && dark.Contains("#1e2024") && !light.Contains("#1e2024"), "");
        Check("dark toggle keeps content", dark.Contains("<h1>Dark</h1>"));
        Check("dark: Theme.IsDark and brush swapped", Theme.IsDark && BrushHex("PaperBrush") == "#FF123A5E" && BrushHex("InkBrush") == "#FFEAF3FB", BrushHex("PaperBrush"));
        Check("dark: settings.json written inside sandbox with Dark true", File.Exists(Settings.PathName) && Settings.PathName.StartsWith(Path.GetFullPath(Path.Combine(work, "..", "profile"))) && File.ReadAllText(Settings.PathName).Replace(" ", "").ToLower().Contains("\"dark\":true"), File.Exists(Settings.PathName) ? File.ReadAllText(Settings.PathName) : "missing");
        Check("dark: settings reload gives Dark", Settings.Load().Dark);
        Check("dark: label says light-mode", FindText(w, "라이트모드"));
        string pngDark = Path.Combine(work, "dark.png");
        w.RenderToPng(pngDark);
        w.SetDark(false);
        Check("light: Theme + brush back", !Theme.IsDark && BrushHex("PaperBrush") == "#FFFFFFFF", BrushHex("PaperBrush"));
        Check("light: PreviewHtml restored", w.PreviewHtml() == light, "");
        Check("light: settings Dark false", !Settings.Load().Dark);
        Check("light: label says dark-mode", FindText(w, "다크모드"));
        string pngLight = Path.Combine(work, "light.png");
        w.RenderToPng(pngLight);

        // --- RenderToPng
        double ld = Lum(pngLight), dd = Lum(pngDark);
        Check("png light non-empty + decodable", new FileInfo(pngLight).Length > 2000 && ld >= 0);
        Check("png dark non-empty + decodable", new FileInfo(pngDark).Length > 2000 && dd >= 0);
        Check("png dark is darker than light (mean luma " + dd.ToString("0") + " < " + ld.ToString("0") + ")", dd >= 0 && ld >= 0 && dd < ld - 30);
        Check("png has many distinct colours", Distinct(pngLight) > 8 && Distinct(pngDark) > 8);

        // --- close without dialog when clean
        w.SetText("final"); w.SaveForTest(Path.Combine(work, "final.md"));
        bool closed = false;
        w.Closed += (s, e) => closed = true;
        w.Close();
        Pump(200);
        Check("ui close when clean needs no dialog and closes", closed);
    }

    static string BrushHex(string key)
    {
        var b = Application.Current.Resources[key] as SolidColorBrush;
        return b == null ? "null" : b.Color.ToString();
    }

    static bool FindText(DependencyObject root, string text)
    {
        var tb = root as System.Windows.Controls.TextBlock;
        if (tb != null && tb.Text == text) return true;
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++) if (FindText(VisualTreeHelper.GetChild(root, i), text)) return true;
        return false;
    }

    static byte[] Pixels(string png, out int w, out int h)
    {
        var dec = new PngBitmapDecoder(new Uri(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var fr = new FormatConvertedBitmap(dec.Frames[0], PixelFormats.Bgra32, null, 0);
        w = fr.PixelWidth; h = fr.PixelHeight;
        var px = new byte[w * h * 4];
        fr.CopyPixels(px, w * 4, 0);
        return px;
    }
    static double Lum(string png)
    {
        try
        {
            int w, h; var px = Pixels(png, out w, out h);
            double sum = 0; long n = 0;
            for (int i = 0; i + 3 < px.Length; i += 4 * 7) { sum += 0.299 * px[i + 2] + 0.587 * px[i + 1] + 0.114 * px[i]; n++; }
            return n == 0 ? -1 : sum / n;
        }
        catch { return -1; }
    }
    static int Distinct(string png)
    {
        try
        {
            int w, h; var px = Pixels(png, out w, out h);
            var set = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i + 3 < px.Length && set.Count < 50; i += 4) set.Add(px[i] | px[i + 1] << 8 | px[i + 2] << 16);
            return set.Count;
        }
        catch { return 0; }
    }
}
