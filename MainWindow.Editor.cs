using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;

// Values the footer shows (lead can bind to MainWindow.Status).
class EditorStatus : INotifyPropertyChanged
{
    public int Line = 1, Col = 1, SelLength, TotalLines = 1, Chars, Words;
    public long Utf8Bytes;
    public int ZoomPercent = 100;
    public string ViewMode = "분할";
    public bool Pinned;
    public string CaretText { get { return "Ln " + Line + ", Col " + Col + (SelLength > 0 ? " (" + SelLength + " 선택)" : ""); } }
    public string CountText { get { return TotalLines + "줄 · " + Chars + "자 · " + Words + "단어 · " + SizeFmt.Format(Utf8Bytes); } }
    public event PropertyChangedEventHandler PropertyChanged;
    public void Changed() { var h = PropertyChanged; if (h != null) h(this, new PropertyChangedEventArgs(null)); }
}

// AvalonEdit setup: theme, highlighting, zoom, edit application, view mode, line commands, find/replace/goto.
partial class MainWindow
{
    public readonly EditorStatus Status = new EditorStatus();
    SearchPanel searchPanel;
    IHighlightingDefinition mdDef;
    bool highlightOff;
    const int HighlightLimit = 2000000;      // above this many chars syntax highlighting is off (scrolling to the end would colour everything before it)
    int viewMode = 1;                        // 0 editor, 1 split, 2 preview

    void SetupEditor()
    {
        var o = editor.Options;
        o.ConvertTabsToSpaces = true; o.IndentationSize = 2;
        o.EnableHyperlinks = false; o.EnableEmailHyperlinks = false;
        o.HighlightCurrentLine = true;
        editor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
        // Alt+Shift+Up/Down = move line (AvalonEdit uses them for rectangular selection)
        var ib = editor.TextArea.DefaultInputHandler.CaretNavigation.InputBindings;
        var drop = new List<InputBinding>();
        foreach (var b in ib) if (b.Command == ICSharpCode.AvalonEdit.Editing.RectangleSelection.BoxSelectUpByLine || b.Command == ICSharpCode.AvalonEdit.Editing.RectangleSelection.BoxSelectDownByLine) drop.Add(b);
        foreach (var b in drop) ib.Remove(b);
        searchPanel = SearchPanel.Install(editor);
        try { mdDef = HighlightingManager.Instance.GetDefinition("MarkDown"); } catch (Exception ex) { Log.Error("highlight", ex); }
        editor.SyntaxHighlighting = mdDef;
        editor.TextArea.Caret.PositionChanged += (s, e) => UpdateCaretStatus();
        editor.TextArea.SelectionChanged += (s, e) => UpdateCaretStatus();
        editor.PreviewMouseWheel += (s, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) { ZoomBy(e.Delta > 0 ? 1 : -1); e.Handled = true; }
        };
        ApplyEditorTheme();
        Status.ZoomPercent = ZoomPercentNow();
        Status.Pinned = prefs.Pin;
        viewMode = prefs.View;
    }

    // ---- theme
    static SolidColorBrush Br(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

    void ApplyEditorTheme()
    {
        bool d = Theme.IsDark;
        var ta = editor.TextArea;
        ta.SelectionBrush = Br(d ? "#2f4f86" : "#cfe0fb");
        ta.SelectionForeground = null;
        ta.SelectionBorder = null;
        ta.TextView.CurrentLineBackground = Br(d ? "#25282e" : "#f4f6fa");
        ta.TextView.CurrentLineBorder = new Pen(Br(d ? "#25282e" : "#f4f6fa"), 1);
        ta.Caret.CaretBrush = Br(d ? "#e6e8ec" : "#1c1d20");
        if (searchPanel != null) searchPanel.MarkerBrush = Br(d ? "#7a5c10" : "#ffe08a");
        if (mdDef != null) { RecolorHighlighting(d); ta.TextView.Redraw(); }
    }

    static readonly string[] HlLight = { "Heading", "#1d4ed8", "Emphasis", "#6a3d9a", "StrongEmphasis", "#6a3d9a", "Code", "#8a4b08", "BlockQuote", "#3f7a2c", "Link", "#0b5fb0", "Image", "#0b5fb0", "LineBreak", "#8a8f99" };
    static readonly string[] HlDark = { "Heading", "#7aa7ff", "Emphasis", "#d4a8ff", "StrongEmphasis", "#d4a8ff", "Code", "#e0a458", "BlockQuote", "#9ad08a", "Link", "#7ec8ff", "Image", "#7ec8ff", "LineBreak", "#9fbdd6" };

    void RecolorHighlighting(bool dark)
    {
        var map = new Dictionary<string, Brush>();
        var src = dark ? HlDark : HlLight;
        for (int i = 0; i + 1 < src.Length; i += 2) map[src[i]] = Br(src[i + 1]);
        var fallback = Br(dark ? "#9fbdd6" : "#5b6472");
        var seen = new HashSet<object>();
        Action<HighlightingColor> fix = c =>
        {
            if (c == null || !seen.Add(c)) return;
            Brush b;
            c.Foreground = new SimpleHighlightingBrush(((SolidColorBrush)(c.Name != null && map.TryGetValue(c.Name, out b) ? b : fallback)).Color);
        };
        foreach (var c in mdDef.NamedHighlightingColors) fix(c);
        var rsSeen = new HashSet<HighlightingRuleSet>();
        Action<HighlightingRuleSet> walk = null;
        walk = rs =>
        {
            if (rs == null || !rsSeen.Add(rs)) return;
            foreach (var r in rs.Rules) fix(r.Color);
            foreach (var sp in rs.Spans) { fix(sp.SpanColor); fix(sp.StartColor); fix(sp.EndColor); walk(sp.RuleSet); }
        };
        walk(mdDef.MainRuleSet);
    }

    void UpdateHighlightMode()
    {
        bool big = !prefs.Highlight || editor.Document.TextLength > HighlightLimit;
        if (big == highlightOff) return;
        highlightOff = big;
        editor.SyntaxHighlighting = big ? null : mdDef;
    }

    // ---- zoom
    int ZoomPercentNow() { return (int)Math.Round(editor.FontSize / Prefs.DefaultFont * 100); }

    void ZoomBy(double delta) { SetFontSize(editor.FontSize + delta); }
    void ZoomReset() { SetFontSize(Prefs.DefaultFont); }

    void SetFontSize(double v)
    {
        v = Math.Max(Prefs.MinFont, Math.Min(Prefs.MaxFont, v));
        editor.FontSize = v;
        placeholder.FontSize = v;
        prefs.FontSize = v;
        prefs.Save();
        UpdateModeUi();
    }

    // ---- status
    void UpdateCaretStatus()
    {
        var c = editor.TextArea.Caret;
        Status.Line = c.Line; Status.Col = c.Column; Status.SelLength = editor.SelectionLength;
        footCaret.Text = Status.CaretText;
        Status.Changed();
    }

    static readonly string[] ViewNames = { "편집", "분할", "미리보기" };

    void UpdateModeUi()
    {
        Status.ZoomPercent = ZoomPercentNow();
        Status.ViewMode = ViewNames[viewMode];
        Status.Pinned = Topmost;
        if (optWin != null) optWin.Sync();
        footZoom.Text = Status.ZoomPercent + "%";
        footMode.Text = Status.ViewMode + (Topmost ? " · 항상 위" : "");
        UpdatePinUi();
        Status.Changed();
    }

    // ---- view mode / pin
    void CycleView()
    {
        viewMode = (viewMode + 1) % 3;
        prefs.View = viewMode; prefs.Save();
        ApplyViewMode(true);
    }

    void ApplyViewMode(bool refresh)
    {
        var cols = paneGrid.ColumnDefinitions;
        bool showL = viewMode != 2, showR = viewMode != 0;
        leftPane.Visibility = showL ? Visibility.Visible : Visibility.Collapsed;
        rightPane.Visibility = showR ? Visibility.Visible : Visibility.Collapsed;
        splitter.Visibility = showL && showR ? Visibility.Visible : Visibility.Collapsed;
        cols[0].MinWidth = showL && showR ? 200 : 0; cols[2].MinWidth = showL && showR ? 200 : 0;
        cols[0].Width = showL ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        cols[1].Width = showL && showR ? new GridLength(8) : new GridLength(0);
        cols[2].Width = showR ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        UpdateModeUi();
        if (viewMode != 0 && refresh) { try { RefreshPreviewNow(true); } catch (Exception ex) { Log.Error("view preview", ex); } }
        if (viewMode != 2) editor.Focus();
    }

    void TogglePin()
    {
        Topmost = !Topmost;
        prefs.Pin = Topmost; prefs.Save();
        UpdateModeUi();
    }

    // ---- edits
    void ApplyOp(EditResult r)
    {
        if (!r.Ok) { MessageBox.Show(this, r.Error, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Error); return; }
        ApplyEdit(r.Text, r.SelStart, r.SelLength);
    }

    // One Document.Replace over the changed middle part (common prefix/suffix trimmed) inside one undo group: fast on big documents.
    void ApplyEdit(string newText, int selStart, int selLen)
    {
        var doc = editor.Document;
        string old = doc.Text;
        if (old != newText)
        {
            int max = Math.Min(old.Length, newText.Length);
            int p = 0;
            while (p < max && old[p] == newText[p]) p++;
            int s = 0;
            while (s < max - p && old[old.Length - 1 - s] == newText[newText.Length - 1 - s]) s++;
            doc.BeginUpdate();
            try { doc.Replace(p, old.Length - p - s, newText.Substring(p, newText.Length - p - s)); }
            finally { doc.EndUpdate(); }
        }
        editor.Focus();
        int len = doc.TextLength;
        int ss = Math.Max(0, Math.Min(selStart, len));
        editor.Select(ss, Math.Max(0, Math.Min(selLen, len - ss)));
        editor.TextArea.Caret.BringCaretToView();
    }

    void MoveLines(bool up)
    {
        var r = LineOps.Move(editor.Text, editor.SelectionStart, editor.SelectionLength, up);
        if (r.Ok) ApplyEdit(r.Text, r.SelStart, r.SelLength);
    }

    void NewDoc()
    {
        if (!ConfirmDiscard()) return;
        SetEditorTextNoUndo("");
        savedText = "";
        currentPath = null;
        fileNameBox.Text = "untitled.md";
        DeleteRecovery();
        UpdateDirtyUi(); UpdateFooter();
        editor.Focus();
    }

    // ---- go to line
    internal static int TestGoToLine;   // test hook: >0 skips the modal

    void GoToLineCmd()
    {
        int total = editor.Document.LineCount;
        int line;
        if (TestGoToLine > 0) line = TestGoToLine;
        else
        {
            var d = new GoToLineDialog(this, editor.TextArea.Caret.Line, total);
            if (d.ShowDialog() != true) return;
            line = d.Line;
        }
        GoToLine(line);
    }

    internal void GoToLine(int line)
    {
        var doc = editor.Document;
        line = Math.Max(1, Math.Min(line, doc.LineCount));
        var l = doc.GetLineByNumber(line);
        editor.Focus();
        editor.Select(l.Offset, 0);
        editor.ScrollToLine(line);
        editor.TextArea.Caret.BringCaretToView();
    }

    // ---- replace window (Ctrl+H)
    FindReplaceWindow replaceWin;
    void ShowReplace()
    {
        if (replaceWin != null) { replaceWin.Activate(); replaceWin.FocusFind(); return; }
        replaceWin = new FindReplaceWindow(this, editor.SelectedText);
        replaceWin.Closed += (s, e) => replaceWin = null;
        replaceWin.Show();
    }

    static StringComparison Cmp(bool matchCase) { return matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase; }

    internal int FrCount(string find, bool matchCase) { return FindCore.Count(editor.Text, find, matchCase); }

    // select next match after the selection (wraps); false when none
    internal bool FrNext(string find, bool matchCase)
    {
        if (string.IsNullOrEmpty(find)) return false;
        string t = editor.Text;
        int from = editor.SelectionStart + editor.SelectionLength;
        int i = t.IndexOf(find, Math.Min(from, t.Length), Cmp(matchCase));
        if (i < 0) i = t.IndexOf(find, 0, Cmp(matchCase));
        if (i < 0) return false;
        editor.Select(i, find.Length);
        editor.TextArea.Caret.BringCaretToView();
        return true;
    }

    internal bool FrReplaceOne(string find, string repl, bool matchCase)
    {
        if (string.IsNullOrEmpty(find)) return false;
        if (editor.SelectionLength == find.Length && string.Equals(editor.SelectedText, find, Cmp(matchCase)))
        {
            int st = editor.SelectionStart;
            var doc = editor.Document;
            doc.BeginUpdate();
            try { doc.Replace(st, find.Length, repl ?? ""); } finally { doc.EndUpdate(); }
            editor.Select(st + (repl ?? "").Length, 0);
        }
        return FrNext(find, matchCase);
    }

    internal int FrReplaceAll(string find, string repl, bool matchCase)
    {
        int n;
        string t = editor.Text;
        string r = FindCore.ReplaceAll(t, find, repl, matchCase, out n);
        if (n > 0) ApplyEdit(r, Math.Min(editor.SelectionStart, r.Length), 0);
        return n;
    }
}
