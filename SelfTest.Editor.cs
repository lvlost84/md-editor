// AvalonEdit editor checks: theme, shortcuts, line ops, find/replace, go-to-line, view mode/pin/zoom, big document, recovery.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

static partial class SelfTest
{
    static void EditorTests(string work)
    {
        MainWindow w = Program.CreateForTest();
        w.Left = -3000; w.Top = 0; w.ShowInTaskbar = false;
        w.Show();
        Pump(500);

        // --- editor basics
        Check("ed line numbers on", w.LineNumbersShown());
        Check("ed word wrap on", w.WordWrapOn());
        Check("ed markdown highlighting on", w.HighlightingOn());
        w.SetText("");
        Check("ed placeholder shown when empty", w.PlaceholderVisible());
        w.SetText("x");
        Check("ed placeholder hidden when text", !w.PlaceholderVisible());
        w.SetDark(false);
        string hl = w.HeadingColorHex(), bgL = w.EditorBackgroundHex(), selL = w.SelectionBrushHex();
        w.SetDark(true);
        string hd = w.HeadingColorHex(), bgD = w.EditorBackgroundHex(), selD = w.SelectionBrushHex();
        Check("ed highlight colour follows theme (heading light != dark)", hl.Length > 0 && hd.Length > 0 && hl != hd, hl + " / " + hd);
        Check("ed background follows theme", bgL == "#FFFFFFFF" && bgD == "#FF123A5E", bgL + " / " + bgD);
        Check("ed selection brush follows theme", selL.Length > 0 && selL != selD, selL + " / " + selD);
        w.SetDark(false);

        // --- shortcut table fully bound (every non-builtin entry has a KeyBinding)
        foreach (var d in Shortcuts.All)
            Check("sc bound: " + d.Display("+"), w.IsShortcutBound(d.Display("+")));
        Check("sc help table lists every shortcut", Shortcuts.All.All(d => HelpWindow.BuildHtml(false).Contains(">" + d.Display(" + ") + "<")));

        Func<string, int, int, string, string> sc = (text, cs, cl, key) =>
        {
            w.SetText(text); w.SetCaret(cs, cl);
            return w.Shortcut(key) ? w.GetText() : "<<unbound>>";
        };
        string o;
        // headings
        o = sc("# a", 0, 0, "Ctrl+Alt+2"); Check("sc H2 on '# a' replaces level", o == "## a", o);
        o = sc("a", 0, 0, "Ctrl+Alt+3"); Check("sc H3 adds prefix", o == "### a", o);
        o = sc("## a", 2, 0, "Ctrl+Alt+2"); Check("sc H2 again toggles off", o == "a", o);
        o = sc("a\nb", 0, 3, "Ctrl+Alt+1"); Check("sc H1 on two selected lines", o == "# a\n# b", o);
        o = sc("a", 0, 0, "Ctrl+Alt+6"); Check("sc H6", o == "###### a", o);
        o = sc("## a", 3, 0, "Ctrl+Alt+0"); Check("sc Ctrl+Alt+0 strips heading", o == "a", o);
        o = sc("- a", 0, 0, "Ctrl+Alt+0"); Check("sc Ctrl+Alt+0 strips bullet", o == "a", o);
        o = sc("12. a", 0, 0, "Ctrl+Alt+0"); Check("sc Ctrl+Alt+0 strips number", o == "a", o);
        o = sc("> a", 0, 0, "Ctrl+Alt+0"); Check("sc Ctrl+Alt+0 strips quote", o == "a", o);
        o = sc("plain", 0, 0, "Ctrl+Alt+0"); Check("sc Ctrl+Alt+0 leaves plain text", o == "plain", o);
        o = sc("# a\r\n# b", 0, 8, "Ctrl+Alt+2"); Check("sc H2 keeps CRLF", o == "## a\r\n## b", o.Replace("\r", "\\r"));
        // inline
        o = sc("abc", 0, 3, "Ctrl+B"); Check("sc Ctrl+B", o == "**abc**", o);
        o = sc("abc", 0, 3, "Ctrl+I"); Check("sc Ctrl+I", o == "*abc*", o);
        o = sc("abc", 0, 3, "Ctrl+Shift+X"); Check("sc strike", o == "~~abc~~", o);
        o = sc("abc", 0, 3, "Ctrl+K"); Check("sc link", o == "[abc](https://)", o);
        o = sc("abc", 0, 3, "Ctrl+`"); Check("sc inline code", o == "`abc`", o);
        o = sc("abc", 3, 0, "Ctrl+Shift+K"); Check("sc code block", o.Contains("```") && o.StartsWith("abc"), o);
        o = sc("abc", 3, 0, "Ctrl+Alt+F"); Check("sc footnote", o == "abc[^1]\n\n[^1]: ", o);
        o = sc("abc", 3, 0, "Ctrl+Enter"); Check("sc horizontal rule", o.Contains("\n---\n") && o.StartsWith("abc"), o);
        MainWindow.SetTableSizeForTest(1, 2);
        o = sc("", 0, 0, "Ctrl+Alt+T"); MainWindow.SetTableSizeForTest(0, 0);
        Check("sc insert table (modal bypassed)", o.Contains("| --- |") || o.Contains("---"), o);
        // lists / quote
        o = sc("a\nb", 0, 3, "Ctrl+Shift+L"); Check("sc bullet list on 2 lines", o == "- a\n- b", o);
        o = sc("- a\n- b", 0, 7, "Ctrl+Shift+L"); Check("sc bullet list toggles off", o == "a\nb", o);
        o = sc("a\nb\nc", 0, 5, "Ctrl+Shift+O"); Check("sc numbered list numbers lines", o == "1. a\n2. b\n3. c", o);
        o = sc("- a\n- b", 0, 7, "Ctrl+Shift+O"); Check("sc numbered replaces bullets", o == "1. a\n2. b", o);
        o = sc("a", 0, 0, "Ctrl+Shift+Q"); Check("sc quote", o == "> a", o);
        o = sc("> a", 0, 0, "Ctrl+Shift+Q"); Check("sc quote toggles off", o == "a", o);
        // editor-only commands do nothing without focus (real key path, no test bypass)
        w.SetText("abc"); w.SetCaret(0, 3);
        w.FocusFileName(); Pump(100); w.ShortcutRealFocus("Ctrl+B");
        Check("sc editor-only bold ignored when editor not focused", w.GetText() == "abc", w.GetText());

        // --- move lines
        o = sc("a\nb\nc", 2, 0, "Alt+Shift+Up"); Check("move line up", o == "b\na\nc" && w.CaretLine() == 1, o + " ln" + w.CaretLine());
        o = sc("a\nb\nc", 2, 0, "Alt+Shift+Down"); Check("move line down", o == "a\nc\nb" && w.CaretLine() == 3, o + " ln" + w.CaretLine());
        o = sc("a\nb", 0, 0, "Alt+Shift+Up"); Check("move first line up = no change", o == "a\nb", o);
        o = sc("a\nb", 2, 0, "Alt+Shift+Down"); Check("move last line down = no change", o == "a\nb", o);
        o = sc("a\nb\nc\nd", 2, 3, "Alt+Shift+Down"); Check("move 2 selected lines down", o == "a\nd\nb\nc" && w.GetSelection() == "b\nc", o + " sel=" + w.GetSelection());
        o = sc("a\r\nb\r\nc", 3, 0, "Alt+Shift+Up"); Check("move line up keeps CRLF", o == "b\r\na\r\nc", o.Replace("\r", "\\r"));
        o = sc("a\r\nb\r\nc", 3, 0, "Alt+Shift+Down"); Check("move line down keeps CRLF", o == "a\r\nc\r\nb", o.Replace("\r", "\\r"));
        w.UndoGroupsProbe();

        // --- indent / outdent
        o = sc("a\nb", 0, 3, "Tab"); Check("indent selected lines by 2 spaces", o == "  a\n  b", o);
        o = sc("  a\n  b", 0, 7, "Shift+Tab"); Check("outdent selected lines by 2 spaces", o == "a\nb", o);
        o = sc("    a", 4, 0, "Shift+Tab"); Check("outdent single line", o == "  a", o);

        // --- find / replace
        Check("fc count case-insensitive", FindCore.Count("Aa aA aa", "aa", false) == 3);
        Check("fc count match case", FindCore.Count("Aa aA aa", "aa", true) == 1);
        int n;
        string rr = FindCore.ReplaceAll("Aa aA aa", "aa", "X", false, out n);
        Check("fc replace all text+count", rr == "X X X" && n == 3, rr + " " + n);
        Check("fc replace empty find = no-op", FindCore.ReplaceAll("abc", "", "X", false, out n) == "abc" && n == 0);
        Check("fc replace with longer/shorter text", FindCore.ReplaceAll("a-a-a", "a", "bb", true, out n) == "bb-bb-bb" && n == 3);
        w.SetText("Aa aA aa\nAA");
        int cnt = w.ReplaceAllForTest("aa", "X", false);
        Check("replace all returns count (case-insens)", cnt == 4 && w.GetText() == "X X X\nX", cnt + " " + w.GetText());
        Check("replace all = ONE undo step", w.CanUndoForTest() && w.UndoGroupsProbe() == 1 && w.GetText() == "Aa aA aa\nAA", w.GetText());
        w.SetText("Aa aA aa");
        cnt = w.ReplaceAllForTest("aa", "X", true);
        Check("replace all match case", cnt == 1 && w.GetText() == "Aa aA X", cnt + " " + w.GetText());
        w.SetText("one two one"); w.SetCaret(0, 0);
        Check("find next selects first", w.FindNextForTest("one", true) && w.SelStart == 0 && w.SelLength == 3);
        Check("find next moves on and wraps", w.FindNextForTest("one", true) && w.SelStart == 8 && w.FindNextForTest("one", true) && w.SelStart == 0);
        Check("find next missing = false", !w.FindNextForTest("zzz", true));
        w.Shortcut("Ctrl+F");
        Pump(200);
        Check("Ctrl+F opens AvalonEdit search panel", w.SearchPanelOpen());

        // --- go to line
        string ten = string.Join("\n", Enumerable.Range(1, 10).Select(i => "line" + i));
        w.SetText(ten);
        w.GoToLineForTest(5);
        Check("goto line 5", w.CaretLine() == 5 && w.CaretColumn() == 1, w.CaretLine() + ":" + w.CaretColumn());
        w.GoToLineForTest(99);
        Check("goto line past end clamps", w.CaretLine() == 10, w.CaretLine().ToString());
        w.GoToLineForTest(0);
        Check("goto line 0 clamps to 1", w.CaretLine() == 1);
        MainWindow.SetGoToLineForTest(3);
        w.Shortcut("Ctrl+G");
        MainWindow.SetGoToLineForTest(0);
        Check("Ctrl+G goes to line (dialog bypassed)", w.CaretLine() == 3, w.CaretLine().ToString());
        w.SetCaret(ten.IndexOf("line7", StringComparison.Ordinal) + 2, 0);
        Check("caret status text Ln/Col", w.FooterCaretText() == "Ln 7, Col 3", w.FooterCaretText());

        // --- status hooks
        w.SetText("a\nbc 한글"); w.ForcePreviewRefresh(); w.SetCaret(3, 0);
        Check("status lines/chars/words", w.Status.TotalLines == 2 && w.Status.Chars == 7 && w.Status.Words == 3, w.Status.TotalLines + "/" + w.Status.Chars + "/" + w.Status.Words);
        Check("status utf8 size", w.Status.Utf8Bytes == 1 + 1 + 2 + 1 + 6, w.Status.Utf8Bytes.ToString());
        Check("status caret", w.Status.Line == 2 && w.Status.Col == 2, w.Status.Line + ":" + w.Status.Col);
        w.SetCaret(0, 3);
        Check("status selection length", w.Status.SelLength == 3, w.Status.SelLength.ToString());

        // --- new document
        w.SetText("abc"); w.SaveForTest(Path.Combine(work, "newdoc.md"));
        w.Shortcut("Ctrl+N");
        Check("Ctrl+N when clean -> empty untitled", w.GetText() == "" && w.GetFileNameBox() == "untitled.md" && w.CurrentPath == null && !w.IsDirty());

        // --- zoom
        w.ZoomResetForTest();
        Check("zoom default 100%", w.FontSizeNow() == 14.5 && w.ZoomPercentForTest() == 100 && w.FooterZoomText() == "100%", w.FooterZoomText());
        w.ZoomForTest(1);
        Check("zoom in +1pt", w.FontSizeNow() == 15.5 && w.FooterZoomText() == "107%", w.FontSizeNow() + " " + w.FooterZoomText());
        for (int i = 0; i < 60; i++) w.ZoomForTest(1);
        Check("zoom max 40", w.FontSizeNow() == 40, w.FontSizeNow().ToString());
        for (int i = 0; i < 80; i++) w.ZoomForTest(-1);
        Check("zoom min 8", w.FontSizeNow() == 8, w.FontSizeNow().ToString());
        w.Shortcut("Ctrl+0");
        Check("Ctrl+0 resets zoom", w.FontSizeNow() == 14.5 && w.ZoomPercentForTest() == 100);
        w.ZoomForTest(2);
        Check("zoom persisted to view.json", Prefs.Load().FontSize == 16.5, Prefs.Load().FontSize.ToString());
        w.ZoomResetForTest();

        // --- view mode cycle + persist
        Check("view default split", w.ViewModeNow() == 1 && w.FooterModeText().StartsWith("분할"), w.FooterModeText());
        w.Shortcut("Ctrl+Shift+V");
        Check("view cycle -> preview only (persisted)", w.ViewModeNow() == 2 && Prefs.Load().View == 2 && w.FooterModeText().StartsWith("미리보기"), w.FooterModeText());
        w.Shortcut("Ctrl+Shift+V");
        Check("view cycle -> editor only (persisted)", w.ViewModeNow() == 0 && Prefs.Load().View == 0, w.ViewModeNow().ToString());
        w.Shortcut("Ctrl+Shift+V");
        Check("view cycle -> split (persisted)", w.ViewModeNow() == 1 && Prefs.Load().View == 1);

        // --- always on top (persisted)
        Check("pin default off", !w.PinnedNow() && !Prefs.Load().Pin);
        w.Shortcut("Ctrl+Alt+P");
        Check("Ctrl+Alt+P pins window + saves flag", w.PinnedNow() && Prefs.Load().Pin && w.FooterModeText().Contains("항상 위"), w.FooterModeText());
        w.Close(); Pump(200);
        MainWindow w2 = Program.CreateForTest();
        w2.Left = -3000; w2.Top = 0; w2.ShowInTaskbar = false; w2.Show(); Pump(300);
        Check("pin restored on next start", w2.PinnedNow());
        w2.Shortcut("Ctrl+Alt+P");
        Check("Ctrl+Alt+P unpins + saves flag", !w2.PinnedNow() && !Prefs.Load().Pin);

        // --- big document: loads and scrolls fast, no exception, preview paused, footer counts off-thread
        var sb = new StringBuilder();
        int ln = 0;
        while (sb.Length < 5 * 1024 * 1024) { ln++; sb.Append("## line ").Append(ln).Append(" **bold** lorem ipsum dolor sit amet, 한글 문장도 섞어서 길게 씁니다 consectetur adipiscing elit\n"); }
        string big = sb.ToString();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Exception bigEx = null;
        try
        {
            w2.SetText(big); Pump(100);
            w2.ScrollEnd(); Pump(100);
            w2.ScrollToLine(ln / 2); Pump(100);
            w2.ScrollToLine(1); Pump(100);
        }
        catch (Exception ex) { bigEx = ex; }
        long ms = sw.ElapsedMilliseconds;
        Check("big 5MB doc loads + scrolls < 3s, no exception (" + ms + " ms)", bigEx == null && ms < 3000, bigEx == null ? ms + " ms" : bigEx.Message);
        Check("big doc text intact", w2.TextLen() == big.Length && w2.LineCountNow() == ln + 1, w2.TextLen() + " vs " + big.Length);
        Check("big doc syntax highlighting off", !w2.HighlightingOn());
        w2.ForcePreviewRefresh();
        Check("big doc preview paused note", w2.PreviewHtml().Contains("미리보기 일시 중지") && !w2.PreviewHtml().Contains("<h2>"), w2.PreviewHtml().Length.ToString());
        Pump(2500);
        Check("big doc footer counts computed (MB shown)", w2.GetStatusText().Contains("MB") && w2.Status.Chars > 4000000, w2.GetStatusText());
        sw.Restart();
        w2.SetCaret(0, 0); w2.Shortcut("Ctrl+Alt+3"); Pump(100);
        Check("big doc: heading edit is fast (" + sw.ElapsedMilliseconds + " ms)", sw.ElapsedMilliseconds < 2500 && w2.GetText().StartsWith("### line 1 "), sw.ElapsedMilliseconds.ToString());
        w2.SetText("# small"); Pump(400);
        Check("small doc again: highlighting back on", w2.HighlightingOn());
        w2.ForcePreviewRefresh();
        Check("small doc again: preview renders", w2.PreviewHtml().Contains("small") && !w2.PreviewHtml().Contains("일시 중지"));

        // --- recovery file
        string rmd = MainWindow.RecoveryMdPath, rjs = MainWindow.RecoveryJsonPath;
        if (File.Exists(rmd)) File.Delete(rmd);
        if (File.Exists(rjs)) File.Delete(rjs);
        w2.SaveForTest(Path.Combine(work, "pre.md"));
        w2.NewDocForTest();
        w2.SetText("미저장 내용 recover-me");
        w2.RecoveryTickForTest();
        Check("recovery.md written while dirty (inside sandbox)", File.Exists(rmd) && File.ReadAllText(rmd) == "미저장 내용 recover-me" && rmd.StartsWith(Path.GetFullPath(Path.Combine(work, "..", "profile"))), File.Exists(rmd) ? File.ReadAllText(rmd) : "missing");
        Check("recovery.json written", File.Exists(rjs));
        Check("recovery: no .tmp left behind", !File.Exists(rmd + ".tmp") && !File.Exists(rjs + ".tmp"));
        w2.SetText("미저장 내용 v2");
        w2.RecoveryTickForTest();
        Check("recovery.md updated on next tick", File.ReadAllText(rmd) == "미저장 내용 v2");
        string pr = Path.Combine(work, "recov.md");
        w2.SaveForTest(pr);
        Check("recovery removed after clean save", !File.Exists(rmd) && !File.Exists(rjs));
        w2.SetText("dirty again");
        w2.RecoveryTickForTest();
        Check("recovery.json records original path", File.Exists(rjs) && File.ReadAllText(rjs).Contains("recov.md"), File.Exists(rjs) ? File.ReadAllText(rjs) : "missing");
        w2.SetText(File.ReadAllText(pr));    // back to saved text -> clean
        w2.RecoveryTickForTest();
        Check("recovery removed when document is clean again", !File.Exists(rmd));

        // restore offer
        File.WriteAllText(rmd, "복구될 글", new UTF8Encoding(false));
        File.WriteAllText(rjs, "{\"Path\":\"\"}", new UTF8Encoding(false));
        MainWindow w3 = Program.CreateForTest();
        w3.Left = -3000; w3.Top = 0; w3.ShowInTaskbar = false; w3.Show(); Pump(300);
        w3.OfferRecoveryForTest(false);
        Check("recovery declined: text unchanged + file removed", w3.GetText().Contains("새 문서") && !File.Exists(rmd));
        File.WriteAllText(rmd, "복구될 글", new UTF8Encoding(false));
        w3.OfferRecoveryForTest(true);
        Check("recovery accepted: text restored and dirty", w3.GetText() == "복구될 글" && w3.IsDirty(), w3.GetText());
        w3.SetText("same text");
        File.WriteAllText(rmd, "same text", new UTF8Encoding(false));
        w3.OfferRecoveryForTest(true);
        Check("recovery equal to current: dropped without restore", w3.GetText() == "same text" && !File.Exists(rmd));

        w3.SetText("clean exit"); w3.SaveForTest(Path.Combine(work, "exit.md"));
        w3.Close(); w2.SetText("x"); w2.SaveForTest(Path.Combine(work, "exit2.md")); w2.Close(); Pump(200);

    }
}
