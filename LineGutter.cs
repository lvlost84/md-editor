using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

// 코드 에디터식 줄 번호. 자동 줄바꿈된 줄은 번호 없이 이어지고, 보이는 줄만 그린다. 커서가 있는 줄 번호는 강조.
class LineGutter : FrameworkElement
{
    public static readonly DependencyProperty BackBrushProperty = DependencyProperty.Register("BackBrush", typeof(Brush), typeof(LineGutter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FgBrushProperty = DependencyProperty.Register("FgBrush", typeof(Brush), typeof(LineGutter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CurBrushProperty = DependencyProperty.Register("CurBrush", typeof(Brush), typeof(LineGutter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    readonly TextBox editor;
    readonly Typeface face = new Typeface("Consolas");
    readonly double fontSize = 13;
    int[] nl = new int[0];          // '\n' 위치 목록 (논리 줄 시작 계산용)
    int digits = 3;

    public LineGutter(TextBox editor)
    {
        this.editor = editor;
        SetResourceReference(BackBrushProperty, "AccentSoftBrush");
        SetResourceReference(FgBrushProperty, "InkSoftBrush");
        SetResourceReference(CurBrushProperty, "AccentBrush");
        IsHitTestVisible = false; ClipToBounds = true;
        editor.TextChanged += (s, e) => Rebuild();
        editor.SelectionChanged += (s, e) => InvalidateVisual();
        editor.SizeChanged += (s, e) => InvalidateVisual();
        editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) => InvalidateVisual()));
        Rebuild();
    }

    public int LineCount { get { return nl.Length + 1; } }

    void Rebuild()
    {
        string t = editor.Text ?? "";
        var l = new List<int>();
        for (int i = 0; i < t.Length; i++) if (t[i] == '\n') l.Add(i);
        nl = l.ToArray();
        int d = Math.Max(3, LineCount.ToString(CultureInfo.InvariantCulture).Length);
        if (d != digits) { digits = d; InvalidateMeasure(); }
        InvalidateVisual();
    }

    // charIndex가 속한 논리 줄 번호(1부터)
    public int LineOf(int charIndex)
    {
        int lo = 0, hi = nl.Length;   // nl 중 charIndex보다 앞선 개수
        while (lo < hi) { int m = (lo + hi) / 2; if (nl[m] < charIndex) lo = m + 1; else hi = m; }
        return lo + 1;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(Math.Ceiling(digits * fontSize * 0.55) + 18, 0);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var back = (Brush)GetValue(BackBrushProperty);
        if (back != null) dc.DrawRectangle(back, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var fg = (Brush)GetValue(FgBrushProperty) ?? Brushes.Gray;
        var cur = (Brush)GetValue(CurBrushProperty) ?? fg;
        try
        {
            int first = editor.GetFirstVisibleLineIndex(), last = editor.GetLastVisibleLineIndex();
            if (first < 0 || last < first) return;
            string t = editor.Text ?? "";
            int curLine = LineOf(editor.CaretIndex);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            for (int i = first; i <= last; i++)
            {
                int ci = editor.GetCharacterIndexFromLineIndex(i);
                if (ci < 0) continue;
                if (ci > 0 && ci <= t.Length && t[ci - 1] != '\n') continue;   // 줄바꿈으로 이어진 줄
                Rect r = editor.GetRectFromCharacterIndex(ci);
                if (r.IsEmpty || double.IsInfinity(r.Top)) continue;
                int no = LineOf(ci);
                var ft = new FormattedText(no.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    face, fontSize, no == curLine ? cur : fg, dpi);
                if (no == curLine) ft.SetFontWeight(FontWeights.Bold);
                double y = r.Top + (r.Height - ft.Height) / 2;
                dc.DrawText(ft, new Point(ActualWidth - 10 - ft.Width, y));
            }
        }
        catch { }
    }
}
