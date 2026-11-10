using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Test hooks used by SelfTest*.cs (no screen capture: RenderTargetBitmap only).
partial class MainWindow
{
    public void SetText(string s) { editor.Text = s; }
    public string GetText() { return editor.Text; }
    public int TextLen() { return editor.Document.TextLength; }
    public int LineCountNow() { return editor.Document.LineCount; }
    public void ScrollEnd() { editor.ScrollToEnd(); }
    public void ScrollToLine(int i) { editor.ScrollToLine(i); }
    public double VerticalOffset() { return editor.VerticalOffset; }
    public string GetStatusText()
    {
        return footDoc.Text + " | " + footDate.Text + " | " + footRev.Text + " | " + footCount.Text + (footStatus.Text.Length > 0 ? " | " + footStatus.Text : "");
    }
    public string FooterCaretText() { return footCaret.Text; }
    public string FooterZoomText() { return footZoom.Text; }
    public string FooterModeText() { return footMode.Text; }
    public bool ClickToolbar(string label)
    {
        foreach (var kv in toolbar) if (kv.Key == label) { kv.Value(); return true; }
        foreach (var kv in toolbar) if (kv.Key.Contains(label)) { kv.Value(); return true; }
        return false;
    }
    public void SetCaret(int start, int len) { editor.Focus(); editor.Select(start, len); }
    public string GetFileNameBox() { return fileNameBox.Text; }
    public void SetFileNameBox(string s) { fileNameBox.Text = s; }
    public bool IsDirty() { return IsDirtyNow(); }
    public void ForcePreviewRefresh() { RefreshPreviewNow(false); }
    public string PreviewHtml() { return lastHtml; }
    public string GetSelection() { return editor.SelectedText; }
    public int SelStart { get { return editor.SelectionStart; } }
    public int SelLength { get { return editor.SelectionLength; } }
    public string CurrentPath { get { return currentPath; } }
    public bool OpenFileForTest(string path) { return OpenFile(path); }
    public bool SaveForTest(string path)
    {
        if (!WriteTo(path)) return false;
        currentPath = path; fileNameBox.Text = Path.GetFileName(path);
        AfterSave();
        return true;
    }
    public void SetDark(bool dark) { SetDarkCore(dark); }
    public static void SetTableSizeForTest(int rows, int cols) { TestTableSize = rows <= 0 ? null : new[] { rows, cols }; }

    // shortcuts / commands
    public bool Shortcut(string display) { return RunShortcut(display); }
    public void FocusFileName() { fileNameBox.Focus(); }
    public bool ShortcutRealFocus(string display) { return RunShortcut(display, false); }
    public bool IsShortcutBound(string display) { return ShortcutBound(display); }
    public void GoToLineForTest(int line) { GoToLine(line); }
    public int CaretLine() { return editor.TextArea.Caret.Line; }
    public int CaretColumn() { return editor.TextArea.Caret.Column; }
    public int ReplaceAllForTest(string find, string repl, bool matchCase) { return FrReplaceAll(find, repl, matchCase); }
    public bool FindNextForTest(string find, bool matchCase) { return FrNext(find, matchCase); }
    public void NewDocForTest() { NewDoc(); }
    public void UndoForTest() { editor.Undo(); }
    public bool CanUndoForTest() { return editor.Document.UndoStack.CanUndo; }
    public int UndoGroupsProbe() { int n = 0; while (editor.Document.UndoStack.CanUndo && n < 100) { editor.Undo(); n++; } return n; }
    public bool HighlightingOn() { return editor.SyntaxHighlighting != null; }
    public string HeadingColorHex()
    {
        var def = editor.SyntaxHighlighting;
        if (def == null) return "";
        var c = def.GetNamedColor("Heading");
        return c == null || c.Foreground == null ? "" : c.Foreground.GetBrush(null).ToString();
    }
    public string EditorBackgroundHex() { var b = editor.Background as SolidColorBrush; return b == null ? "" : b.Color.ToString(); }
    public string SelectionBrushHex() { var b = editor.TextArea.SelectionBrush as SolidColorBrush; return b == null ? "" : b.Color.ToString(); }
    public bool LineNumbersShown() { return editor.ShowLineNumbers; }
    public bool WordWrapOn() { return editor.WordWrap; }
    public bool PlaceholderVisible() { return placeholder.Visibility == Visibility.Visible; }
    public bool SearchPanelOpen() { return searchPanel != null && !searchPanel.IsClosed; }
    public double FontSizeNow() { return editor.FontSize; }
    public void ZoomForTest(int dir) { ZoomBy(dir); }
    public void ZoomResetForTest() { ZoomReset(); }
    public int ZoomPercentForTest() { return Status.ZoomPercent; }
    public int ViewModeNow() { return viewMode; }
    public bool PinnedNow() { return Topmost; }
    public void RecoveryTickForTest() { RecoveryTick(true); }
    public static string RecoveryMdPath { get { return RecoveryMd; } }
    public static string RecoveryJsonPath { get { return RecoveryJson; } }
    public void OfferRecoveryForTest(bool yes) { TestRecoveryAnswer = yes; try { OfferRecovery(); } finally { TestRecoveryAnswer = null; } }
    public static void SetGoToLineForTest(int line) { TestGoToLine = line; }

    public void RenderToPng(string path)
    {
        var root = (FrameworkElement)Content;
        int w = (int)Math.Max(ActualWidth, Width), h = (int)Math.Max(ActualHeight, Height);
        root.Measure(new Size(w, h)); root.Arrange(new Rect(0, 0, w, h)); root.UpdateLayout();
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using (var fs = File.Create(path)) enc.Save(fs);
    }
}
