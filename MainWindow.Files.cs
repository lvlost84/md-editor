using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

// File open/save/export/print, dirty tracking, close confirmation, external open requests.
partial class MainWindow
{
    string currentPath;      // null = never saved
    string savedText = "";
    static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    void SetupFiles() { }

    bool IsDirtyNow() { return editor != null && editor.Text != savedText; }

    void UpdateDirtyUi()
    {
        if (fileNameBox == null) return;
        bool d = IsDirtyNow();
        Title = (d ? "● " : "") + fileNameBox.Text + " - " + AppInfo.Title;
        if (footDoc != null) footDoc.Text = "DOC " + fileNameBox.Text + (d ? " ●" : "");
    }

    // true = go on, false = cancel
    bool ConfirmDiscard()
    {
        if (!IsDirtyNow()) return true;
        var r = MessageBox.Show(this, "저장하시겠습니까?", AppInfo.Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes) return Save();
        return true;
    }

    void OnClosing(object s, CancelEventArgs e)
    {
        if (!ConfirmDiscard()) e.Cancel = true;
    }

    // ---- open
    void DoOpen()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = "마크다운/텍스트 (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt|모든 파일 (*.*)|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        OpenFile(dlg.FileName);
    }

    internal bool OpenFile(string path)
    {
        try
        {
            string t = File.ReadAllText(path, new UTF8Encoding(false, false));
            editor.Text = t;
            savedText = editor.Text;
            currentPath = path;
            fileNameBox.Text = Path.GetFileName(path);
            editor.CaretIndex = 0;
            UpdateDirtyUi(); UpdateFooter();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("open " + path, ex);
            MessageBox.Show(this, "파일 열기 실패: " + ex.Message, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    internal void OpenFileStartup(string path) { OpenFile(path); }

    internal void ReceiveExternal(string path)
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate(); Topmost = true; Topmost = false; Focus();
        if (!string.IsNullOrEmpty(path) && ConfirmDiscard()) OpenFile(path);
    }

    // ---- save
    bool WriteTo(string path)
    {
        try
        {
            File.WriteAllText(path, NormText(), Utf8NoBom);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("save " + path, ex);
            MessageBox.Show(this, "저장 실패: " + ex.Message, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    bool Save()
    {
        if (currentPath == null) return SaveAs();
        if (!WriteTo(currentPath)) return false;
        AfterSave();
        return true;
    }

    bool SaveAs()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Markdown (*.md;*.markdown)|*.md;*.markdown|텍스트 (*.txt)|*.txt",
            FileName = fileNameBox.Text, AddExtension = true, DefaultExt = ".md"
        };
        if (dlg.ShowDialog(this) != true) return false;
        if (!WriteTo(dlg.FileName)) return false;
        currentPath = dlg.FileName;
        fileNameBox.Text = Path.GetFileName(currentPath);
        AfterSave();
        return true;
    }

    void AfterSave()
    {
        savedText = editor.Text;
        UpdateDirtyUi();
        FlashSaved();
    }

    // ---- export / print
    void ExportHtml()
    {
        string baseName = Regex_StripMd(fileNameBox.Text);
        if (baseName.Length == 0) baseName = "document";
        var dlg = new SaveFileDialog { Filter = "HTML (*.html)|*.html", FileName = baseName + ".html", AddExtension = true, DefaultExt = ".html" };
        if (dlg.ShowDialog(this) != true) return;
        try { File.WriteAllText(dlg.FileName, Md.ExportDocument(NormText(), baseName), Utf8NoBom); }
        catch (Exception ex)
        {
            Log.Error("export", ex);
            MessageBox.Show(this, "내보내기 실패: " + ex.Message, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    static string Regex_StripMd(string n)
    {
        return System.Text.RegularExpressions.Regex.Replace((n ?? "").Trim(), @"\.(md|markdown|txt)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    void PrintPdf()
    {
        try
        {
            string title = Regex_StripMd(fileNameBox.Text);
            string tmp = Path.Combine(Path.GetTempPath(), "mdeditor_print_" + Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(tmp, Md.ExportDocument(NormText(), title.Length == 0 ? "document" : title), Utf8NoBom);
            var host = new Window { Width = 800, Height = 600, Left = -3000, Top = 0, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            var wb = new WebBrowser();
            host.Content = wb;
            bool done = false;
            wb.LoadCompleted += (s, e) =>
            {
                if (done) return; done = true;
                try { dynamic doc = wb.Document; doc.execCommand("print", true, null); }
                catch (Exception ex) { Log.Error("print", ex); MessageBox.Show(this, "인쇄 실패: " + ex.Message, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Error); }
                host.Close();
                try { File.Delete(tmp); } catch { }
            };
            host.Show();
            wb.Navigate(new Uri(tmp));
        }
        catch (Exception ex)
        {
            Log.Error("print", ex);
            MessageBox.Show(this, "인쇄 실패: " + ex.Message, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
