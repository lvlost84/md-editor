using System;
using System.IO;
using System.Text;
using System.Windows;

// 파일 크기 표시 + 편집기(AvalonEdit) 검사의 진입점. (줄 번호 거터 자체 구현은 AvalonEdit 내장으로 대체되어 삭제됨)
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
        Check("line count 4 (CRLF)", w.LineCountNow() == 4, w.LineCountNow().ToString());
        w.SetText("");
        Pump(300);
        Check("empty doc = 1 line", w.LineCountNow() == 1);
        w.SetText("가나다\n");
        Pump(300);
        Check("trailing newline adds empty last line", w.LineCountNow() == 2);
        Check("footer shows line count", w.GetStatusText().Contains("2줄"), w.GetStatusText());
        Check("footer shows size (UTF-8, LF)", w.GetStatusText().Contains("10 B"), w.GetStatusText());   // 가나다=9 + \n
        w.SaveForTest(Path.Combine(work, "gut.md"));
        w.Close();
        Pump(200);

        EditorTests(work);
    }
}
