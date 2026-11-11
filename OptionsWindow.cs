using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// 옵션 창 (Ctrl+,): 바꾸는 즉시 적용되고 자동 저장된다 (확인/취소 단계 없음). 색은 모두 테마 토큰을 따른다.

// 켜짐/꺼짐 스위치
class OptSwitch : Border
{
    readonly Border knob;
    bool on;
    public event Action<bool> Toggled;
    public bool IsOn { get { return on; } }

    public OptSwitch(string name)
    {
        Width = 38; Height = 22; CornerRadius = new CornerRadius(11); Cursor = Cursors.Hand; Focusable = true;
        FocusVisualStyle = null; VerticalAlignment = VerticalAlignment.Center; BorderThickness = new Thickness(1);
        System.Windows.Automation.AutomationProperties.SetName(this, name);
        knob = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Margin = new Thickness(2, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };
        Theme.Bind(knob, BackgroundProperty, "Paper");
        Child = knob;
        MouseLeftButtonUp += (s, e) => Flip();
        KeyDown += (s, e) => { if (e.Key == Key.Space || e.Key == Key.Enter) { Flip(); e.Handled = true; } };
        GotKeyboardFocus += (s, e) => Paint();
        LostKeyboardFocus += (s, e) => Paint();
        Paint();
    }

    void Flip() { Set(!on, true); }

    public void Set(bool v, bool raise)
    {
        if (on == v && !raise) { Paint(); return; }
        on = v; Paint();
        if (raise && Toggled != null) Toggled(on);
    }

    void Paint()
    {
        Theme.Bind(this, BackgroundProperty, on ? "Accent" : "BorderStrong");
        Theme.Bind(this, BorderBrushProperty, IsKeyboardFocused ? "Accent" : (on ? "Accent" : "BorderStrong"));
        knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        System.Windows.Automation.AutomationProperties.SetHelpText(this, on ? "켜짐" : "꺼짐");
    }
}

// 여러 개 중 하나 고르기 (칸막이 버튼)
class OptSeg : Border
{
    readonly List<Border> items = new List<Border>();
    readonly List<TextBlock> labels = new List<TextBlock>();
    int sel = -1;
    public event Action<int> Picked;
    public int Selected { get { return sel; } }
    public int Count { get { return items.Count; } }

    public OptSeg(string name, params string[] texts)
    {
        BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(8); Padding = new Thickness(2);
        Focusable = true; FocusVisualStyle = null; VerticalAlignment = VerticalAlignment.Center;
        Theme.Bind(this, BackgroundProperty, "Bg"); Theme.Bind(this, BorderBrushProperty, "Border");
        System.Windows.Automation.AutomationProperties.SetName(this, name);
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < texts.Length; i++)
        {
            int idx = i;
            var t = new TextBlock { Text = texts[i], FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
            var b = new Border { Child = t, CornerRadius = new CornerRadius(6), Padding = new Thickness(11, 4, 11, 4), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            b.MouseLeftButtonUp += (s, e) => Pick(idx, true);
            b.MouseEnter += (s, e) => { if (idx != sel) Theme.Bind(b, BackgroundProperty, "Hover"); };
            b.MouseLeave += (s, e) => { if (idx != sel) b.Background = Brushes.Transparent; };
            items.Add(b); labels.Add(t); sp.Children.Add(b);
        }
        Child = sp;
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Left && sel > 0) { Pick(sel - 1, true); e.Handled = true; }
            else if (e.Key == Key.Right && sel < items.Count - 1) { Pick(sel + 1, true); e.Handled = true; }
        };
        GotKeyboardFocus += (s, e) => Theme.Bind(this, BorderBrushProperty, "Accent");
        LostKeyboardFocus += (s, e) => Theme.Bind(this, BorderBrushProperty, "Border");
        Paint();
    }

    public void Pick(int i, bool raise)
    {
        if (i < 0 || i >= items.Count) i = -1;
        bool changed = i != sel;
        sel = i; Paint();
        if (raise && changed && Picked != null) Picked(i);
    }

    void Paint()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (i == sel) { Theme.Bind(items[i], BackgroundProperty, "AccentSoft"); Theme.Bind(labels[i], TextBlock.ForegroundProperty, "Accent"); labels[i].FontWeight = FontWeights.SemiBold; }
            else { items[i].Background = Brushes.Transparent; Theme.Bind(labels[i], TextBlock.ForegroundProperty, "InkSoft"); labels[i].FontWeight = FontWeights.Normal; }
        }
    }
}

class OptionsWindow : Window
{
    readonly MainWindow main;
    OptSeg theme, view, font, indent, autosave;
    OptSwitch pin, lineNo, wrap, curLine, hilite;
    TextBlock sizeText;
    bool syncing;

    // 선택지 (저장 값)
    static readonly int[] IndentVals = { 2, 4, 8 };
    static readonly int[] AutoVals = { 10, 20, 60, 120 };
    internal readonly List<string> FontNames = new List<string>();   // "" = 기본

    public OptionsWindow(MainWindow owner)
    {
        main = owner;
        Owner = owner; Title = "옵션"; Width = 540; SizeToContent = SizeToContent.Height; MaxHeight = 760;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Icon = owner.Icon;
        Theme.Bind(this, BackgroundProperty, "Paper"); Theme.Bind(this, ForegroundProperty, "Ink");
        FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
        PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };

        var root = new DockPanel();

        // 아래 단추 줄
        var foot = new Border { Padding = new Thickness(20, 12, 20, 14), BorderThickness = new Thickness(0, 1, 0, 0) };
        Theme.Bind(foot, Border.BackgroundProperty, "Bg"); Theme.Bind(foot, Border.BorderBrushProperty, "Border");
        var fd = new DockPanel();
        var close = new Button { Content = "닫기", MinWidth = 76, Margin = new Thickness(0), IsDefault = true, IsCancel = true };
        close.SetResourceReference(StyleProperty, "PrimaryBtn");
        DockPanel.SetDock(close, Dock.Right); fd.Children.Add(close);
        var reset = new Button { Content = "편집 옵션 기본값으로", Margin = new Thickness(0, 0, 8, 0) };
        reset.SetResourceReference(StyleProperty, "TbBtn");
        reset.Click += (s, e) => { main.OptResetAll(); Sync(); };
        var folder = new Button { Content = "설정 폴더 열기" };
        folder.SetResourceReference(StyleProperty, "TbBtn");
        folder.Click += (s, e) => main.OptOpenFolder();
        var lp = new StackPanel { Orientation = Orientation.Horizontal };
        lp.Children.Add(reset); lp.Children.Add(folder);
        fd.Children.Add(lp);
        foot.Child = fd;
        DockPanel.SetDock(foot, Dock.Bottom); root.Children.Add(foot);

        var body = new StackPanel { Margin = new Thickness(22, 16, 22, 8) };

        // ---- 화면
        body.Children.Add(Section("화면"));
        theme = new OptSeg("테마", "라이트", "다크");
        theme.Picked += i => { if (!syncing) main.OptSetDark(i == 1); };
        body.Children.Add(Row("테마", "Ctrl+Alt+D 로도 바꿀 수 있습니다", theme));
        view = new OptSeg("보기 모드", "편집", "분할", "미리보기");
        view.Picked += i => { if (!syncing) main.OptSetView(i); };
        body.Children.Add(Row("보기 모드", "다음에 열 때도 기억합니다", view));
        pin = new OptSwitch("항상 위");
        pin.Toggled += v => { if (!syncing) main.OptSetPin(v); };
        body.Children.Add(Row("항상 위", "다른 창 위에 계속 띄워 둡니다", pin));

        // ---- 편집기
        body.Children.Add(Section("편집기"));
        var names = new List<string>(); var labels = new List<string>();
        FontNames.Add(""); labels.Add("기본");
        foreach (var f in new[] { "Consolas", "D2Coding", "Malgun Gothic", "Courier New" })
        {
            if (!MainWindow.FontInstalled(f)) continue;
            FontNames.Add(f); labels.Add(f == "Malgun Gothic" ? "맑은 고딕" : f);
        }
        font = new OptSeg("편집 글꼴", labels.ToArray());
        font.Picked += i => { if (!syncing) main.OptSetFontName(FontNames[i]); };
        body.Children.Add(Row("글꼴", "기본 = Cascadia Mono", font));

        var stepper = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var minus = StepBtn("−", "글자 작게"); var plus = StepBtn("+", "글자 크게");
        sizeText = new TextBlock { Width = 64, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
        Theme.Bind(sizeText, TextBlock.ForegroundProperty, "Ink");
        minus.Click += (s, e) => main.OptFontStep(-1);
        plus.Click += (s, e) => main.OptFontStep(1);
        sizeText.MouseLeftButtonUp += (s, e) => main.OptFontReset();
        sizeText.Cursor = Cursors.Hand; sizeText.ToolTip = "클릭하면 기본 크기 (Ctrl+0)";
        stepper.Children.Add(minus); stepper.Children.Add(sizeText); stepper.Children.Add(plus);
        body.Children.Add(Row("글자 크기", "Ctrl+마우스 휠로도 조절 (8~40pt)", stepper));

        lineNo = new OptSwitch("줄 번호"); lineNo.Toggled += v => { if (!syncing) main.OptSetLineNumbers(v); };
        body.Children.Add(Row("줄 번호 표시", null, lineNo));
        wrap = new OptSwitch("자동 줄바꿈"); wrap.Toggled += v => { if (!syncing) main.OptSetWrap(v); };
        body.Children.Add(Row("자동 줄바꿈", "끄면 긴 줄은 가로로 스크롤합니다", wrap));
        curLine = new OptSwitch("현재 줄 강조"); curLine.Toggled += v => { if (!syncing) main.OptSetCurLine(v); };
        body.Children.Add(Row("현재 줄 강조", null, curLine));
        hilite = new OptSwitch("마크다운 색 표시"); hilite.Toggled += v => { if (!syncing) main.OptSetHighlight(v); };
        body.Children.Add(Row("마크다운 색 표시", "제목·굵게·링크 등에 색을 입힙니다 (200만 글자 넘으면 자동으로 끔)", hilite));
        indent = new OptSeg("들여쓰기 칸 수", "2칸", "4칸", "8칸");
        indent.Picked += i => { if (!syncing) main.OptSetIndent(IndentVals[i]); };
        body.Children.Add(Row("들여쓰기", "Tab / Shift+Tab 으로 선택한 줄을 밀거나 당길 때", indent));

        // ---- 저장
        body.Children.Add(Section("저장"));
        autosave = new OptSeg("자동 임시 저장 간격", "10초", "20초", "1분", "2분");
        autosave.Picked += i => { if (!syncing) main.OptSetAutosave(AutoVals[i]); };
        body.Children.Add(Row("자동 임시 저장", "수정 중인 글을 이 간격으로 복구 파일에 저장합니다 (정상 저장과는 별개)", autosave));

        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body, Focusable = false };
        root.Children.Add(sv);
        Content = root;
        Sync();
    }

    Button StepBtn(string t, string tip)
    {
        var b = new Button { Content = t, ToolTip = tip, Width = 30, Padding = new Thickness(0, 3, 0, 3), Margin = new Thickness(0), FontSize = 15 };
        b.SetResourceReference(StyleProperty, "TbBtn");
        System.Windows.Automation.AutomationProperties.SetName(b, tip);
        return b;
    }

    UIElement Section(string text)
    {
        var t = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 6) };
        Theme.Bind(t, TextBlock.ForegroundProperty, "Accent");
        var b = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 4), Margin = new Thickness(0, 0, 0, 4), Child = t };
        Theme.Bind(b, Border.BorderBrushProperty, "Border");
        return b;
    }

    UIElement Row(string label, string hint, UIElement control)
    {
        var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        var l = new TextBlock { Text = label, FontSize = 13.5 };
        Theme.Bind(l, TextBlock.ForegroundProperty, "Ink");
        sp.Children.Add(l);
        if (hint != null)
        {
            var h = new TextBlock { Text = hint, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            Theme.Bind(h, TextBlock.ForegroundProperty, "InkSoft");
            sp.Children.Add(h);
        }
        g.Children.Add(sp);
        Grid.SetColumn((FrameworkElement)control, 1);
        g.Children.Add(control);
        return g;
    }

    // 본창의 현재 값을 화면에 맞춘다 (본창에서 단축키로 바뀐 것도 따라온다)
    public void Sync()
    {
        syncing = true;
        try
        {
            theme.Pick(Theme.IsDark ? 1 : 0, false);
            view.Pick(main.OptView, false);
            pin.Set(main.OptPin, false);
            int fi = FontNames.IndexOf(main.OptPrefs.FontName); font.Pick(fi < 0 ? 0 : fi, false);
            sizeText.Text = main.OptFontSize.ToString("0.#") + " pt";
            lineNo.Set(main.OptPrefs.ShowLineNumbers, false);
            wrap.Set(main.OptPrefs.WordWrap, false);
            curLine.Set(main.OptPrefs.HighlightLine, false);
            hilite.Set(main.OptPrefs.Highlight, false);
            indent.Pick(Array.IndexOf(IndentVals, main.OptPrefs.IndentSize), false);
            autosave.Pick(Array.IndexOf(AutoVals, main.OptPrefs.AutosaveSec), false);
        }
        finally { syncing = false; }
    }

    // ---- 테스트 훅 (화면 클릭 대신 컨트롤 직접 조작)
    internal OptSeg ThemeSeg { get { return theme; } }
    internal OptSeg ViewSeg { get { return view; } }
    internal OptSeg FontSeg { get { return font; } }
    internal OptSeg IndentSeg { get { return indent; } }
    internal OptSeg AutosaveSeg { get { return autosave; } }
    internal OptSwitch PinSw { get { return pin; } }
    internal OptSwitch LineNoSw { get { return lineNo; } }
    internal OptSwitch WrapSw { get { return wrap; } }
    internal OptSwitch CurLineSw { get { return curLine; } }
    internal OptSwitch HiliteSw { get { return hilite; } }
    internal string SizeLabel { get { return sizeText.Text; } }
}
