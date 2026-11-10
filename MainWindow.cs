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
        editor.TextChanged += OnEditorChanged;
        ApplyBindings();
        ApplyViewMode(false);
        Topmost = prefs.Pin;
        UpdateModeUi();
        SetupRecovery();
        Closing += OnClosing;
        Closed += (s, e) => { SaveSettings(); StopBackground(); };
    }

    Button Btn(string label, Action act, string style = "TbBtn", string tip = null)
    {
        var b = new Button { Content = label };
        b.SetResourceReference(StyleProperty, style);
        b.Click += (s, e) => act();
        if (tip != null) b.ToolTip = tip;
        return b;
    }

    Button Tb(string label, Action act, string tip = null)
    {
        toolbar.Add(new KeyValuePair<string, Action>(label, act));
        return Btn(label, act, "TbBtn", tip);
    }

    UIElement Sep()
    {
        var r = new Border { Width = 1, Margin = new Thickness(6, 3, 10, 3) };
        Theme.Bind(r, Border.BackgroundProperty, "BorderStrong");
        return r;
    }

    UIElement BuildHeader()
    {
        var outer = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(14, 10, 14, 10) };
        Theme.Bind(outer, Border.BackgroundProperty, "Paper"); Theme.Bind(outer, Border.BorderBrushProperty, "BorderStrong");
        var dock = new DockPanel();

        // brand
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        var badge = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 8, 0) };
        Theme.Bind(badge, Border.BackgroundProperty, "Accent");
        var bt = new TextBlock { Text = "MD", FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas") };
        Theme.Bind(bt, TextBlock.ForegroundProperty, "Bg");
        badge.Child = bt;
        var name = new TextBlock { Text = AppInfo.Title, FontWeight = FontWeights.SemiBold, FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
        Theme.Bind(name, TextBlock.ForegroundProperty, "Ink");
        brand.Children.Add(badge); brand.Children.Add(name);
        DockPanel.SetDock(brand, Dock.Left); dock.Children.Add(brand);

        // top actions (right)
        var top = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        darkBtn = new Button();
        darkLabel = new TextBlock { Text = "다크모드" };
        darkBtn.Content = darkLabel;
        darkBtn.ToolTip = Shortcuts.Tip("다크/라이트 모드 전환", "dark");
        darkBtn.SetResourceReference(StyleProperty, "TbBtn");
        darkBtn.Click += (s, e) => ToggleDark();
        top.Children.Add(darkBtn);
        top.Children.Add(Btn("파일 열기", () => DoOpen(), "TbBtn", Shortcuts.Tip("파일 열기", "open")));
        top.Children.Add(Btn("저장", () => Save(), "TbBtn", Shortcuts.Tip("저장", "save")));
        top.Children.Add(Btn("다른 이름으로 저장", () => SaveAs(), "TbBtn", Shortcuts.Tip("다른 이름으로 저장", "saveas")));
        top.Children.Add(Btn("HTML 내보내기", () => ExportHtml()));
        top.Children.Add(Btn("도움말 (F1)", () => ShowHelp()));
        top.Children.Add(Btn("인쇄 / PDF", () => PrintPdf(), "PrimaryBtn", Shortcuts.Tip("인쇄 / PDF", "print")));
        DockPanel.SetDock(top, Dock.Right); dock.Children.Add(top);

        // middle: filename + toolbar
        var mid = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        fileNameBox = new TextBox { Text = "untitled.md", Width = 160, Padding = new Thickness(5, 3, 5, 3), Margin = new Thickness(0, 0, 10, 0), VerticalContentAlignment = VerticalAlignment.Center };
        Theme.Bind(fileNameBox, BackgroundProperty, "Paper"); Theme.Bind(fileNameBox, ForegroundProperty, "Ink"); Theme.Bind(fileNameBox, BorderBrushProperty, "BorderStrong");
        fileNameBox.TextChanged += (s, e) => { UpdateDirtyUi(); UpdateFooter(); };
        mid.Children.Add(fileNameBox);
        mid.Children.Add(Tb("H1", () => DoHeading(1), Shortcuts.Tip("제목 1", "h1")));
        mid.Children.Add(Tb("H2", () => DoHeading(2), Shortcuts.Tip("제목 2", "h2")));
        mid.Children.Add(Tb("B", () => WrapSel("**"), Shortcuts.Tip("굵게", "bold")));
        mid.Children.Add(Tb("I", () => WrapSel("*"), Shortcuts.Tip("기울임", "italic")));
        mid.Children.Add(Sep());
        mid.Children.Add(Tb("• 목록", () => DoList("bullet"), Shortcuts.Tip("글머리 목록", "bullet")));
        mid.Children.Add(Tb("1. 목록", () => DoList("numbered"), Shortcuts.Tip("번호 목록", "numbered")));
        mid.Children.Add(Tb("\" 인용", () => DoList("quote"), Shortcuts.Tip("인용", "quote")));
        mid.Children.Add(Sep());
        mid.Children.Add(Tb("</>", () => WrapSel("`"), Shortcuts.Tip("인라인 코드", "code")));
        mid.Children.Add(Tb("코드블록", () => ApplyOp(EditOps.CodeBlock(editor.Text, editor.SelectionStart)), Shortcuts.Tip("코드 블록", "codeblock")));
        mid.Children.Add(Tb("링크", () => ApplyOp(EditOps.Link(editor.Text, editor.SelectionStart, editor.SelectionLength)), Shortcuts.Tip("링크", "link")));
        mid.Children.Add(Sep());
        mid.Children.Add(Tb("표 만들기", () => MakeTable(), Shortcuts.Tip("표 만들기", "table")));
        mid.Children.Add(Tb("행 추가", () => ApplyOp(EditOps.AddRow(editor.Text, editor.SelectionStart))));
        mid.Children.Add(Tb("열 추가", () => ApplyOp(EditOps.AddCol(editor.Text, editor.SelectionStart))));
        mid.Children.Add(Sep());
        mid.Children.Add(Tb("각주", () => ApplyOp(EditOps.InsertFootnote(editor.Text, editor.SelectionStart)), Shortcuts.Tip("각주", "footnote")));
        dock.Children.Add(mid);
        outer.Child = dock;
        return outer;
    }

    UIElement BuildPanes()
    {
        var g = new Grid { Margin = new Thickness(12) };
        paneGrid = g;
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });

        editor = new TextEditor
        {
            ShowLineNumbers = true, WordWrap = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Malgun Gothic"), FontSize = prefs.FontSize,
            BorderThickness = new Thickness(0), Padding = new Thickness(4, 6, 6, 6)
        };
        Theme.Bind(editor, BackgroundProperty, "Paper"); Theme.Bind(editor, ForegroundProperty, "Ink");
        editor.SetResourceReference(TextEditor.LineNumbersForegroundProperty, "InkSoftBrush");
        placeholder = new TextBlock { Text = "여기에 마크다운을 입력하세요...", IsHitTestVisible = false, Margin = new Thickness(50, 6, 0, 0), FontFamily = editor.FontFamily, FontSize = editor.FontSize, Visibility = Visibility.Collapsed };
        Theme.Bind(placeholder, TextBlock.ForegroundProperty, "InkSoft");
        var eGrid = new Grid();
        eGrid.Children.Add(editor); eGrid.Children.Add(placeholder);
        leftPane = Pane("편집 (Markdown)", eGrid);
        Grid.SetColumn(leftPane, 0); g.Children.Add(leftPane);

        splitter = new GridSplitter { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, ResizeBehavior = GridResizeBehavior.PreviousAndNext, Background = Brushes.Transparent };
        Grid.SetColumn(splitter, 1); g.Children.Add(splitter);

        browser = new System.Windows.Controls.WebBrowser();
        rightPane = Pane("미리보기", browser);
        Grid.SetColumn(rightPane, 2); g.Children.Add(rightPane);
        return g;
    }

    UIElement Pane(string label, UIElement body)
    {
        var b = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
        Theme.Bind(b, Border.BorderBrushProperty, "BorderStrong"); Theme.Bind(b, Border.BackgroundProperty, "Paper");
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var lb = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 6, 12, 6) };
        Theme.Bind(lb, Border.BorderBrushProperty, "Border"); Theme.Bind(lb, Border.BackgroundProperty, "AccentSoft");
        var t = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold };
        Theme.Bind(t, TextBlock.ForegroundProperty, "InkSoft");
        lb.Child = t;
        Grid.SetRow(body, 1);
        g.Children.Add(lb); g.Children.Add(body);
        b.Child = g;
        return b;
    }

    TextBlock Cell(string text, string brush = "InkSoft", bool right = false)
    {
        var t = new TextBlock { Text = text, FontFamily = new FontFamily("Consolas, Malgun Gothic"), FontSize = 12, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        Theme.Bind(t, TextBlock.ForegroundProperty, brush);
        return t;
    }

    UIElement BuildFooter()
    {
        var outer = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(14, 6, 14, 6) };
        Theme.Bind(outer, Border.BackgroundProperty, "Paper"); Theme.Bind(outer, Border.BorderBrushProperty, "BorderStrong");
        var dock = new DockPanel();
        footCount = Cell("0자 · 0단어"); footCount.Margin = new Thickness(0);
        DockPanel.SetDock(footCount, Dock.Right); dock.Children.Add(footCount);
        footCaret = Cell("Ln 1, Col 1"); footCaret.Margin = new Thickness(0, 0, 18, 0);
        DockPanel.SetDock(footCaret, Dock.Right); dock.Children.Add(footCaret);
        footZoom = Cell("100%"); footZoom.ToolTip = Shortcuts.Tip("글자 크기 (클릭하면 100%)", "zoomreset"); footZoom.Cursor = Cursors.Hand;
        footZoom.MouseLeftButtonUp += (s, e) => ZoomReset();
        DockPanel.SetDock(footZoom, Dock.Right); dock.Children.Add(footZoom);
        footMode = Cell("분할"); footMode.ToolTip = Shortcuts.Tip("보기 전환 (클릭)", "view"); footMode.Cursor = Cursors.Hand;
        footMode.MouseLeftButtonUp += (s, e) => CycleView();
        DockPanel.SetDock(footMode, Dock.Right); dock.Children.Add(footMode);
        footStatus = Cell("", "Accent"); footStatus.Margin = new Thickness(0, 0, 18, 0);
        DockPanel.SetDock(footStatus, Dock.Right); dock.Children.Add(footStatus);
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        footDoc = Cell("DOC "); footDate = Cell("DATE " + DateTime.Now.ToString("yyyy-MM-dd")); footRev = Cell("REV v" + AppInfo.Version);
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
