using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;

// UI construction (code-only). Feature logic lives in the other MainWindow.*.cs files.
partial class MainWindow : Window
{
    internal TextEditor editor;
    internal TextBox fileNameBox;
    TextBlock placeholder, footDoc, footDate, footRev, footCount, footStatus, footCaret, footZoom, footMode, darkLabel;
    Button darkBtn;
    Grid paneGrid;
    UIElement leftPane, rightPane;
    GridSplitter splitter;
    readonly List<KeyValuePair<string, Action>> toolbar = new List<KeyValuePair<string, Action>>();
    Settings settings;
    Prefs prefs;

    const string SampleText =
        "## 새 문서\n\n" +
        "이 에디터는 왼쪽에서 마크다운을 입력하면 오른쪽 미리보기에 바로 반영됩니다. 파일은 내 컴퓨터에만 저장됩니다.\n\n" +
        "- 위쪽 도구 막대로 제목, 굵게, 목록, 표를 쉽게 넣을 수 있습니다\n" +
        "- Ctrl+S로 저장하고 Ctrl+O로 파일을 엽니다\n" +
        "- 다크모드와 라이트모드를 전환할 수 있습니다\n" +
        "- 각주는 [^1]처럼 적고 아래에 설명을 달면 됩니다\n\n" +
        "| 항목 | 결과 |\n| --- | --- |\n| 예시1 | 123 |\n| 예시2 | 456 |\n\n" +
        "[^1]: 이렇게 문서 하단에 각주 설명을 적으면 미리보기에 자동으로 번호와 함께 정리됩니다.";

    public MainWindow()
    {
        Theme.Install();
        settings = Settings.Load();
        prefs = Prefs.Load();
        Theme.Apply(settings.Dark);
        Title = AppInfo.Title;
        Icon = AppInfo.LoadIcon();
        Width = Math.Max(900, settings.Width); Height = Math.Max(600, settings.Height);
        MinWidth = 900; MinHeight = 600;
        FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
        Theme.Bind(this, BackgroundProperty, "Bg");
        Theme.Bind(this, ForegroundProperty, "Ink");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = BuildHeader(); Grid.SetRow(header, 0); root.Children.Add(header);
        var panes = BuildPanes(); Grid.SetRow(panes, 1); root.Children.Add(panes);
        var footer = BuildFooter(); Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = root;

        SetupPreview();
        SetupFiles();
        SetupEditor();
        SetEditorTextNoUndo(SampleText);
        savedText = editor.Text;
        UpdateDirtyUi();
        UpdateFooter();
        darkLabel.Text = Theme.IsDark ? "라이트모드" : "다크모드";
        SetThemeIcon();
        editor.TextChanged += OnEditorChanged;
        ApplyBindings();
        ApplyViewMode(false);
        Topmost = prefs.Pin;
        UpdateModeUi();
        SetupRecovery();
        Closing += OnClosing;
        Closed += (s, e) => { SaveSettings(); StopBackground(); };
    }

    // ---------------------------------------------------------------- 화면 구성 (헤더 / 본문 / 상태바)
    Button Btn(string label, Action act, string style = "TbBtn", string tip = null)
    {
        var b = new Button { Content = label };
        b.SetResourceReference(StyleProperty, style);
        b.Click += (s, e) => act();
        if (tip != null) { b.ToolTip = tip; System.Windows.Automation.AutomationProperties.SetName(b, tip); }
        return b;
    }

    // 아이콘 버튼: 이름과 단축키는 툴팁(+접근성 이름)으로 보여 준다
    Button IconBtn(string icon, Action act, string tip)
    {
        var b = new Button { Content = Icons.Make(icon, 18), ToolTip = tip };
        b.SetResourceReference(StyleProperty, "IconBtn");
        System.Windows.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (s, e) => act();
        return b;
    }

    // 도구 모음에 등록되는 버튼(테스트/단축키 연동용 label 키). icon==null 이면 글자 버튼.
    Button Tb(string label, string icon, string glyph, Action act, string tip)
    {
        toolbar.Add(new KeyValuePair<string, Action>(label, act));
        if (icon != null) return IconBtn(icon, act, tip);
        var b = new Button { Content = glyph, ToolTip = tip };
        b.SetResourceReference(StyleProperty, "GlyphBtn");
        System.Windows.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (s, e) => act();
        return b;
    }

    UIElement Sep()
    {
        var r = new Border { Width = 1, Height = 18, Margin = new Thickness(7, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        Theme.Bind(r, Border.BackgroundProperty, "BorderStrong");
        return r;
    }

    Button themeBtn, pinBtn;

    void SetThemeIcon()
    {
        themeBtn.Content = Icons.Make(Theme.IsDark ? "sun" : "moon", 18);
        themeBtn.ToolTip = Shortcuts.Tip(Theme.IsDark ? "라이트 모드로" : "다크 모드로", "dark");
    }

    UIElement BuildHeader()
    {
        var wrap = new StackPanel();

        // 1줄: 로고 + 파일 이름 ........ 보기/고정/테마/도움말 | 내보내기/인쇄
        var bar = new Border { Padding = new Thickness(14, 8, 12, 4) };
        Theme.Bind(bar, Border.BackgroundProperty, "Bg");
        var dock = new DockPanel();

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(IconBtn("view", () => CycleView(), Shortcuts.Tip("보기 전환 (편집 / 분할 / 미리보기)", "view")));
        pinBtn = IconBtn("pin", () => TogglePin(), Shortcuts.Tip("항상 위", "pin"));
        right.Children.Add(pinBtn);
        darkLabel = new TextBlock { Visibility = Visibility.Collapsed };   // (테스트 훅 호환용, 화면엔 안 나옴)
        themeBtn = new Button { ToolTip = "" };
        themeBtn.SetResourceReference(StyleProperty, "IconBtn");
        themeBtn.Click += (s, e) => ToggleDark();
        darkBtn = themeBtn;
        right.Children.Add(themeBtn);
        right.Children.Add(IconBtn("help", () => ShowHelp(), Shortcuts.Tip("마크다운 문법 도움말", "help")));
        right.Children.Add(Sep());
        right.Children.Add(IconBtn("export", () => ExportHtml(), "HTML로 내보내기"));
        right.Children.Add(IconBtn("print", () => PrintPdf(), Shortcuts.Tip("인쇄 / PDF", "print")));
        DockPanel.SetDock(right, Dock.Right); dock.Children.Add(right);

        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var badge = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 0, 10, 0) };
        Theme.Bind(badge, Border.BackgroundProperty, "Accent");
        var bt = new TextBlock { Text = "M", FontWeight = FontWeights.Bold, FontSize = 14, FontFamily = new FontFamily("Segoe UI"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Theme.Bind(bt, TextBlock.ForegroundProperty, "Paper");
        badge.Child = bt;
        brand.Children.Add(badge);
        // 파일 이름: 평소엔 제목처럼 보이고 올리거나 누르면 입력 상자가 된다
        fileNameBox = new TextBox
        {
            Text = "untitled.md", Width = 260, FontSize = 14, FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(6, 3, 6, 3), VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1), Background = Brushes.Transparent
        };
        Theme.Bind(fileNameBox, ForegroundProperty, "Ink");
        fileNameBox.BorderBrush = Brushes.Transparent;
        fileNameBox.MouseEnter += (s, e) => { if (!fileNameBox.IsKeyboardFocused) Theme.Bind(fileNameBox, BorderBrushProperty, "Border"); };
        fileNameBox.MouseLeave += (s, e) => { if (!fileNameBox.IsKeyboardFocused) fileNameBox.BorderBrush = Brushes.Transparent; };
        fileNameBox.GotKeyboardFocus += (s, e) => { Theme.Bind(fileNameBox, BorderBrushProperty, "Accent"); Theme.Bind(fileNameBox, BackgroundProperty, "Paper"); };
        fileNameBox.LostKeyboardFocus += (s, e) => { fileNameBox.BorderBrush = Brushes.Transparent; fileNameBox.Background = Brushes.Transparent; };
        fileNameBox.ToolTip = "파일 이름 (저장할 때 기본 이름)";
        fileNameBox.TextChanged += (s, e) => { UpdateDirtyUi(); UpdateFooter(); };
        brand.Children.Add(fileNameBox);
        dock.Children.Add(brand);
        bar.Child = dock;
        wrap.Children.Add(bar);

        // 2줄: 서식 도구 모음 (아이콘만, 이름/단축키는 툴팁)
        var tbBar = new Border { Padding = new Thickness(12, 2, 12, 8), BorderThickness = new Thickness(0, 0, 0, 1) };
        Theme.Bind(tbBar, Border.BackgroundProperty, "Bg"); Theme.Bind(tbBar, Border.BorderBrushProperty, "Border");
        var tb = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        tb.Children.Add(IconBtn("new", NewDoc, Shortcuts.Tip("새 문서", "new")));
        tb.Children.Add(IconBtn("open", () => DoOpen(), Shortcuts.Tip("열기", "open")));
        tb.Children.Add(IconBtn("save", () => Save(), Shortcuts.Tip("저장", "save")));
        tb.Children.Add(IconBtn("saveas", () => SaveAs(), Shortcuts.Tip("다른 이름으로 저장", "saveas")));
        tb.Children.Add(Sep());
        tb.Children.Add(IconBtn("undo", () => editor.Undo(), "실행 취소 (Ctrl+Z)"));
        tb.Children.Add(IconBtn("redo", () => editor.Redo(), "다시 실행 (Ctrl+Y)"));
        tb.Children.Add(Sep());
        tb.Children.Add(Tb("H1", null, "H1", () => DoHeading(1), Shortcuts.Tip("제목 1", "h1")));
        tb.Children.Add(Tb("H2", null, "H2", () => DoHeading(2), Shortcuts.Tip("제목 2", "h2")));
        tb.Children.Add(Tb("B", "bold", null, () => WrapSel("**"), Shortcuts.Tip("굵게", "bold")));
        tb.Children.Add(Tb("I", "italic", null, () => WrapSel("*"), Shortcuts.Tip("기울임", "italic")));
        tb.Children.Add(IconBtn("strike", () => WrapSel("~~"), Shortcuts.Tip("취소선", "strike")));
        tb.Children.Add(Sep());
        tb.Children.Add(Tb("• 목록", "ul", null, () => DoList("bullet"), Shortcuts.Tip("글머리 목록", "bullet")));
        tb.Children.Add(Tb("1. 목록", "ol", null, () => DoList("numbered"), Shortcuts.Tip("번호 목록", "numbered")));
        tb.Children.Add(Tb("\" 인용", "quote", null, () => DoList("quote"), Shortcuts.Tip("인용", "quote")));
        tb.Children.Add(Sep());
        tb.Children.Add(Tb("</>", "code", null, () => WrapSel("`"), Shortcuts.Tip("인라인 코드", "code")));
        tb.Children.Add(Tb("코드블록", "codeblock", null, () => ApplyOp(EditOps.CodeBlock(editor.Text, editor.SelectionStart)), Shortcuts.Tip("코드 블록", "codeblock")));
        tb.Children.Add(Tb("링크", "link", null, () => ApplyOp(EditOps.Link(editor.Text, editor.SelectionStart, editor.SelectionLength)), Shortcuts.Tip("링크", "link")));
        tb.Children.Add(Sep());
        tb.Children.Add(Tb("표 만들기", "table", null, () => MakeTable(), Shortcuts.Tip("표 만들기", "table")));
        tb.Children.Add(Tb("행 추가", "rowadd", null, () => ApplyOp(EditOps.AddRow(editor.Text, editor.SelectionStart)), "표에 행 추가"));
        tb.Children.Add(Tb("열 추가", "coladd", null, () => ApplyOp(EditOps.AddCol(editor.Text, editor.SelectionStart)), "표에 열 추가"));
        tb.Children.Add(Sep());
        tb.Children.Add(Tb("각주", "footnote", null, () => ApplyOp(EditOps.InsertFootnote(editor.Text, editor.SelectionStart)), Shortcuts.Tip("각주", "footnote")));
        tb.Children.Add(IconBtn("hr", () => ApplyOp(EditOps.Block(editor.Text, editor.SelectionStart, "\n---\n")), Shortcuts.Tip("구분선", "hr")));
        tb.Children.Add(Sep());
        tb.Children.Add(IconBtn("search", () => System.Windows.Input.ApplicationCommands.Find.Execute(null, editor.TextArea), Shortcuts.Tip("찾기", "find")));
        tb.Children.Add(IconBtn("replace", ShowReplace, Shortcuts.Tip("바꾸기", "replace")));
        tbBar.Child = tb;
        wrap.Children.Add(tbBar);

        SetThemeIcon();
        UpdatePinUi();
        return wrap;
    }

    // 항상 위 버튼: 켜져 있으면 포인트색 배경으로 표시
    void UpdatePinUi()
    {
        if (pinBtn == null) return;
        if (Topmost) pinBtn.SetResourceReference(BackgroundProperty, "AccentSoftBrush"); else pinBtn.ClearValue(BackgroundProperty);
    }

    UIElement BuildPanes()
    {
        var g = new Grid { Margin = new Thickness(12, 10, 12, 10) };
        paneGrid = g;
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });

        editor = new TextEditor
        {
            ShowLineNumbers = true, WordWrap = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Malgun Gothic"), FontSize = prefs.FontSize,
            BorderThickness = new Thickness(0), Padding = new Thickness(6, 10, 10, 10)
        };
        Theme.Bind(editor, BackgroundProperty, "Paper"); Theme.Bind(editor, ForegroundProperty, "Ink");
        editor.SetResourceReference(TextEditor.LineNumbersForegroundProperty, "BorderStrongBrush");
        placeholder = new TextBlock { Text = "여기에 마크다운을 입력하세요...", IsHitTestVisible = false, Margin = new Thickness(52, 10, 0, 0), FontFamily = editor.FontFamily, FontSize = editor.FontSize, Visibility = Visibility.Collapsed };
        Theme.Bind(placeholder, TextBlock.ForegroundProperty, "InkSoft");
        var eGrid = new Grid();
        eGrid.Children.Add(editor); eGrid.Children.Add(placeholder);
        leftPane = Pane("편집", eGrid);
        Grid.SetColumn(leftPane, 0); g.Children.Add(leftPane);

        splitter = new GridSplitter { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, ResizeBehavior = GridResizeBehavior.PreviousAndNext, Background = Brushes.Transparent };
        Grid.SetColumn(splitter, 1); g.Children.Add(splitter);

        browser = new System.Windows.Controls.WebBrowser();
        rightPane = Pane("미리보기", browser);
        Grid.SetColumn(rightPane, 2); g.Children.Add(rightPane);
        return g;
    }

    // 본문 창: 그림자 없는 얇은 테두리 카드 + 작은 머리글
    UIElement Pane(string label, UIElement body)
    {
        var b = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true };
        Theme.Bind(b, Border.BorderBrushProperty, "Border"); Theme.Bind(b, Border.BackgroundProperty, "Paper");
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var lb = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(14, 5, 14, 5) };
        Theme.Bind(lb, Border.BorderBrushProperty, "Border");
        var t = new TextBlock { Text = label, FontSize = 11.5, FontWeight = FontWeights.SemiBold };
        Theme.Bind(t, TextBlock.ForegroundProperty, "InkSoft");
        lb.Child = t;
        Grid.SetRow(body, 1);
        g.Children.Add(lb); g.Children.Add(body);
        b.Child = g;
        return b;
    }

    TextBlock Cell(string text, string brush = "InkSoft", bool right = false)
    {
        var t = new TextBlock { Text = text, FontSize = 12, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        Theme.Bind(t, TextBlock.ForegroundProperty, brush);
        return t;
    }

    // 상태바: 왼쪽=문서, 오른쪽=위치/통계/모드 (눌러서 바꿀 수 있는 곳은 손 모양)
    UIElement BuildFooter()
    {
        var outer = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 5, 14, 5) };
        Theme.Bind(outer, Border.BackgroundProperty, "Bg"); Theme.Bind(outer, Border.BorderBrushProperty, "Border");
        var dock = new DockPanel();
        footCount = Cell("0자 · 0단어"); footCount.Margin = new Thickness(0);
        DockPanel.SetDock(footCount, Dock.Right); dock.Children.Add(footCount);
        footCaret = Cell("Ln 1, Col 1"); footCaret.Margin = new Thickness(0, 0, 16, 0);
        DockPanel.SetDock(footCaret, Dock.Right); dock.Children.Add(footCaret);
        footZoom = Cell("100%"); footZoom.ToolTip = Shortcuts.Tip("글자 크기 (클릭하면 100%)", "zoomreset"); footZoom.Cursor = Cursors.Hand;
        footZoom.MouseLeftButtonUp += (s, e) => ZoomReset();
        DockPanel.SetDock(footZoom, Dock.Right); dock.Children.Add(footZoom);
        footMode = Cell("분할"); footMode.ToolTip = Shortcuts.Tip("보기 전환 (클릭)", "view"); footMode.Cursor = Cursors.Hand;
        footMode.MouseLeftButtonUp += (s, e) => CycleView();
        DockPanel.SetDock(footMode, Dock.Right); dock.Children.Add(footMode);
        footStatus = Cell("", "Accent"); footStatus.Margin = new Thickness(0, 0, 16, 0);
        DockPanel.SetDock(footStatus, Dock.Right); dock.Children.Add(footStatus);
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        footDoc = Cell("DOC "); footDate = Cell("DATE " + DateTime.Now.ToString("yyyy-MM-dd")); footRev = Cell("REV v" + AppInfo.Version);
        footDoc.Foreground = null; Theme.Bind(footDoc, TextBlock.ForegroundProperty, "Ink");
        left.Children.Add(footDoc); left.Children.Add(footDate); left.Children.Add(footRev);
        dock.Children.Add(left);
        outer.Child = dock;
        return outer;
    }

    HelpWindow helpWin;
    void ShowHelp()
    {
        if (helpWin != null) { helpWin.Activate(); return; }
        helpWin = new HelpWindow(this, Theme.IsDark);
        helpWin.Closed += (s, e) => helpWin = null;
        helpWin.Show();
    }

    // ---- toolbar helpers
    void WrapSel(string m) { ApplyOp(EditOps.Wrap(editor.Text, editor.SelectionStart, editor.SelectionLength, m, m)); }
    void DoHeading(int level) { ApplyOp(LineOps.Heading(editor.Text, editor.SelectionStart, editor.SelectionLength, level)); }
    void DoList(string kind) { ApplyOp(LineOps.List(editor.Text, editor.SelectionStart, editor.SelectionLength, kind)); }

    internal static int[] TestTableSize;   // test hook: skips the modal

    void MakeTable()
    {
        int rows, cols;
        if (TestTableSize != null) { rows = TestTableSize[0]; cols = TestTableSize[1]; }
        else
        {
            var d = new TableDialog(this);
            if (d.ShowDialog() != true) return;
            rows = d.Rows; cols = d.Cols;
        }
        ApplyOp(EditOps.InsertTable(editor.Text, editor.SelectionStart, rows, cols));
    }

    void ToggleDark() { SetDarkCore(!Theme.IsDark); }

    void SetDarkCore(bool dark)
    {
        Theme.Apply(dark);
        darkLabel.Text = dark ? "라이트모드" : "다크모드";
        SetThemeIcon();
        settings.Dark = dark;
        SaveSettings();
        ApplyEditorTheme();
        RefreshPreviewNow(false);
    }

    void SaveSettings()
    {
        try
        {
            if (WindowState == WindowState.Normal && ActualWidth > 0) { settings.Width = ActualWidth; settings.Height = ActualHeight; }
            settings.Save();
        }
        catch { }
    }

    void ShowLog()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Log.Dir);
            if (System.IO.File.Exists(Log.FilePath)) System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + Log.FilePath + "\"");
            else System.Diagnostics.Process.Start("explorer.exe", "\"" + Log.Dir + "\"");
        }
        catch (Exception ex) { Log.Error("showlog", ex); }
    }

    void OpenDataFolder()
    {
        try { System.IO.Directory.CreateDirectory(Log.Dir); System.Diagnostics.Process.Start("explorer.exe", "\"" + Log.Dir + "\""); }
        catch (Exception ex) { Log.Error("datafolder", ex); }
    }
}
