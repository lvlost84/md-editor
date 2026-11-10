using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Test hooks used by SelfTest.cs (no screen capture: RenderTargetBitmap only).
partial class MainWindow
{
    public void SetText(string s) { editor.Text = s; }
    public string GetText() { return editor.Text; }
    public int GutterLineCount() { return gutter.LineCount; }
    public int GutterLineOf(int charIndex) { return gutter.LineOf(charIndex); }
    public double GutterWidth() { return gutter.ActualWidth; }
    public string GetStatusText()
    {
        return footDoc.Text + " | " + footDate.Text + " | " + footRev.Text + " | " + footCount.Text + (footStatus.Text.Length > 0 ? " | " + footStatus.Text : "");
    }
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
