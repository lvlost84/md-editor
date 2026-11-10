using System;
using System.Text;
using System.Windows;

// 줄 번호 거터 / 파일 크기 표시 검사
static partial class SelfTest
{
    static void GutterTests(string work)
    {
        // 크기 표기
        Check("size 0 B", SizeFmt.Format(0) == "0 B", SizeFmt.Format(0));
        Check("size 1023 B", SizeFmt.Format(1023) == "1023 B", SizeFmt.Format(1023));
        Check("size 1 KB boundary", SizeFmt.Format(1024) == "1.0 KB", SizeFmt.Format(1024));
        Check("size 12.5 KB", SizeFmt.Format(12800) == "12.5 KB", SizeFmt.Format(12800));
        Check("size 1 MB boundary", SizeFmt.Format(1048576) == "1.00 MB", SizeFmt.Format(1048576));

        MainWindow w = Program.CreateForTest();
        w.Left = -3000; w.Top = 0; w.ShowInTaskbar = false;
        w.Show();
        Pump(500);

        w.SetText("a\r\nb\r\n\r\nd");
        Pump(400);
        Check("gutter line count 4", w.GutterLineCount() == 4, w.GutterLineCount().ToString());
        Check("gutter LineOf start", w.GutterLineOf(0) == 1);
        Check("gutter LineOf second line", w.GutterLineOf(3) == 2);                 // 'b'
        Check("gutter LineOf empty line", w.GutterLineOf(6) == 3, w.GutterLineOf(6).ToString());
        Check("gutter LineOf last line", w.GutterLineOf(8) == 4, w.GutterLineOf(8).ToString());
        Check("gutter LineOf at very end", w.GutterLineOf(9) == 4, w.GutterLineOf(9).ToString());
        w.SetText("");
        Pump(300);
        Check("gutter empty doc = 1 line", w.GutterLineCount() == 1);
        w.SetText("가나다\n");
        Pump(300);
        Check("gutter trailing newline adds empty last line", w.GutterLineCount() == 2);
        Check("footer shows line count", w.GetStatusText().Contains("2줄"), w.GetStatusText());
        Check("footer shows size (UTF-8, LF)", w.GetStatusText().Contains("10 B"), w.GetStatusText());   // 가나다=9 + \n

        // 1000줄이면 자릿수 증가로 거터가 넓어진다
        w.SetText("x");
        Pump(300);
        double w1 = w.GutterWidth();
        w.SetText(new string('\n', 1500));
        Pump(500);
        Check("gutter widens for 4-digit line numbers", w.GutterWidth() > w1, w1 + " -> " + w.GutterWidth());
        Check("gutter width positive", w1 > 20, w1.ToString());

        // 회귀: 편집창에 줄 높이를 고정하면 긴 문서(679줄 이후)에서 번호가 사라지던 문제.
        // 스크롤해도, 맨 끝으로 가도, 줄바꿈이 많아도 보이는 줄에는 번호가 그려져야 하고 맨 위 번호가 실제 맨 위 줄이어야 한다.
        var big = new StringBuilder();
        for (int i = 1; i <= 2000; i++) big.Append("line " + i + "\r\n");
        w.SetText(big.ToString()); Pump(600);
        foreach (int ln in new[] { 0, 300, 700, 1000, 1500, 1990 })
        {
            w.ScrollToLine(ln); Pump(400);
            Check("gutter numbers drawn after scrolling to line " + ln,
                w.GutterDrawn() >= (ln > 1980 ? 8 : 20) && Math.Abs(w.GutterFirstNo() - w.GutterTopLogical()) <= 1, w.GutterInfo());
        }
        w.ScrollEnd(); Pump(500);
        Check("gutter numbers drawn at document end", w.GutterDrawn() >= 8 && Math.Abs(w.GutterFirstNo() - w.GutterTopLogical()) <= 1, w.GutterInfo());

        var wr = new StringBuilder();
        for (int i = 1; i <= 1500; i++) wr.Append("L" + i + " " + (i % 3 == 0 ? string.Concat(System.Linq.Enumerable.Repeat("wrapped text ", 25)) : "short") + "\r\n");
        string wrText = wr.ToString();
        w.SetText(wrText); Pump(800);
        w.ScrollEnd(); Pump(800);
        Check("gutter (wrapped lines) drawn at end", w.GutterDrawn() >= 5 && Math.Abs(w.GutterFirstNo() - w.GutterTopLogical()) <= 1, w.GutterInfo());
        foreach (int target in new[] { 700, 1200 })
        {
            w.SetCaret(wrText.IndexOf("L" + target + " ", StringComparison.Ordinal), 0); Pump(800);
            Check("gutter (wrapped lines) drawn with caret in line " + target, w.GutterDrawn() >= 5 && Math.Abs(w.GutterFirstNo() - w.GutterTopLogical()) <= 1, w.GutterInfo());
        }

        var lines = new StringBuilder();
        for (int i = 1; i <= 40; i++) lines.Append(i == 5 ? string.Concat(System.Linq.Enumerable.Repeat("긴 줄입니다. ", 30)) : "줄 " + i + " 내용 **강조**").Append("\n");
        w.SetText(lines.ToString()); Pump(500);
        w.SetCaret(40, 0); Pump(300);
        w.RenderToPng(System.IO.Path.Combine(work, "gutter.png"));
        Check("gutter png written", System.IO.File.Exists(System.IO.Path.Combine(work, "gutter.png")));
    }
}
