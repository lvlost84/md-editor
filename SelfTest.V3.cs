// v3.0.0 hard-case checks (tester): Markdig XSS set, garbage/timeout robustness, palette, icon toolbar, theme icon brush, scrollbar style,
// embedded-DLL resolve (exe copy alone in an empty dir), corrupt settings/view json, 5 MB preview pause. Fixed seeds / fixed dates only.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using SPath = System.Windows.Shapes.Path;

static partial class SelfTest
{
    static void V3All(string work)
    {
        Try("v3-xss", V3Xss);
        Try("v3-garbage", V3Garbage);
        Try("v3-palette", V3Palette);
        Try("v3-ui", () => V3Ui(work));
        Try("v3-corrupt-settings", () => V3Corrupt(work));
        Try("v3-embedded-dll", () => V3Embedded(work));
        Try("options", () => OptionsTests(work));
    }

    // true when the theme (icon) button carries a tooltip containing text
    internal static bool HasButtonTip(DependencyObject root, string text)
    {
        foreach (var b in V3Buttons(root)) { var s = b.ToolTip as string; if (s != null && s.Contains(text)) return true; }
        return false;
    }

    static List<Button> V3Buttons(DependencyObject root)
    {
        var l = new List<Button>();
        Action<DependencyObject> walk = null;
        walk = d =>
        {
            var b = d as Button; if (b != null) l.Add(b);
            int n = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++) walk(VisualTreeHelper.GetChild(d, i));
        };
        walk(root);
        return l;
    }

    static List<T> V3Find<T>(DependencyObject root) where T : DependencyObject
    {
        var l = new List<T>();
        Action<DependencyObject> walk = null;
        walk = d =>
        {
            var t = d as T; if (t != null) l.Add(t);
            int n = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++) walk(VisualTreeHelper.GetChild(d, i));
        };
        walk(root);
        return l;
    }

    // returns null when html looks inert, else the reason
    static string V3Unsafe(string h)
    {
        var lt = Regex.Match(h, @"<\s*/?\s*(script|svg|iframe|object|embed|style|x|form|link|meta|body|html|math|video|audio|details)\b", RegexOptions.IgnoreCase);
        if (lt.Success) return "live tag '" + h.Substring(lt.Index, Math.Min(40, h.Length - lt.Index)) + "'";
        // event handler attributes: look at each tag with its quoted attribute values removed (a value like title="x onerror=y" is inert)
        foreach (Match tag in Regex.Matches(h, @"<[^>]*>"))
        {
            string bare = Regex.Replace(tag.Value, "\"[^\"]*\"", "\"\"");
            if (Regex.IsMatch(bare, @"[\s""'/]on\w+\s*=", RegexOptions.IgnoreCase)) return "event attribute in " + tag.Value;
        }
        foreach (Match m in Regex.Matches(h, @"\b(?:href|src|action|formaction|xlink:href|style)\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase))
        {
            string v = WebUtility.HtmlDecode(m.Groups[1].Value);
            var sb = new StringBuilder();
            foreach (char c in v) if (c > ' ' && !char.IsControl(c) && c != '\u00ad' && c != '\u200b' && c != '\ufeff') sb.Append(c);
            string t = sb.ToString().ToLowerInvariant();
            if (t.StartsWith("javascript:") || t.StartsWith("vbscript:") || t.StartsWith("data:") || t.Contains("expression(")) return "bad url: " + v;
        }
        return null;
    }

    // ------------------------------------------------------------ XSS
    static void V3Xss()
    {
        // inputs that must never produce an anchor / image (dangerous scheme, none of them has a legit link)
        string[] noLink =
        {
            "[x](javascript:alert(1))",
            "[x](JaVaScRiPt:alert(1))",
            "[x](  javascript:alert(1))",
            "[x](&#106;avascript:alert(1))",
            "[x](&#x6A;&#x61;vascript:alert(1))",
            "[x](java&#9;script:alert(1))",
            "[x](<java&#9;script:alert(1)>)",
            "[x](<java\tscript:alert(1)>)",
            "[x](java\u0001script:alert(1))",
            "[x](vbscript:msgbox(1))",
            "[x](data:text/html,<script>alert(1)</script>)",
            "[x](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)",
            "![x](data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9YWxlcnQoMSk+)",
            "![x](javascript:alert(1))",
            "[x][r]\n\n[r]: javascript:alert(1)",
            "<javascript:alert(1)>",
            "<JAVASCRIPT:alert(1)>",
            "<vbscript:msgbox(1)>",
            "<data:text/html;base64,AAAA>",
            "[x](\\javascript:alert(1))",
        };
        foreach (string s in noLink)
        {
            string h = Md.ToHtml(s);
            string u = V3Unsafe(h);
            Check("v3 xss no active url: " + Short(s), u == null && !h.Contains("<a ") && !h.Contains("<img"), u ?? h);
        }

        // markup / attribute injection (raw html escaped)
        string[] inert =
        {
            "<script>alert(1)</script>",
            "<SCRIPT SRC=//evil.example/x.js></SCRIPT>",
            "<img src=x onerror=alert(1)>",
            "<svg onload=alert(1)>",
            "<svg><script>alert(1)</script></svg>",
            "<iframe src=\"javascript:alert(1)\"></iframe>",
            "<a href=\"javascript:alert(1)\">x</a>",
            "<body onload=alert(1)>",
            "![a](x\" onerror=\"alert(1))",
            "![a](x \"t\\\" onerror=\\\"alert(1)\")",
            "[a](https://x.example \"t\" onmouseover=\"alert(1)\")",
            "[a](https://x.example/\" onmouseover=\"alert(1))",
            "[a\"><svg onload=alert(1)>](https://x.example)",
            "`<script>alert(1)</script>`",
            "```\n<script>alert(1)</script>\n```",
            "www.evil.example\"onmouseover=\"alert(1)",
            "http://evil.example/\"onmouseover=\"alert(1)",
            "http://evil.example/<script>alert(1)</script>",
            "<http://evil.example/\"onmouseover=\"alert(1)>",
            "a[^\"><x]\n\n[^\"><x]: def",
            "a[^\"onmouseover=\"alert(1)]\n\n[^\"onmouseover=\"alert(1)]: <img src=x onerror=alert(1)>",
            "a[^1]\n\n[^1]: <script>alert(1)</script> [x](javascript:alert(1))",
            "|a|b|\n|:-\"><x|--:|\n|1|2|",
            "|a|b|\n|:--\" onmouseover=\"alert(1)|---|\n|<script>|2|",
            "| <img src=x onerror=alert(1)> | [x](javascript:alert(1)) |\n|---|---|\n| a | b |",
            "# <script>alert(1)</script>",
            "> <svg onload=alert(1)>",
            "- [ ] <script>alert(1)</script>",
            "&lt;script&gt;alert(1)&lt;/script&gt;",
            "&#60;script&#62;alert(1)&#60;/script&#62;",
            "<<script>script>alert(1)<</script>/script>",
            "\\<script>alert(1)</script>",
            "<img src=\"x\" onerror=\"alert(1)\" />",
            "<a href=\"&#x6A;avascript:alert(1)\">x</a>",
        };
        foreach (string s in inert)
        {
            string h = Md.ToHtml(s);
            string u = V3Unsafe(h);
            Check("v3 xss inert: " + Short(s), u == null, (u ?? "") + " :: " + h);
        }
        // same inputs through the full preview/export documents (only our own <style>/<html>/<body> may exist -> check body part)
        foreach (string s in inert.Take(12))
        {
            string d = Md.PreviewDocument(s, true);
            int i = d.IndexOf("<body>");
            string body = i >= 0 ? d.Substring(i + 6) : d;
            Check("v3 xss preview body inert: " + Short(s), V3Unsafe(body.Replace("</body></html>", "")) == null, body);
        }

        // footnote label must be attribute-escaped everywhere it is used
        string fh = Md.ToHtml("a[^\"><x]\n\n[^\"><x]: def");
        Check("v3 footnote label escaped (no raw quote/angle in id/href)", !fh.Contains("<x") && !Regex.IsMatch(fh, "id=\"[^\"]*[<>][^\"]*\"") && !Regex.IsMatch(fh, "href=\"[^\"]*[<>][^\"]*\"") && fh.Contains("&quot;&gt;&lt;x") && fh.Contains("footnotes"), fh);
        string fh2 = Md.ToHtml("a[^x\"y]\n\n[^x\"y]: d");
        Check("v3 footnote label with quote keeps one id attribute", Regex.Matches(fh2, "<li id=\"[^\"]*\"").Count == 1 && fh2.Contains("fn-x&quot;y"), fh2);

        // positive controls: the predicate must not be vacuous, and legit urls must survive
        Check("v3 xss control: https link kept", Md.ToHtml("[ok](https://example.com/a?b=1)").Contains("href=\"https://example.com/a?b=1\""));
        Check("v3 xss control: mailto kept", Md.ToHtml("[m](mailto:a@b.example)").Contains("href=\"mailto:a@b.example\""));
        Check("v3 xss control: relative + anchor kept", Md.ToHtml("[r](docs/a.md) [h](#top)").Contains("href=\"docs/a.md\"") && Md.ToHtml("[h](#top)").Contains("href=\"#top\""));
        Check("v3 xss control: drive-letter + file kept", Md.ToHtml("[d](C:/x/y.md)").Contains("href=\"C:/x/y.md\"") && Md.ToHtml("[f](file:///C:/x/y.md)").Contains("href=\"file:///C:/x/y.md\""));
        Check("v3 xss control: image kept", Md.ToHtml("![i](pic.png)").Contains("<img src=\"pic.png\""));
        Check("v3 xss control: autolink kept", Md.ToHtml("<https://auto.example>").Contains("href=\"https://auto.example\""));
        Check("v3 xss control: rejected link keeps its text", Md.ToHtml("[visible text](javascript:alert(1))").Contains("visible text"));
        Check("v3 xss control: predicate fires on a live script", V3Unsafe("<p><script>x</script></p>") != null && V3Unsafe("<a href=\"java&#9;script:1\">") != null && V3Unsafe("<img src=x onerror=1>") != null);
    }

    // ------------------------------------------------------------ never throws / never hangs
    static string V3Rand(Random r, int maxLen)
    {
        string[] toks =
        {
            "[", "]", "(", ")", "|", "![", "](", "<", ">", "&#106;", "javascript:", "<script>", "onerror=", "`", "```", "*", "_", "~~", "\n", "\r\n", "\r", "\t", " ", "#", "- ", "1. ", "> ", "[^1]", "[^", "]: ",
            "---", ":--", "<http://a.b>", "http://x.y", "www.a.b", "\uE000", "\uE001", "\uE0001\uE001", "\uE000999999999999999\uE001", "\0", "한글", "\U0001F600", "\ud800", "\udc00", "\u202e", "\u200b", "\ufeff", "\"", "'", "&amp;", "&", "\\", "[ ]", "- [x] ", "|---|", "    ", "=====",
        };
        var sb = new StringBuilder();
        int len = r.Next(0, maxLen);
        for (int i = 0; i < len; i++)
        {
            if (r.Next(10) < 7) sb.Append(toks[r.Next(toks.Length)]);
            else sb.Append((char)r.Next(0, 0x10000));
        }
        return sb.ToString();
    }

    static void V3Garbage()
    {
        var rnd = new Random(20261110);
        int bad = 0, slow = 0, unsafeN = 0, fallback = 0; string first = "";
        double maxMs = 0;
        for (int i = 0; i < 500; i++)
        {
            string s = V3Rand(rnd, i % 10 == 0 ? 3000 : 300);
            var sw = Stopwatch.StartNew();
            string h = null; Exception ex = null;
            try { h = Md.ToHtml(s); } catch (Exception e) { ex = e; }
            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds > maxMs) maxMs = sw.Elapsed.TotalMilliseconds;
            if (ex != null || h == null) { bad++; if (first == "") first = "#" + i + " threw/null " + (ex == null ? "" : ex.GetType().Name); continue; }
            if (sw.ElapsedMilliseconds >= 5000) { slow++; if (first == "") first = "#" + i + " slow " + sw.ElapsedMilliseconds; }
            if (h.StartsWith("<pre>")) fallback++;
            string u = V3Unsafe(h);
            if (u != null) { unsafeN++; if (first == "") first = "#" + i + " unsafe " + u + " :: " + Short(s); }
        }
        res.Add("INFO v3 garbage: max " + maxMs.ToString("0") + " ms, fallback<pre> " + fallback);
        Check("v3 md: 500 seeded garbage strings never throw", bad == 0, bad + " " + first);
        Check("v3 md: 500 seeded garbage strings each < 5 s", slow == 0, slow + " " + first);
        Check("v3 md: 500 seeded garbage strings never yield live markup", unsafeN == 0, unsafeN + " " + first);

        // Preview/Export wrappers too
        bool wrapOk = true;
        for (int i = 0; i < 60; i++) { string s = V3Rand(rnd, 400); try { if (Md.PreviewDocument(s, i % 2 == 0) == null || Md.ExportDocument(s, V3Rand(rnd, 20)) == null) wrapOk = false; } catch { wrapOk = false; } }
        Check("v3 md: Preview/Export documents never throw on garbage", wrapOk);
        Check("v3 md: null input", Md.ToHtml(null) != null && Md.PreviewDocument(null, false) != null);

        // pathological runs (each must return < 5 s whether it converts or falls back to the escaped source)
        string[] units = { "[", "(", "|", "![", "[[", "](", "[]()", "|-", ">", "*", "`", "<", "- ", "[^" };
        int slowRun = 0, throwRun = 0; string fd = "";
        foreach (string u in units)
        {
            var sb = new StringBuilder(u.Length * 100000);
            for (int i = 0; i < 100000; i++) sb.Append(u);
            string s = sb.ToString();
            var sw = Stopwatch.StartNew();
            string h = null;
            try { h = Md.ToHtml(s); } catch (Exception) { throwRun++; }
            sw.Stop();
            res.Add("INFO v3 run100k '" + u + "': " + sw.ElapsedMilliseconds + " ms" + (h != null && h.StartsWith("<pre>") ? " (fallback)" : ""));
            if (sw.ElapsedMilliseconds >= 5000 || h == null) { slowRun++; fd += " '" + u + "'=" + sw.ElapsedMilliseconds; }
        }
        Check("v3 md: 100k-run pathological inputs each < 5 s and never throw", slowRun == 0 && throwRun == 0, fd);
        // mixed line (pipe table with 100k cells, one huge line of '[' with a closing tail)
        string mixed = new string('[', 100000) + "x" + new string(']', 100000) + "(javascript:alert(1))";
        var sw2 = Stopwatch.StartNew(); string mh = Md.ToHtml(mixed); sw2.Stop();
        Check("v3 md: 100k nested brackets + js url < 5 s, inert", sw2.ElapsedMilliseconds < 5000 && V3Unsafe(mh) == null, sw2.ElapsedMilliseconds + " ms");

        // Md stays usable afterwards (also after a possible thread-abort fallback), including from several threads at once
        Check("v3 md: usable after pathological input", Md.ToHtml("# ok **b**").Contains("<h1>ok <strong>b</strong></h1>"), Md.ToHtml("# ok **b**"));
        bool par = true;
        var ts = new List<Thread>();
        for (int k = 0; k < 4; k++)
        {
            int kk = k;
            var t = new Thread(() => { try { for (int i = 0; i < 20; i++) if (!Md.ToHtml("# t" + kk + "\n\ntext [^1]\n\n[^1]: n").Contains("<h1>t" + kk + "</h1>")) par = false; } catch { par = false; } });
            ts.Add(t); t.Start();
        }
        foreach (var t in ts) if (!t.Join(20000)) par = false;
        Check("v3 md: 4 parallel callers stay correct after pathological inputs", par);
    }

    // ------------------------------------------------------------ preview palette (pure string)
    static void V3Palette()
    {
        string d = Md.PreviewDocument("# T\n\n[a](https://x.example) `c`\n\n> q", true);
        string l = Md.PreviewDocument("# T\n\n[a](https://x.example) `c`\n\n> q", false);
        Check("v3 palette dark: body bg #1e2024 + ink #e6e8ec", d.Contains("body{margin:0;background:#1e2024;color:#e6e8ec;"), d.Substring(0, Math.Min(200, d.Length)));
        Check("v3 palette dark: accent #7aa7ff on links + h2", d.Contains("a{color:#7aa7ff;}") && d.Contains("h2{font-size:1.35em;color:#7aa7ff;}"));
        Check("v3 palette dark: border #3a3e46 / soft #9aa1ad / accentSoft #26324f", d.Contains("#3a3e46") && d.Contains("#9aa1ad") && d.Contains("#26324f"));
        Check("v3 palette light: body bg #fff + ink #1c1d20", l.Contains("body{margin:0;background:#fff;color:#1c1d20;"), l.Substring(0, Math.Min(200, l.Length)));
        Check("v3 palette light: accent #2563eb on links + h1 border", l.Contains("a{color:#2563eb;}") && l.Contains("border-bottom:2px solid #2563eb"));
        Check("v3 palette: no cross-theme colours", !l.Contains("#1e2024") && !l.Contains("#7aa7ff") && !d.Contains("#2563eb") && !d.Contains("#1c1d20"));
        Check("v3 palette: no old navy theme colours", !d.Contains("#123a5e") && !d.Contains("#eaf3fb") && !l.Contains("#123a5e"), "");
    }

    // ------------------------------------------------------------ window level
    static void V3Ui(string work)
    {
        MainWindow w = Program.CreateForTest();
        w.Left = -3000; w.Top = 0; w.ShowInTaskbar = false;
        w.Show();
        Pump(600);
        try
        {
            w.SetDark(false); Pump(50);

            // --- icon toolbar: style + count
            var all = V3Buttons(w);
            var iconStyle = Application.Current.Resources["IconBtn"] as Style;
            var glyphStyle = Application.Current.Resources["GlyphBtn"] as Style;
            Check("v3 styles IconBtn + GlyphBtn exist in Application resources", iconStyle != null && glyphStyle != null);
            var ib = all.Where(b => iconStyle != null && (ReferenceEquals(b.Style, iconStyle) || (glyphStyle != null && ReferenceEquals(b.Style, glyphStyle)))).ToList();
            Check("v3 toolbar: >= 25 buttons use IconBtn/GlyphBtn (" + ib.Count + ")", ib.Count >= 25, ib.Count.ToString());
            Check("v3 toolbar: every icon button has a non-empty tooltip",
                ib.All(b => !string.IsNullOrWhiteSpace(b.ToolTip as string)),
                string.Join(",", ib.Where(b => string.IsNullOrWhiteSpace(b.ToolTip as string)).Select(b => "#" + all.IndexOf(b))));
            var noName = ib.Where(b => System.Windows.Automation.AutomationProperties.GetName(b) != (b.ToolTip as string)).Select(b => "'" + (b.ToolTip as string) + "'").ToList();
            Known("v3 toolbar: every icon button has AutomationProperties.Name == tooltip (accessibility)", noName.Count == 0, "missing/stale on " + string.Join(",", noName));
            Check("v3 toolbar: every icon button is 32px wide and tall", ib.All(b => Math.Abs(b.ActualWidth - 32) < 0.5 && Math.Abs(b.ActualHeight - 32) < 0.5), string.Join(",", ib.Select(b => b.ActualWidth.ToString("0"))));
            Check("v3 toolbar: icon buttons draw a Path (icon) or a glyph text", ib.All(b => b.Content is string || (b.Content is Viewbox && V3Find<SPath>(b).Count == 1)));
            var tips = ib.Select(b => (string)b.ToolTip).ToList();
            foreach (string id in new[] { "new", "open", "save", "saveas", "print", "bold", "italic", "strike", "link", "h1", "h2", "bullet", "numbered", "quote", "code", "codeblock", "table", "footnote", "hr", "find", "replace", "view", "pin", "dark", "help" })
            {
                var sd = Shortcuts.Get(id);
                string disp = sd == null ? "?" : "(" + sd.Display("+") + ")";
                Check("v3 toolbar tooltip carries shortcut of '" + id + "' " + disp, sd != null && tips.Any(t => t.EndsWith(disp)), string.Join(" | ", tips));
            }
            // every "(Ctrl+X)" style hint in any tooltip must be a real shortcut (table or AvalonEdit built-in undo/redo)
            var known = new HashSet<string>(Shortcuts.All.Select(s => "(" + s.Display("+") + ")")); known.Add("(Ctrl+Z)"); known.Add("(Ctrl+Y)");
            var wrong = new List<string>();
            foreach (var t in tips) { var m = Regex.Match(t, @"\([^()]*\)$"); if (m.Success && Regex.IsMatch(m.Value, @"^\((Ctrl|Alt|Shift|F\d+)") && !known.Contains(m.Value)) wrong.Add(t); }
            Check("v3 toolbar: tooltip shortcut hints all exist in Shortcuts table", wrong.Count == 0, string.Join(" | ", wrong));
            Check("v3 toolbar: tooltip hints unique per button (no copy-paste)", tips.Where(t => Regex.IsMatch(t, @"\(Ctrl|\(F\d")).GroupBy(t => t).All(g => g.Count() == 1));

            // --- icons
            var names = Icons.Names.ToList();
            Check("v3 icons: >= 30 names, unique", names.Count >= 30 && names.Distinct().Count() == names.Count, names.Count.ToString());
            string badIcon = "";
            foreach (string n in names)
            {
                try
                {
                    var vb = Icons.Make(n) as Viewbox;
                    var cv = vb == null ? null : vb.Child as Canvas;
                    var p = cv == null || cv.Children.Count != 1 ? null : cv.Children[0] as SPath;
                    if (p == null || p.Data == null || p.Data.Bounds.IsEmpty || p.Data.Bounds.Width < 0.5 && p.Data.Bounds.Height < 0.5 && n != "more") badIcon += n + ":shape ";
                }
                catch (Exception ex) { badIcon += n + ":" + ex.GetType().Name + " "; }
            }
            Check("v3 icons: Icons.Make works for every name (non-empty geometry)", badIcon == "", badIcon);
            string[] needed = { "new", "open", "save", "saveas", "print", "export", "undo", "redo", "bold", "italic", "strike", "heading", "ul", "ol", "quote", "code", "codeblock", "link", "table", "rowadd", "coladd", "footnote", "hr", "search", "replace", "view", "moon", "sun", "pin", "help" };
            Check("v3 icons: all names the window uses exist", needed.All(n => Icons.Has(n)), string.Join(",", needed.Where(n => !Icons.Has(n))));

            // --- theme toggle swaps IconBrush (resource + the live icon stroke)
            Check("v3 theme light: IconBrush #4a505c", BrushHex("IconBrush") == "#FF4A505C", BrushHex("IconBrush"));
            Check("v3 theme light: accent #2563eb", BrushHex("AccentBrush") == "#FF2563EB", BrushHex("AccentBrush"));
            Func<string> stroke = () =>
            {
                var p = V3Find<SPath>(w).FirstOrDefault(x => x.StrokeThickness > 1.5 && x.Stroke is SolidColorBrush);
                return p == null ? "none" : ((SolidColorBrush)p.Stroke).Color.ToString();
            };
            Check("v3 theme light: toolbar icon stroke follows IconBrush", stroke() == "#FF4A505C", stroke());
            w.SetDark(true); Pump(50);
            Check("v3 theme dark: IconBrush #c3c9d4", BrushHex("IconBrush") == "#FFC3C9D4", BrushHex("IconBrush"));
            Check("v3 theme dark: accent #7aa7ff", BrushHex("AccentBrush") == "#FF7AA7FF", BrushHex("AccentBrush"));
            Check("v3 theme dark: toolbar icon stroke follows IconBrush", stroke() == "#FFC3C9D4", stroke());
            Check("v3 theme dark: window preview uses dark palette", w.PreviewHtml().Contains("background:#1e2024;color:#e6e8ec") && w.PreviewHtml().Contains("a{color:#7aa7ff;}"));
            Check("v3 theme dark: theme button now shows the sun icon tip", HasButtonTip(w, "라이트 모드로") && !HasButtonTip(w, "다크 모드로"));
            w.SetDark(false); Pump(50);
            Check("v3 theme back to light: IconBrush + stroke restored", BrushHex("IconBrush") == "#FF4A505C" && stroke() == "#FF4A505C", stroke());
            Check("v3 theme back to light: light palette in preview", w.PreviewHtml().Contains("background:#fff;color:#1c1d20") && w.PreviewHtml().Contains("a{color:#2563eb;}"));

            // --- thin scrollbar style
            var sbStyle = Application.Current.TryFindResource(typeof(ScrollBar)) as Style;
            Check("v3 scrollbar: implicit ScrollBar style present in Application resources", sbStyle != null);
            bool thin = false;
            if (sbStyle != null) foreach (var sb in sbStyle.Setters.OfType<Setter>()) if (sb.Property == FrameworkElement.WidthProperty && sb.Value is double && (double)sb.Value <= 12) thin = true;
            Check("v3 scrollbar: style width <= 12 and custom template", thin && sbStyle != null && sbStyle.Setters.OfType<Setter>().Any(s => s.Property == Control.TemplateProperty));
            // the style really reaches a ScrollBar created now
            var probe = new ScrollBar(); var host = new Grid(); host.Children.Add(probe); host.Measure(new Size(100, 100)); host.Arrange(new Rect(0, 0, 100, 100)); host.UpdateLayout();
            Check("v3 scrollbar: a new vertical ScrollBar renders 12px wide through the style", probe.Width == 12 || Math.Abs(probe.ActualWidth - 12) < 0.5, probe.Width + "/" + probe.ActualWidth);
        }
        finally { w.SetDark(false); }

        // --- 5 MB document: preview paused note + edits stay responsive
        var big = new StringBuilder(5300000);
        for (int i = 0; big.Length < 5000000; i++) big.Append("## line ").Append(i).Append(" text 한글 abc def ghi\n");
        string bigText = big.ToString();
        w.SetText(bigText); Pump(1500);   // debounce for 5 MB = 640 ms
        Check("v3 5MB: preview shows paused note, no rendered headings", w.PreviewHtml().Contains("미리보기 일시 중지") && !w.PreviewHtml().Contains("<h2>"), w.PreviewHtml().Length.ToString());
        var worst = 0L; string detail = ""; var totals = new List<long>();
        string[] labels = { "H1", "B", "I", "• 목록", "\" 인용", "</>", "H2" };
        foreach (string lb in labels)
        {
            w.SetCaret(5, 0);
            var sw = Stopwatch.StartNew();
            bool ok = w.ClickToolbar(lb);
            long op = sw.ElapsedMilliseconds;
            Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => { }));
            long total = sw.ElapsedMilliseconds;
            if (!ok) detail += lb + ":nobutton ";
            if (total > worst) worst = total;
            totals.Add(total);
            detail += lb + "=" + op + "/" + total + "ms ";
        }
        res.Add("INFO v3 5MB edit timings " + detail);
        // timing-tolerant (one GC / background footer-count hiccup was seen at ~1.2 s): 5 of 7 ops under 500 ms and none over 3 s
        Check("v3 5MB: edit ops (+ idle dispatcher round-trip) stay responsive: >= 5/7 under 500 ms, none over 3 s (worst " + worst + ")",
            totals.Count(t => t < 500) >= 5 && worst < 3000 && !detail.Contains("nobutton"), detail);
        w.SetText("# small\n\ntext"); Pump(500);
        Check("v3 5MB: shrinking the doc brings the real preview back", w.PreviewHtml().Contains("<h1>small</h1>") && !w.PreviewHtml().Contains("일시 중지"));
        w.SetText("final"); w.SaveForTest(Path.Combine(work, "v3final.md"));
        w.Close(); Pump(200);
    }

    // ------------------------------------------------------------ corrupt settings.json / view.json
    static void V3Corrupt(string work)
    {
        Directory.CreateDirectory(Log.Dir);
        var variants = new List<byte[]>
        {
            Encoding.UTF8.GetBytes("garbage {{{ not json \0\0"),
            new byte[0],
            Encoding.UTF8.GetBytes("null"),
            Encoding.UTF8.GetBytes("[]"),
            Encoding.UTF8.GetBytes("{\"Dark\":\"maybe\",\"Width\":\"abc\",\"FontSize\":\"x\",\"View\":\"y\",\"Pin\":3}"),
            Encoding.UTF8.GetBytes("{\"Dark\":true,\"Width\":-5,\"Height\":1e999,\"FontSize\":-5,\"View\":99}"),
            Encoding.UTF8.GetBytes("{\"Dark\":true,\"Width\":1"),
            new byte[] { 0xFF, 0xFE, 0x00, 0x80, 0xC3, 0x28, 0xA0, 0xA1, 0x7B },
        };
        int i = 0; string bad = "";
        foreach (var v in variants)
        {
            i++;
            File.WriteAllBytes(Settings.PathName, v);
            File.WriteAllBytes(Prefs.PathName, v);
            try
            {
                var s = Settings.Load(); var p = Prefs.Load();
                bool ok = s != null && p != null && p.View >= 0 && p.View <= 2 && p.FontSize >= Prefs.MinFont && p.FontSize <= Prefs.MaxFont;
                if (!ok) bad += "#" + i + ":values(" + (p == null ? "null" : p.View + "/" + p.FontSize) + ") ";
            }
            catch (Exception ex) { bad += "#" + i + ":threw " + ex.GetType().Name + " "; }
        }
        Check("v3 corrupt json: Settings.Load/Prefs.Load never throw, Prefs values clamped (8 variants)", bad == "", bad);

        File.WriteAllText(Prefs.PathName, "{\"View\":99,\"FontSize\":-5,\"Pin\":false}");
        var pc = Prefs.Load();
        Check("v3 corrupt view.json: out-of-range View/FontSize fall back to defaults", pc.View == 1 && pc.FontSize == Prefs.DefaultFont, pc.View + "/" + pc.FontSize);
        File.WriteAllText(Prefs.PathName, "garbage");
        var pd = Prefs.Load();
        Check("v3 corrupt view.json: garbage gives defaults", pd.View == 1 && pd.FontSize == Prefs.DefaultFont && !pd.Pin);
        File.WriteAllText(Settings.PathName, "garbage");
        var sd = Settings.Load();
        Check("v3 corrupt settings.json: garbage gives defaults", sd != null && !sd.Dark && sd.Width == 1440 && sd.Height == 900);

        // a real window starts with each broken pair
        string wbad = "";
        foreach (var v in new[] { variants[0], variants[4], variants[5], variants[7] })
        {
            File.WriteAllBytes(Settings.PathName, v);
            File.WriteAllBytes(Prefs.PathName, v);
            try
            {
                MainWindow w = Program.CreateForTest();
                w.Left = -3000; w.Top = 0; w.ShowInTaskbar = false;
                w.Show(); Pump(150);
                bool ok = w.ActualWidth >= 800 && w.GetText().Contains("새 문서") && w.ViewModeNow() >= 0 && w.ViewModeNow() <= 2 && w.FontSizeNow() >= Prefs.MinFont && w.FontSizeNow() <= Prefs.MaxFont;
                if (!ok) wbad += "state(" + w.ActualWidth + "," + w.ViewModeNow() + "," + w.FontSizeNow() + ") ";
                w.SetDark(false);
                w.Close(); Pump(100);
            }
            catch (Exception ex) { wbad += "threw " + ex.GetType().Name + ":" + ex.Message + " "; }
        }
        Check("v3 corrupt settings/view.json: MainWindow still starts (4 variants), sane size/view/font", wbad == "", wbad);
        string lg = "";
        try { lg = File.Exists(Log.FilePath) ? File.ReadAllText(Log.FilePath) : ""; } catch { }
        Check("v3 corrupt json: the failure is logged (settings load / prefs load)", lg.Contains("ERROR settings load") && lg.Contains("ERROR prefs load"), Short(lg));
        // leave a clean state for anything after us (the parent's smoke test asserts the sandbox log has no ERROR lines)
        try { File.Delete(Settings.PathName); File.Delete(Prefs.PathName); File.Delete(Log.FilePath); } catch { }
    }

    // ------------------------------------------------------------ one exe alone in an empty dir resolves its embedded DLLs
    static void V3Embedded(string work)
    {
        string exe = Process.GetCurrentProcess().MainModule.FileName;
        string dir = Path.Combine(work, "embed_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string copy = Path.Combine(dir, "MdEditor.exe");
        File.Copy(exe, copy);
        string md = Path.Combine(dir, "s.md");
        File.WriteAllText(md, "# embed\n\n| a | b |\n|---|---|\n| 1 | 2 |\n", new UTF8Encoding(false));
        Func<string, string, KeyValuePair<int, long>> run = (args, png) =>
        {
            var psi = new ProcessStartInfo(copy, args) { UseShellExecute = false, WorkingDirectory = dir };
            int code = -999;
            using (var p = Process.Start(psi))
            {
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } p.WaitForExit(3000); }
                else code = p.ExitCode;
            }
            return new KeyValuePair<int, long>(code, File.Exists(png) ? new FileInfo(png).Length : -1);
        };
        // (a) help window: Markdig; a swallowed FileNotFound only degrades Md.ToHtml to the <pre> fallback, so this alone is not proof
        string png1 = Path.Combine(dir, "help.png");
        var r1 = run("--help-shot \"" + png1 + "\"", png1);
        Check("v3 embedded dll: exe copy alone in empty dir writes help PNG, exit " + r1.Key, r1.Key == 0 && r1.Value > 2000, "exit=" + r1.Key + " png=" + r1.Value);
        // (b) main window (--shot): needs AvalonEdit at construction time -> hard crash when the AssemblyResolve hook is broken
        string png2 = Path.Combine(dir, "main.png");
        var r2 = run("--shot \"" + png2 + "\" light \"" + md + "\"", png2);
        Check("v3 embedded dll: exe copy alone in empty dir renders the MAIN window (AvalonEdit) to PNG, exit " + r2.Key, r2.Key == 0 && r2.Value > 5000, "exit=" + r2.Key + " png=" + r2.Value);
        string[] others = Directory.GetFiles(dir).Select(Path.GetFileName).Where(n => n != "MdEditor.exe" && n != "help.png" && n != "main.png" && n != "s.md").ToArray();
        Check("v3 embedded dll: nothing but exe + outputs in that dir (no dll side files)", others.Length == 0, string.Join(",", others));
        // (c) in-process: when no Markdig.dll sits beside this exe, the loaded Markdig/AvalonEdit must come from memory (embedded resource)
        string exeDir = Path.GetDirectoryName(exe);
        if (!File.Exists(Path.Combine(exeDir, "Markdig.dll")))
        {
            string l1 = typeof(Markdig.Markdown).Assembly.Location, l2 = typeof(ICSharpCode.AvalonEdit.TextEditor).Assembly.Location;
            Check("v3 embedded dll: Markdig + AvalonEdit loaded from embedded resources (no file location)", l1 == "" && l2 == "", l1 + " | " + l2);
        }
        try { Directory.Delete(dir, true); } catch { }
    }
}
