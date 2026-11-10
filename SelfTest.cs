// OWNER: tester. `MdEditor.exe --selftest <emptydir>` : exit code = number of failed checks. Result lines in <dir>\result.txt.
// The real run happens in a child process whose USERPROFILE points into <dir>\profile, so Log/Settings (%AppData%\MdEditor)
// never touch the user's real data. Env MDSELFTEST_NOSMOKE=1 skips the 4 s real-GUI launch (used by mutation runs).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

static partial class SelfTest
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

    static List<string> res = new List<string>();
    static int fails;

    public static void Check(string name, bool ok, string detail = null)
    {
        if (ok) res.Add("PASS " + name);
        else { fails++; res.Add("FAIL " + name + (detail != null ? " :: " + Short(detail) : "")); }
    }
    // known product bug: reported as KNOWN (not counted as failure); becomes PASS once fixed
    public static void Known(string name, bool ok, string detail = null)
    {
        res.Add((ok ? "PASS " : "KNOWN-BUG ") + name + (ok || detail == null ? "" : " :: " + Short(detail)));
    }
    static string Short(string s) { s = s.Replace("\r", "\\r").Replace("\n", "\\n"); return s.Length > 300 ? s.Substring(0, 300) + "..." : s; }
    static void Try(string name, Action a)
    {
        try { a(); }
        catch (Exception ex) { fails++; res.Add("FAIL " + name + " threw " + Short(ex.ToString())); }
    }

    public static int Run(string[] args)
    {
        bool child = args.Contains("--child");
        string dir = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null;
        if (dir == null) return Out("usage: MdEditor.exe --selftest <empty dir>", 2);
        dir = Path.GetFullPath(dir);
        if (child) return RunChild(dir);
        return RunParent(dir);
    }

    static int Out(string s, int code)
    {
        try { AttachConsole(-1); Console.WriteLine(s); } catch { }
        return code;
    }

    // ------------------------------------------------------------ parent: sandbox + child + GUI smoke
    static int RunParent(string dir)
    {
        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any()) return Out("refused: folder is not empty: " + dir, 2);
        string prof = Path.Combine(dir, "profile");
        Directory.CreateDirectory(Path.Combine(prof, "AppData", "Roaming"));
        Directory.CreateDirectory(Path.Combine(prof, "AppData", "Local"));
        string exe = Process.GetCurrentProcess().MainModule.FileName;
        string childOut = Path.Combine(dir, "child.txt");

        var psi = new ProcessStartInfo(exe, "--selftest \"" + dir + "\" --child") { UseShellExecute = false, WorkingDirectory = dir };
        SetProfileEnv(psi, prof);
        int code;
        using (var p = Process.Start(psi))
        {
            if (!p.WaitForExit(180000)) { try { p.Kill(); } catch { } return Out("child timeout", 97); }
            code = p.ExitCode;
        }
        var lines = new List<string>();
        if (File.Exists(childOut)) lines.AddRange(File.ReadAllLines(childOut, Encoding.UTF8));
        else { lines.Add("FAIL child produced no result (exit " + code + ")"); code = code == 0 ? 1 : code; }
        int fcount = lines.Count(l => l.StartsWith("FAIL"));
        if (fcount == 0 && code != 0) { lines.Add("FAIL child exit code " + code); fcount++; }

        if (Environment.GetEnvironmentVariable("MDSELFTEST_NOSMOKE") != "1")
        {
            var sm = Smoke(exe, dir, prof);
            lines.AddRange(sm);
            fcount += sm.Count(l => l.StartsWith("FAIL"));
        }
        int pass = lines.Count(l => l.StartsWith("PASS"));
        var final = new List<string>(lines);
        final.Add("TOTAL pass=" + pass + " fail=" + fcount);
        File.WriteAllLines(Path.Combine(dir, "result.txt"), final.ToArray(), new UTF8Encoding(false));
        var sb = new StringBuilder();
        foreach (var l in final) if (l.StartsWith("FAIL") || l.StartsWith("TOTAL") || l.StartsWith("SKIP") || l.StartsWith("KNOWN")) sb.AppendLine(l);
        return Out(sb.ToString().TrimEnd(), fcount);
    }

    static void SetProfileEnv(ProcessStartInfo psi, string prof)
    {
        psi.EnvironmentVariables["USERPROFILE"] = prof;
        psi.EnvironmentVariables["APPDATA"] = Path.Combine(prof, "AppData", "Roaming");
        psi.EnvironmentVariables["LOCALAPPDATA"] = Path.Combine(prof, "AppData", "Local");
    }

    static List<string> Smoke(string exe, string dir, string prof)
    {
        var r = new List<string>();
        Mutex m;
        if (Mutex.TryOpenExisting("MdEditor_SingleInstance", out m))
        {
            m.Dispose();
            r.Add("SKIP smoke: a real MdEditor instance is running (would forward the file to it)");
            return r;
        }
        string sample = Path.Combine(dir, "smoke sample.md");
        File.WriteAllText(sample, "# 스모크\n\n| a | b |\n| --- | --- |\n| 1 | 2 |\n\n각주[^1]\n\n[^1]: 설명\n", new UTF8Encoding(false));
        var psi = new ProcessStartInfo(exe, "\"" + sample + "\"") { UseShellExecute = false, WorkingDirectory = dir };
        SetProfileEnv(psi, prof);
        Process p = null;
        try
        {
            p = Process.Start(psi);
            Thread.Sleep(4000);
            bool alive = !p.HasExited;
            r.Add((alive ? "PASS" : "FAIL") + " smoke: real GUI alive after 4s" + (alive ? "" : " (exit " + p.ExitCode + ")"));
            string log = Path.Combine(prof, "AppData", "Roaming", "MdEditor", "error.log");
            string txt = File.Exists(log) ? File.ReadAllText(log) : "";
            r.Add((txt.Contains("start v") ? "PASS" : "FAIL") + " smoke: log written inside sandbox profile");
            bool bad = Regex.IsMatch(txt, @"ERROR|UNHANDLED|Exception");
            r.Add((!bad ? "PASS" : "FAIL") + " smoke: log has no exception lines" + (bad ? " :: " + Short(txt) : ""));
            if (alive)
            {
                p.Refresh();
                string t = p.MainWindowTitle;
                bool ok = t.Contains("smoke sample.md");
                r.Add((ok ? "PASS" : "FAIL") + " smoke: window title shows opened file" + (ok ? "" : " :: '" + t + "'"));
            }
        }
        catch (Exception ex) { r.Add("FAIL smoke threw " + Short(ex.Message)); }
        finally
        {
            if (p != null) { try { if (!p.HasExited) p.Kill(); p.WaitForExit(3000); } catch { } }
        }
        return r;
    }

    // ------------------------------------------------------------ child: real checks
    static int RunChild(string dir)
    {
        string prof = Path.Combine(dir, "profile");
        if (!Log.Dir.StartsWith(prof, StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(Path.Combine(dir, "child.txt"), "FAIL sandbox: Log.Dir escaped profile: " + Log.Dir + "\n");
            return 98; // refuse to run against the real %AppData%
        }
        string work = Path.Combine(dir, "work");
        Directory.CreateDirectory(work);

        Try("md", MdTests);
        Try("md-xss", XssTests);
        Try("md-perf", PerfTests);
        Try("editops", EditTests);
        Try("program", ProgramTests);
        Try("help", HelpTests);
        Try("gutter", () => GutterTests(work));
        try { UiTests(work); }
        catch (Exception ex) { fails++; res.Add("FAIL ui threw " + Short(ex.ToString())); }

        File.WriteAllLines(Path.Combine(dir, "child.txt"), res.ToArray(), new UTF8Encoding(false));
        return fails;
    }

    static int Count(string s, string pat) { return Regex.Matches(s, pat).Count; }
    static int Occ(string s, string sub) { int n = 0, i = 0; while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; } return n; }

    // ------------------------------------------------------------ Md
    static void MdTests()
    {
        string h = Md.ToHtml("# H1\n## H2 ##\n###### h6\n####### seven");
        Check("md heading h1", h.Contains("<h1>H1</h1>"), h);
        Check("md heading h2 closing hashes stripped", h.Contains("<h2>H2</h2>"), h);
        Check("md heading h6", h.Contains("<h6>h6</h6>"), h);
        Check("md 7 hashes is not a heading", h.Contains("<p>####### seven</p>"), h);

        h = Md.ToHtml("| a | b | c |\n|:--|:-:|--:|\n| 1 | x\\|y | 3 |\n| only |");
        Check("md table 3 th", Count(h, "<th[ >]") == 3, h);
        Check("md table align left/center/right", h.Contains("<th style=\"text-align:left\">a</th>") && h.Contains("<th style=\"text-align:center\">b</th>") && h.Contains("<th style=\"text-align:right\">c</th>"), h);
        Check("md table escaped pipe stays in one cell", h.Contains(">x|y</td>") && Count(h, "<td[ >]") == 6, h);
        Check("md table short row padded to 3 cells", Count(h.Substring(h.LastIndexOf("<tr>")), "<td[ >]") == 3, h);
        h = Md.ToHtml("a | b\n--|--\n1 | 2");
        Check("md table without outer pipes", Count(h, "<th[ >]") == 2 && Count(h, "<td[ >]") == 2, h);

        h = Md.ToHtml("- a\n  - b\n    - c\n- [ ] todo\n- [x] done\n\n1. one\n2. two");
        Check("md nested list depth 3", Count(h, "<ul>") == 3, h);
        Check("md task list unchecked/checked", h.Contains("<input type=\"checkbox\" disabled> todo") && h.Contains("disabled checked> done"), h);
        Check("md task items have class task", Count(h, "<li class=\"task\">") == 2, h);
        Check("md ordered list", h.Contains("<ol>") && h.Contains("<li>one</li>") && h.Contains("<li>two</li>"), h);

        h = Md.ToHtml("```\nx[^1]\n[^1]: no\n```\n\nafter[^1]\n\n[^1]: real");
        Check("md footnote syntax inside fence is literal", h.Contains("<pre><code>x[^1]\n[^1]: no</code></pre>"), h);
        Check("md footnote outside fence still works", h.Contains("<li id=\"fn-1\">real ") && Count(h, "<li id=\"fn-") == 1, h);

        h = Md.ToHtml("text[^2] and[^1]\n\n[^1]: first\n[^2]: second");
        Check("md footnote numbered by first reference order", h.Contains("<a href=\"#fn-2\">[1]</a>") && h.Contains("<a href=\"#fn-1\">[2]</a>"), h);
        Check("md footnote list follows reference order", h.IndexOf("second") < h.IndexOf("first") && h.IndexOf("second") > 0, h);
        h = Md.ToHtml("a[^x] b[^x]\n\n[^x]: def");
        Check("md footnote referenced twice -> one entry, same number", Count(h, "<li id=\"fn-x\">") == 1 && Count(h, "\\[1\\]</a>") == 2, h);
        h = Md.ToHtml("miss[^9] here");
        Check("md footnote missing def numbered with (no content)", h.Contains("[1]") && h.Contains("footnotes") && !h.Contains("[^9]"), h);
        h = Md.ToHtml("no ref\n\n[^1]: unused");
        Check("md footnote def without reference renders no list", !h.Contains("footnotes"), h);

        h = Md.ToHtml("**b** *i* `c<d` & [l](https://e.com/a?x=1&y=2) <https://auto.com>");
        Check("md inline strong/em/code", h.Contains("<strong>b</strong>") && h.Contains("<em>i</em>") && h.Contains("<code>c&lt;d</code>"), h);
        Check("md inline amp escaped, link href", h.Contains(" &amp; ") && h.Contains("href=\"https://e.com/a?x=1&amp;y=2\""), h);
        Check("md autolink", h.Contains("<a href=\"https://auto.com\">"), h);
        Check("md Korean text kept", Md.ToHtml("안녕 **세계**").Contains("안녕 <strong>세계</strong>"));
        Check("md null/empty safe", Md.ToHtml(null) == "" && Md.ToHtml("") == "");

        string lf = "# T\r\na\r\nb\r\n\r\n| x | y |\r\n| - | - |\r\n| 1 | 2 |\r\n\r\n```\r\ncode\r\nmore\r\n```\r\n\r\n- a\r\n- b\r\n\r\n[^1]\r\n\r\n[^1]: n\r\n";
        string a = Md.ToHtml(lf), b = Md.ToHtml(lf.Replace("\r\n", "\n"));
        Check("md CRLF output equals LF output", a == b, a);
        Check("md CRLF leaves no CR in output", a.IndexOf('\r') < 0);
        Check("md lone CR treated as newline", Md.ToHtml("a\rb").Contains("a<br>"), Md.ToHtml("a\rb"));

        h = Md.ToHtml("```\nunclosed\n# not heading\n<b>x</b>");
        Check("md unclosed fence runs to end as code", h.Contains("<pre><code>unclosed\n# not heading\n&lt;b&gt;x&lt;/b&gt;</code></pre>") && !h.Contains("<h1>"), h);
        h = Md.ToHtml("~~~\ntilde\n~~~");
        Check("md tilde fence", h.Contains("<pre><code>tilde</code></pre>"), h);

        // preview vs export documents
        string pv = Md.PreviewDocument("x", false), pd = Md.PreviewDocument("x", true), ex = Md.ExportDocument("x", "T");
        foreach (var piece in new[] { "table{border-collapse", "th,td{border", "pre{", "blockquote{", ".footnotes", "sup a{", "code{" })
        {
            Check("css preview has '" + piece + "'", pv.Contains(piece));
            Check("css export has '" + piece + "'", ex.Contains(piece));
        }
        Check("preview has li.task + img rules", pv.Contains("li.task{list-style:none") && pv.Contains("img{max-width:100%"));
        Known("export lacks li.task rule (task list shows bullets in exported html)", ex.Contains("li.task"), "");
        Check("preview IE edge meta + charset", pv.Contains("X-UA-Compatible") && pv.Contains("charset=\"utf-8\""));
        Check("export doctype/charset/lang", ex.StartsWith("<!DOCTYPE html>") && ex.Contains("<meta charset=\"UTF-8\">") && ex.Contains("lang=\"ko\""));
        Check("preview light vs dark bg differ", pv.Contains("background:#fff;") && pd.Contains("background:#1e2024;") && !pd.Contains("background:#fff;color"), pd.Substring(0, 300));
        Check("preview body contains converted markdown", Md.PreviewDocument("# Z", false).Contains("<h1>Z</h1>\n</body>"));
        string t1 = Md.ExportDocument("x", "</title><script>alert(1)</script>&\"q");
        Check("export title escaped", !t1.Contains("<script>alert") && t1.Contains("<title>&lt;/title&gt;&lt;script&gt;alert(1)&lt;/script&gt;&amp;&quot;q</title>"), t1.Substring(0, 250));
        Check("export title null safe", Md.ExportDocument("x", null).Contains("<title></title>"));
        Check("export body markdown escaped too", !Md.ExportDocument("<script>x</script>", "t").Contains("<script>x"));
    }

    // ------------------------------------------------------------ XSS
    static readonly Regex TagRe = new Regex(@"<([a-zA-Z][a-zA-Z0-9]*)((?:[^>""]|""[^""]*"")*)>", RegexOptions.Singleline);
    static readonly HashSet<string> Allowed = new HashSet<string>(new[] { "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "a", "strong", "em", "b", "i", "u", "s", "sub", "sup", "kbd", "br", "code", "pre", "table", "thead", "tbody", "tr", "th", "td", "input", "img", "blockquote", "hr", "div", "del", "span" });

    // returns a problem description or null
    static string Unsafe(string html)
    {
        foreach (Match m in TagRe.Matches(html))
        {
            string name = m.Groups[1].Value.ToLowerInvariant();
            if (!Allowed.Contains(name)) return "tag <" + name + ">";
            string attrs = m.Groups[2].Value;
            string stripped = Regex.Replace(attrs, "\"[^\"]*\"", "\"\"");
            if (Regex.IsMatch(stripped, @"(^|[\s""'/])on\w+\s*=", RegexOptions.IgnoreCase)) return "event attr in " + m.Value;
            foreach (Match a in Regex.Matches(attrs, @"\b(href|src)\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase))
            {
                string v = a.Groups[2].Value.Replace("&amp;", "&").Replace("&quot;", "\"");
                var sbv = new StringBuilder();
                foreach (char ch in v) if (ch > ' ' && ch != 127) sbv.Append(ch);
                string cv = sbv.ToString().ToLowerInvariant();
                int c = cv.IndexOf(':');
                if (c > 1 && cv.IndexOfAny(new[] { '/', '?', '#' }) is int s && (s < 0 || c < s) && !(cv.StartsWith("http:") || cv.StartsWith("https:") || cv.StartsWith("mailto:") || cv.StartsWith("file:")))
                    return "scheme in " + m.Value;
            }
            if (Regex.IsMatch(attrs, @"\bstyle\s*=", RegexOptions.IgnoreCase) && name != "th" && name != "td") return "style attr in " + m.Value;
        }
        return null;
    }

    static void XssTests()
    {
        var cases = new Dictionary<string, string>
        {
            { "script tag", "<script>alert(1)</script>" },
            { "script in para", "hello <script>alert(1)</script> world" },
            { "javascript link", "[x](javascript:alert(1))" },
            { "javascript link mixed case", "[x](  JaVaScRiPt:alert(1))" },
            { "javascript link entity", "[x](&#106;avascript:alert(1))" },
            { "javascript link colon entity", "[x](javascript&colon;alert(1))" },
            { "javascript angle dest", "[x](<javascript:alert(1)>)" },
            { "javascript ref def", "[x][r]\n\n[r]: javascript:alert(1)" },
            { "javascript autolink", "<javascript:alert(1)>" },
            { "vbscript link", "[x](vbscript:msgbox(1))" },
            { "tab-obfuscated scheme", "[x](java\tscript:alert(1))" },
            { "tab-obfuscated angle", "[x](<java\tscript:alert(1)>)" },
            { "newline-obfuscated scheme", "[x](java&#10;script:alert(1))" },
            { "control-char scheme", "[x](\u0001javascript:alert(1))" },
            { "img onerror raw", "<img src=x onerror=alert(1)>" },
            { "img onerror quoted", "<img src=\"x\" onerror=\"alert(1)\">" },
            { "svg onload", "<svg onload=alert(1)>" },
            { "iframe", "<iframe src=\"javascript:alert(1)\"></iframe>" },
            { "style tag", "<style>body{display:none}</style>" },
            { "raw a href js", "<a href=\"javascript:alert(1)\">x</a>" },
            { "data img", "![i](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)" },
            { "data link", "[x](data:text/html,<script>alert(1)</script>)" },
            { "attr breakout href", "[x](http://a.com\"onmouseover=\"alert(1))" },
            { "attr breakout href 2", "[x](http://a.com \"t\"onmouseover=\"a\")" },
            { "attr breakout title", "[x](http://a.com \"t\" onmouseover=\"alert(1)\")" },
            { "attr breakout img alt", "![a\" onerror=\"alert(1)](http://a/b.png)" },
            { "attr breakout img src", "![a](http://a/b.png\" onerror=\"alert(1))" },
            { "attr breakout footnote label", "a[^x\" onmouseover=\"alert(1)]\n\n[^x\" onmouseover=\"alert(1)]: d" },
            { "allowed tag with attr", "<b onclick=alert(1)>x</b>" },
            { "allowed tag with attr 2", "<kbd style=\"x\" onclick=alert(1)>x</kbd>" },
            { "html in table cell", "| a |\n|---|\n| <img src=x onerror=alert(1)> |" },
            { "html in heading", "# <script>alert(1)</script>" },
            { "html in code span", "`<script>alert(1)</script>`" },
            { "html in fence", "```\n<script>alert(1)</script>\n```" },
            { "html in blockquote/list", "> <script>x</script>\n\n- <img src=x onerror=y>" },
            { "html in footnote def", "a[^1]\n\n[^1]: <script>alert(1)</script>" },
            { "nested emphasis breakout", "**<img src=x onerror=alert(1)>**" },
            { "file link ok but safe", "[x](file:///C:/a.txt)" },
        };
        foreach (var kv in cases)
        {
            string html = Md.ToHtml(kv.Value);
            string prob = Unsafe(html);
            Check("xss " + kv.Key, prob == null, prob + " :: " + html);
        }
        string raw = Md.ToHtml("<script>alert(1)</script>");
        Check("xss script text visible as escaped text", raw.Contains("&lt;script&gt;"), raw);
        Check("xss javascript link renders no anchor", !Md.ToHtml("[x](javascript:alert(1))").Contains("<a "));
        Check("xss data img renders no img", !Md.ToHtml("![i](data:image/png;base64,AAAA)").Contains("<img"));
        Check("xss tab scheme renders no anchor", !Md.ToHtml("[x](java\tscript:alert(1))").Contains("<a "));
        Check("xss http link kept", Md.ToHtml("[x](https://a.com/p)").Contains("<a href=\"https://a.com/p\">x</a>"));
        Check("xss relative + anchor link kept", Md.ToHtml("[x](docs/a.md) [y](#sec)").Contains("href=\"docs/a.md\"") && Md.ToHtml("[y](#sec)").Contains("href=\"#sec\""));
        Check("xss mailto kept", Md.ToHtml("[x](mailto:a@b.c)").Contains("href=\"mailto:a@b.c\""));
        Check("xss raw html tags are escaped, not rendered (v3.0 Markdig, DisableHtml)", Md.ToHtml("<b>x</b> <kbd>k</kbd>").Contains("&lt;b&gt;x&lt;/b&gt;") && !Md.ToHtml("<kbd>k</kbd>").Contains("<kbd>"));
        Check("xss export document also safe", Unsafe(Md.ExportDocument("<script>x</script>[a](javascript:1)", "t").Split(new[] { "<body>" }, StringSplitOptions.None)[1]) == null);
    }

    // ------------------------------------------------------------ perf / robustness
    static void PerfTests()
    {
        const string chunk = "## Title\n\nSome *emphasis* and **bold** with `code` and a [link](https://example.com/a?b=1) and footnote[^1].\n\n| a | b |\n| --- | --- |\n| 1 | 2 |\n\n- item one\n  - nested\n- [ ] task\n\n> quote\n\n```\ncode\n```\n\n";
        Md.ToHtml(chunk + "[^1]: x"); // warm-up (regex compile / JIT)
        var sb = new StringBuilder();
        while (sb.Length < 1048576) sb.Append(chunk);
        sb.Append("[^1]: x");
        string big = sb.ToString();
        var sw = Stopwatch.StartNew();
        string o = Md.ToHtml(big);
        sw.Stop();
        Check("perf 1MB mixed markdown < 1000 ms (was " + sw.ElapsedMilliseconds + " ms)", sw.ElapsedMilliseconds < 1000, sw.ElapsedMilliseconds + " ms");
        Check("perf 1MB output is converted", o.Contains("<h2>Title</h2>") && o.Contains("<table>") && o.Length > big.Length / 2);

        var adv = new Dictionary<string, string>
        {
            { "100k unclosed emphasis", Rep("*a ", 100000) },
            { "100k unclosed links", Rep("[a](", 100000) },
            { "200k brackets", Rep("[", 200000) },
            { "300k lt", Rep("<", 300000) },
            { "50k backticks single", Rep("`a", 50000) },
            { "deep blockquote single line", Rep(">", 5000) + " x" },
            { "deep nested list", string.Join("\n", Enumerable.Range(0, 400).Select(i => new string(' ', i * 2) + "- x").ToArray()) },
            { "20k table rows", "| a | b |\n|---|---|\n" + Rep("| 1 | 2 |\n", 20000) },
            { "50k underscores", Rep("_a", 50000) },
        };
        foreach (var kv in adv)
        {
            sw.Restart();
            string r = null; Exception e = null;
            try { r = Md.ToHtml(kv.Value); } catch (Exception ex) { e = ex; }
            sw.Stop();
            Check("robust " + kv.Key + " no throw and < 1500 ms (" + sw.ElapsedMilliseconds + " ms)", e == null && r != null && sw.ElapsedMilliseconds < 1500, e != null ? e.Message : sw.ElapsedMilliseconds + " ms");
        }
    }
    static string Rep(string s, int n) { var sb = new StringBuilder(s.Length * n); for (int i = 0; i < n; i++) sb.Append(s); return sb.ToString(); }

    // ------------------------------------------------------------ EditOps
    static string Cr(string s) { return s.Replace("\n", "\r\n"); }
    static bool NoBareLf(string s) { return Regex.IsMatch(s, "^(?:[^\r\n]|\r\n)*$"); }

    static void EditTests()
    {
        // Wrap
        var r = EditOps.Wrap("abcdef", 1, 3, "**", "**");
        Check("wrap selection", r.Ok && r.Text == "a**bcd**ef" && r.SelStart == 3 && r.SelLength == 3, r.Text);
        r = EditOps.Wrap("abc", 3, 0, "*", "*");
        Check("wrap no selection inserts placeholder", r.Ok && r.Text == "abc*텍스트*" && r.SelStart == 4 && r.SelLength == 3, r.Text);
        r = EditOps.Wrap("abc", -5, 100, "[", "]");
        Check("wrap out-of-range negative start clamps", r.Ok && r.Text == "[abc]", r.Text);
        r = EditOps.Wrap("abc", 99, 5, "(", ")");
        Check("wrap start beyond end clamps", r.Ok && r.Text.StartsWith("abc(") && r.SelStart <= r.Text.Length, r.Text);
        r = EditOps.Wrap(null, 0, 0, "`", "`");
        Check("wrap null text", r.Ok && r.Text == "`텍스트`", r.Text);
        r = EditOps.Wrap(Cr("a\nb"), 0, 4, "*", "*");
        Check("wrap keeps CRLF inside selection", r.Text == "*a\r\nb*" || r.Text == "*" + Cr("a\nb") + "*", r.Text);

        // LineStart
        r = EditOps.LineStart("hello\nworld", 8, "# ");
        Check("linestart middle of 2nd line", r.Text == "hello\n# world" && r.SelStart == 8, r.Text);
        r = EditOps.LineStart(Cr("hello\nworld\nz"), 9, "- ");
        Check("linestart CRLF 2nd line", r.Text == Cr("hello\n- world\nz"), r.Text);
        r = EditOps.LineStart("abc", 0, "> ");
        Check("linestart caret 0", r.Text == "> abc" && r.SelStart == 2, r.Text);
        r = EditOps.LineStart("abc\n", 4, "1. ");
        Check("linestart caret at end after newline (empty last line)", r.Text == "abc\n1. ", r.Text);
        r = EditOps.LineStart("abc", 999, "## ");
        Check("linestart caret beyond end", r.Ok && r.Text == "## abc", r.Text);
        r = EditOps.LineStart("abc", -3, "## ");
        Check("linestart negative caret", r.Ok && r.Text == "## abc", r.Text);
        r = EditOps.LineStart("", 0, "- ");
        Check("linestart empty doc", r.Ok && r.Text == "- ", r.Text);

        // Block / CodeBlock / Link
        r = EditOps.CodeBlock("abc", 3);
        Check("codeblock mid-line starts on new line", r.Text.StartsWith("abc\n") && r.Text.Contains("```\n코드 입력\n```"), r.Text);
        r = EditOps.CodeBlock(Cr("abc\ndef"), 3);
        Check("codeblock CRLF doc uses CRLF only", NoBareLf(r.Text) && r.Text.Contains("```\r\n코드 입력\r\n```"), r.Text);
        r = EditOps.CodeBlock("", 50);
        Check("codeblock empty doc caret out of range", r.Ok && r.Text.Contains("코드 입력"), r.Text);
        r = EditOps.Link("see foo now", 4, 3);
        Check("link wraps selection", r.Text == "see [foo](https://) now" && r.SelStart == 5 && r.SelLength == 3, r.Text);
        r = EditOps.Link("", 0, 0);
        Check("link without selection", r.Text == "[텍스트](https://)", r.Text);

        // InsertTable
        r = EditOps.InsertTable("", 0, 2, 3);
        string th = Md.ToHtml(r.Text);
        Check("table insert renders 3 th, 2 body rows", Count(th, "<th[ >]") == 3 && Count(th, "<tr>") == 3, r.Text + " => " + th);
        r = EditOps.InsertTable("abc", 3, 1, 1);
        Check("table insert after text starts on own line", r.Text.StartsWith("abc\n\n|") || r.Text.StartsWith("abc\n|") , r.Text);
        r = EditOps.InsertTable(Cr("abc\n"), 5, 2, 2);
        Check("table insert in CRLF doc is pure CRLF", NoBareLf(r.Text), r.Text);
        r = EditOps.InsertTable("", 0, 999, 999);
        Check("table size clamped to 30 rows 15 cols", Count(Md.ToHtml(r.Text), "<th[ >]") == 15 && Count(Md.ToHtml(r.Text), "<tr>") == 31, "");
        r = EditOps.InsertTable("", 0, -1, 0);
        Check("table size lower clamp 1x1", Count(Md.ToHtml(r.Text), "<th[ >]") == 1 && Count(Md.ToHtml(r.Text), "<tr>") == 2, r.Text);

        // AddRow
        string tbl = "| a | b | c |\n| --- | --- | --- |\n| 1 | 2 | 3 |";
        r = EditOps.AddRow(tbl, 2);
        Check("addrow appends row with 3 cells at table end", r.Ok && r.Text == tbl + "\n|  |  |  |", r.Text);
        r = EditOps.AddRow(tbl + "\n\nparagraph after", 2);
        Check("addrow inserts before following paragraph", r.Ok && r.Text == tbl + "\n|  |  |  |\n\nparagraph after", r.Text);
        r = EditOps.AddRow(Cr(tbl) + Cr("\n\nafter"), 5);
        Check("addrow CRLF pure CRLF and no extra CR", r.Ok && NoBareLf(r.Text) && r.Text.Contains(Cr("| 1 | 2 | 3 |\n|  |  |  |\n\nafter")), r.Text);
        r = EditOps.AddRow(tbl, 9999);
        Check("addrow caret out of range -> clamps to end (in last table line)", r.Ok, r.Error);
        r = EditOps.AddRow("no table here", 3);
        Check("addrow outside table fails with message", !r.Ok && !string.IsNullOrEmpty(r.Error) && r.Text == "no table here");
        r = EditOps.AddRow("", 0);
        Check("addrow on empty doc fails gracefully", !r.Ok);
        r = EditOps.AddRow("x\n| a | b |\n|---|---|", 0);
        Check("addrow caret on non-table line above table fails", !r.Ok);

        // AddCol
        r = EditOps.AddCol(tbl, 2);
        string[] ls = r.Text.Split('\n');
        Check("addcol every line has one more column", r.Ok && ls.Length == 3 && ls.All(l => l.Count(ch => ch == '|') == 5), r.Text);
        Check("addcol separator line gets --- cell", ls.Length == 3 && Count(ls[1], "---") == 4, r.Text);
        Check("addcol keeps old content", ls[0].StartsWith("| a | b | c |") && ls[2].StartsWith("| 1 | 2 | 3 |"), r.Text);
        Check("addcol result renders 4 columns", Count(Md.ToHtml(r.Text), "<th[ >]") == 4 && Count(Md.ToHtml(r.Text), "<td[ >]") == 4, Md.ToHtml(r.Text));
        r = EditOps.AddCol(Cr(tbl), 4);
        Check("addcol CRLF pure CRLF, 3 lines", r.Ok && NoBareLf(r.Text) && r.Text.Split(new[] { "\r\n" }, StringSplitOptions.None).Length == 3, r.Text);
        r = EditOps.AddCol("a | b\n--|--\n1 | 2", 0);
        Known("addcol on pipe-less table keeps it a table (EditOps.AddCol: new header cell is empty -> trailing '|' dropped by Md, header 2 cols vs sep 3)", r.Ok &&Count(Md.ToHtml(r.Text), "<th[ >]") == 3, r.Text + " => " + Md.ToHtml(r.Text));
        r = EditOps.AddCol("| a | b |\n|:--|--:|\n| 1 | 2 |", 3);
        Check("addcol aligned separator still separator", Count(Md.ToHtml(r.Text), "<th[ >]") == 3 && Md.ToHtml(r.Text).Contains("text-align:left"), r.Text);
        r = EditOps.AddCol("plain text", 2);
        Check("addcol outside table fails", !r.Ok && !string.IsNullOrEmpty(r.Error));
        r = EditOps.AddCol(tbl, -10);
        Check("addcol negative caret clamps (caret at 0 is in table)", r.Ok);
        r = EditOps.AddCol(tbl + "\n\ntail", 10000);
        Check("addcol caret beyond end on non-table tail fails w/o exception", !r.Ok);
        r = EditOps.AddCol(tbl, 2);
        Check("addcol caret stays in same line", r.Ok && r.SelStart >= 0 && r.SelStart <= r.Text.IndexOf('\n'), r.SelStart.ToString());

        // Footnote
        r = EditOps.InsertFootnote("hello", 5);
        Check("footnote first = [^1] + def at end", r.Text == "hello[^1]\n\n[^1]: " && r.SelStart == r.Text.Length, r.Text);
        r = EditOps.InsertFootnote("a[^1] b[^3]\n\n[^1]: x\n[^3]: y", 1);
        Check("footnote after [^1],[^3] -> [^4]", r.Text.Contains("a[^4][^1] b[^3]") && r.Text.EndsWith("\n\n[^4]: "), r.Text);
        r = EditOps.InsertFootnote(Cr("a\nb"), 1);
        Check("footnote CRLF doc pure CRLF", NoBareLf(r.Text) && r.Text.EndsWith("\r\n\r\n[^1]: "), r.Text);
        r = EditOps.InsertFootnote("a\n", 999);
        Check("footnote caret beyond end + trailing newline", r.Ok && r.Text == "a\n[^1]\n[^1]: " || r.Text.StartsWith("a\n[^1]"), r.Text);
        r = EditOps.InsertFootnote("x[^99999999999999]", 0);
        Check("footnote huge number does not throw", r.Ok);
        r = EditOps.InsertFootnote("x[^abc]", 0);
        Check("footnote non-numeric labels ignored", r.Text.StartsWith("[^1]x"), r.Text);

        // Count
        int c, w;
        EditOps.Count("안녕 하세요 abc", out c, out w);
        Check("count chars/words", c == 10 && w == 3, c + "," + w);
        EditOps.Count(null, out c, out w);
        Check("count null", c == 0 && w == 0);
        EditOps.Count("  a \n\n b\t", out c, out w);
        Check("count words across whitespace", w == 2, w.ToString());
    }

    // ------------------------------------------------------------ Program helpers (reflection, private)
    static void ProgramTests()
    {
        var mi = typeof(Program).GetMethod("FileArg", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Check("program FileArg exists", mi != null);
        if (mi == null) return;
        Func<string[], string> f = a => (string)mi.Invoke(null, new object[] { a });
        Check("FileArg none", f(new string[0]) == null);
        Check("FileArg md full path", f(new[] { "a.md" }) == Path.GetFullPath("a.md"));
        Check("FileArg upper-case ext + markdown + txt", f(new[] { "A.MD" }) != null && f(new[] { "x.markdown" }) != null && f(new[] { "x.txt" }) != null);
        Check("FileArg skips -- flags", f(new[] { "--x", "b.md" }) == Path.GetFullPath("b.md"));
        Check("FileArg non-md ignored", f(new[] { "a.exe" }) == null && f(new[] { "a.md.exe" }) == null);
        Check("FileArg path with spaces", f(new[] { @"C:\my dir\한글 파일.md" }) == @"C:\my dir\한글 파일.md");
        Check("FileArg scans all args (as original find)", f(new[] { "a.exe", "b.md" }) == Path.GetFullPath("b.md"));
    }
}
