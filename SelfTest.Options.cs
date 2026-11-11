using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

// 옵션 창: Prefs 범위 확인, 시작 시 적용, 각 컨트롤 -> 편집기 반영 + view.json 저장, 본창 변경 -> 창 동기화, 기본값 복원.
static partial class SelfTest
{
    static void OptionsTests(string work)
    {
        // ---- Prefs 로드: 범위 밖/이상한 값은 기본값
        File.WriteAllText(Prefs.PathName, "{\"IndentSize\":99,\"AutosaveSec\":0,\"FontName\":\"a,b<\",\"ShowLineNumbers\":false}");
        var p = Prefs.Load();
        Check("opt prefs: bad indent/autosave/font -> defaults, valid field kept", p.IndentSize == 2 && p.AutosaveSec == 20 && p.FontName == "" && !p.ShowLineNumbers, p.IndentSize + "/" + p.AutosaveSec + "/" + p.FontName + "/" + p.ShowLineNumbers);
        File.WriteAllText(Prefs.PathName, "{\"FontName\":null,\"IndentSize\":4,\"AutosaveSec\":600}");
        p = Prefs.Load();
        Check("opt prefs: null font ok, boundary values kept", p.FontName == "" && p.IndentSize == 4 && p.AutosaveSec == 600);
        File.WriteAllText(Prefs.PathName, "{\"View\":1}");
        p = Prefs.Load();
        Check("opt prefs: old view.json (no option fields) -> defaults", p.ShowLineNumbers && p.WordWrap && p.HighlightLine && p.Highlight && p.IndentSize == 2 && p.AutosaveSec == 20);

        // ---- 시작 시 저장된 옵션이 적용된다
        File.WriteAllText(Prefs.PathName, "{\"ShowLineNumbers\":false,\"WordWrap\":false,\"HighlightLine\":false,\"Highlight\":false,\"IndentSize\":4,\"AutosaveSec\":60}");
        var w = Program.CreateForTest();
        w.Left = -3000; w.Top = 0; w.ShowInTaskbar = false;
        w.Show(); Pump(500);
        Check("opt startup: line numbers off", !w.LineNumbersShown());
        Check("opt startup: wrap off -> horizontal scroll available", !w.WordWrapOn() && w.HScrollNow() == Visibility.Visible);
        Check("opt startup: current-line highlight off", !w.CurrentLineHighlightNow());
        Check("opt startup: syntax colors off", !w.HighlightingOn());
        Check("opt startup: indent 4, autosave 60s", w.IndentationSizeNow() == 4 && w.AutosaveIntervalNow() == 60, w.IndentationSizeNow() + "/" + w.AutosaveIntervalNow());
        Check("opt startup: placeholder moves left without line numbers", w.PlaceholderLeftNow() == 12, w.PlaceholderLeftNow().ToString());
        w.Close(); Pump(200);
        try { File.Delete(Prefs.PathName); File.Delete(Settings.PathName); } catch { }

        // ---- 옵션 창 조작
        w = Program.CreateForTest();
        w.Left = -3000; w.Top = 0; w.ShowInTaskbar = false;
        w.Show(); Pump(500);
        Check("opt: Ctrl+, opens options window", w.Shortcut("Ctrl+,") && w.OptWinForTest != null);
        var ow = w.OptWinForTest;
        w.ShowOptionsForTest();
        Check("opt: second open reuses the same window", ReferenceEquals(ow, w.OptWinForTest));
        Check("opt: window shows defaults", ow.LineNoSw.IsOn && ow.WrapSw.IsOn && ow.CurLineSw.IsOn && ow.HiliteSw.IsOn && !ow.PinSw.IsOn && ow.ThemeSeg.Selected == 0 && ow.ViewSeg.Selected == 1 && ow.IndentSeg.Selected == 0 && ow.AutosaveSeg.Selected == 1 && ow.FontSeg.Selected == 0,
            ow.LineNoSw.IsOn + " " + ow.WrapSw.IsOn + " " + ow.IndentSeg.Selected + " " + ow.AutosaveSeg.Selected);
        Check("opt: size label shows 14.5 pt", ow.SizeLabel == "14.5 pt", ow.SizeLabel);

        ow.LineNoSw.Set(false, true); Pump(100);
        Check("opt: line numbers switch -> editor + saved", !w.LineNumbersShown() && !Prefs.Load().ShowLineNumbers && w.PlaceholderLeftNow() == 12);
        ow.LineNoSw.Set(true, true); Pump(100);
        Check("opt: line numbers back on", w.LineNumbersShown() && Prefs.Load().ShowLineNumbers && w.PlaceholderLeftNow() == 52);

        ow.WrapSw.Set(false, true); Pump(100);
        Check("opt: wrap off -> editor + saved", !w.WordWrapOn() && w.HScrollNow() == Visibility.Visible && !Prefs.Load().WordWrap);
        ow.WrapSw.Set(true, true); Pump(100);
        Check("opt: wrap on", w.WordWrapOn() && w.HScrollNow() == Visibility.Collapsed);

        ow.CurLineSw.Set(false, true); Pump(100);
        Check("opt: current line highlight off + saved", !w.CurrentLineHighlightNow() && !Prefs.Load().HighlightLine);
        ow.CurLineSw.Set(true, true);

        ow.HiliteSw.Set(false, true); Pump(100);
        Check("opt: syntax colors off + saved", !w.HighlightingOn() && !Prefs.Load().Highlight);
        ow.HiliteSw.Set(true, true); Pump(100);
        Check("opt: syntax colors back on", w.HighlightingOn() && Prefs.Load().Highlight);

        ow.IndentSeg.Pick(1, true); Pump(100);
        w.SetText("a\nb"); w.SetCaret(0, 3);
        w.Shortcut("Tab"); Pump(100);
        Check("opt: indent 4 -> Tab indents selected lines by 4 spaces, saved", w.GetText() == "    a\n    b" && w.IndentationSizeNow() == 4 && Prefs.Load().IndentSize == 4, "'" + w.GetText() + "'");
        ow.IndentSeg.Pick(0, true);
        Check("opt: indent back to 2", w.IndentationSizeNow() == 2 && Prefs.Load().IndentSize == 2);

        ow.AutosaveSeg.Pick(0, true);
        Check("opt: autosave 10s applied to timer + saved", w.AutosaveIntervalNow() == 10 && Prefs.Load().AutosaveSec == 10, w.AutosaveIntervalNow().ToString());
        ow.AutosaveSeg.Pick(3, true);
        Check("opt: autosave 2min", w.AutosaveIntervalNow() == 120 && Prefs.Load().AutosaveSec == 120);

        if (ow.FontSeg.Count > 1)
        {
            ow.FontSeg.Pick(1, true); Pump(100);
            string fn = ow.FontNames[1];
            Check("opt: font choice -> editor font + saved", w.EditorFontNow().StartsWith(fn) && Prefs.Load().FontName == fn, w.EditorFontNow());
            ow.FontSeg.Pick(0, true);
            Check("opt: font default restored", w.EditorFontNow().StartsWith("Cascadia Mono") && Prefs.Load().FontName == "");
        }
        else Check("opt: font choice (only default available, skipped)", true);

        w.ZoomForTest(1); Pump(50);
        Check("opt: zoom from main window updates the size label", ow.SizeLabel == "15.5 pt", ow.SizeLabel);
        w.OptFontStep(-1); w.OptFontStep(-1);
        Check("opt: stepper changes size and saves", w.FontSizeNow() == 13.5 && ow.SizeLabel == "13.5 pt" && Prefs.Load().FontSize == 13.5, ow.SizeLabel);
        w.OptFontReset();
        Check("opt: size reset", w.FontSizeNow() == 14.5 && ow.SizeLabel == "14.5 pt");

        ow.ThemeSeg.Pick(1, true); Pump(200);
        Check("opt: theme seg -> dark", Theme.IsDark && Settings.Load().Dark);
        w.Shortcut("Ctrl+Alt+D"); Pump(200);
        Check("opt: Ctrl+Alt+D in main window moves the theme seg (sync)", !Theme.IsDark && ow.ThemeSeg.Selected == 0);

        ow.ViewSeg.Pick(2, true); Pump(200);
        Check("opt: view seg -> preview only + saved", w.ViewModeNow() == 2 && Prefs.Load().View == 2);
        w.Shortcut("Ctrl+Shift+V"); Pump(100);
        Check("opt: view cycle in main window moves the view seg", w.ViewModeNow() == 0 && ow.ViewSeg.Selected == 0);
        ow.ViewSeg.Pick(1, true);

        ow.PinSw.Set(true, true); Pump(100);
        Check("opt: pin switch -> Topmost + saved", w.PinnedNow() && Prefs.Load().Pin);
        w.Shortcut("Ctrl+Alt+P"); Pump(100);
        Check("opt: pin shortcut moves the switch (sync)", !w.PinnedNow() && !ow.PinSw.IsOn);

        // 기본값으로: 편집/저장 옵션만 되돌리고 보기·글자 크기는 유지
        ow.WrapSw.Set(false, true); ow.IndentSeg.Pick(2, true); ow.AutosaveSeg.Pick(0, true); ow.LineNoSw.Set(false, true);
        w.ZoomForTest(2); Pump(50);
        w.OptResetAll(); Pump(100);
        var r = Prefs.Load();
        Check("opt: reset restores editor/save options", w.WordWrapOn() && w.LineNumbersShown() && w.IndentationSizeNow() == 2 && w.AutosaveIntervalNow() == 20 && r.WordWrap && r.ShowLineNumbers && r.IndentSize == 2 && r.AutosaveSec == 20);
        Check("opt: reset keeps font size", w.FontSizeNow() == 16.5, w.FontSizeNow().ToString());
        Check("opt: reset syncs window controls", ow.WrapSw.IsOn && ow.LineNoSw.IsOn && ow.IndentSeg.Selected == 0 && ow.AutosaveSeg.Selected == 1);

        ow.Close(); Pump(200);
        Check("opt: closing clears the singleton and reopening works", w.OptWinForTest == null && w.Shortcut("Ctrl+,") && w.OptWinForTest != null && !ReferenceEquals(ow, w.OptWinForTest));
        w.OptWinForTest.Close(); Pump(100);

        Check("opt: shortcut listed in help table and tooltips", Shortcuts.Get("options") != null && Shortcuts.Tip("옵션", "options") == "옵션 (Ctrl+,)", Shortcuts.Tip("옵션", "options"));
        Check("opt: gear icon exists", Array.IndexOf(Icons.Names.ToArray(), "settings") >= 0);

        w.Close(); Pump(200);
        try { File.Delete(Prefs.PathName); File.Delete(Settings.PathName); } catch { }
    }
}
