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

    // programmatic replace that also drops the undo history (TextEditor.Text setter clears the undo stack)
    void SetEditorTextNoUndo(string t)
    {
        editor.Text = t;
        editor.Document.UndoStack.ClearAll();
    }

    // length check first: avoids copying a huge document on every keystroke
    bool IsDirtyNow() { return editor != null && (editor.Document.TextLength != savedText.Length || editor.Text != savedText); }

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
        else DeleteRecovery();
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
            SetEditorTextNoUndo(t);
            if (t.IndexOf('�') >= 0)
                MessageBox.Show(this, "UTF-8이 아닌 파일로 보입니다. 저장하면 글자가 깨질 수 있습니다.", AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            savedText = editor.Text;
            currentPath = path;
            fileNameBox.Text = Path.GetFileName(path);
            editor.CaretOffset = 0;
            editor.ScrollToHome();
            DeleteRecovery();
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
        Activate(); bool pin = Topmost; Topmost = true; Topmost = pin; Focus();
        if (!string.IsNullOrEmpty(path) && ConfirmDiscard()) OpenFile(path);
    }

    // ---- save
    bool WriteTo(string path)
    {
        string tmp = path + ".tmp";
        try
        {
            File.WriteAllText(tmp, NormText(), Utf8NoBom);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
            return true;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
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
        DeleteRecovery();
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
            bool done = false, cleaned = false;
            Action cleanup = () =>
            {
                if (cleaned) return; cleaned = true;
                try { host.Close(); } catch { }
                try { File.Delete(tmp); } catch { }
            };
            // fallback: close + delete after 60s even if printing/LoadCompleted never finishes
            var fallback = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            fallback.Tick += (s, e) => { fallback.Stop(); cleanup(); };
            fallback.Start();
            wb.LoadCompleted += (s, e) =>
            {
                if (done) return; done = true;
                try
                {
                    dynamic doc = wb.Document;
                    // PrintTemplateTeardown fires when the print dialog/job is finished
                    ((dynamic)doc).parentWindow.attachEvent("onafterprint", new Action(() => { fallback.Stop(); cleanup(); }));
                }
                catch { }
                try { dynamic doc = wb.Document; doc.execCommand("print", true, null); }
                catch (Exception ex) { Log.Error("print", ex); MessageBox.Show(this, "인쇄 실패: " + ex.Message, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Error); fallback.Stop(); cleanup(); }
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
