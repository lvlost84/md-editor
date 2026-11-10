using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;

// Live preview (IE-engine WebBrowser): async render, debounce, scroll keep, link handling, footer counts.
partial class MainWindow
{
    WebBrowser browser;
    DispatcherTimer previewTimer, statusTimer;
    string lastHtml = "";
    double pendingScroll = -1;
    bool navigatingInternal;
    int previewVer, footerVer, docVer;
    internal const int PreviewPauseLimit = 3000000;      // chars; above this the preview is paused
    const int FooterAsyncLimit = 1000000;                // chars; above this counts are computed off the UI thread

    void SetupPreview()
    {
        previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        previewTimer.Tick += (s, e) =>
        {
            previewTimer.Stop();
            try { StartAsyncPreview(); } catch (Exception ex) { Log.Error("preview", ex); }
        };
        statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        statusTimer.Tick += (s, e) => { statusTimer.Stop(); footStatus.Text = ""; };
        browser.Navigating += OnNavigating;
        browser.LoadCompleted += OnLoadCompleted;
        // first render once the window exists (guarded: converter may throw while stubbed)
        Loaded += (s, e) => { try { RefreshPreviewNow(false); } catch (Exception ex) { Log.Error("first preview", ex); } };
    }

    void OnEditorChanged(object s, EventArgs e)
    {
        placeholder.Visibility = editor.Document.TextLength == 0 ? Visibility.Visible : Visibility.Collapsed;
        docVer++;
        UpdateHighlightMode();
        UpdateDirtyUi();
        // 150 ms + 1 ms per 10 KB, at most 800 ms
        previewTimer.Interval = TimeSpan.FromMilliseconds(Math.Min(800, 150 + editor.Document.TextLength / 10240));
        previewTimer.Stop(); previewTimer.Start();
    }

    string NormText() { return editor.Text.Replace("\r\n", "\n").Replace('\r', '\n'); }

    // synchronous refresh (tests, theme/view switches); also invalidates any render in flight
    void RefreshPreviewNow(bool keepScroll)
    {
        previewTimer.Stop();
        UpdateFooter();
        previewVer++;
        if (viewMode == 0) { return; }
        string html = editor.Document.TextLength > PreviewPauseLimit ? PausedHtml() : Md.PreviewDocument(NormText(), Theme.IsDark);
        Navigate(html, keepScroll);
    }

    // debounce timer path: converter runs on a background task, the UI thread only navigates
    void StartAsyncPreview()
    {
        UpdateFooter();
        int ver = ++previewVer;
        if (viewMode == 0) { return; }
        if (editor.Document.TextLength > PreviewPauseLimit) { Navigate(PausedHtml(), true); return; }
        string text = NormText();
        bool dark = Theme.IsDark;
        Task.Run(() =>
        {
            string html = null;
            try { html = Md.PreviewDocument(text, dark); } catch (Exception ex) { Log.Error("preview render", ex); }
            return html;
        }).ContinueWith(t =>
        {
            string html = t.Result;
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ver != previewVer || html == null) return;
                    try { Navigate(html, true); } catch (Exception ex) { Log.Error("preview navigate", ex); }
                }));
            }
            catch { }
        }, TaskScheduler.Default);
    }

    string PausedHtml()
    {
        bool d = Theme.IsDark;
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"></head><body style=\"font-family:'Malgun Gothic',sans-serif;padding:32px;background:"
            + (d ? "#123a5e;color:#eaf3fb" : "#ffffff;color:#182233")
            + "\"><h3>미리보기 일시 중지 (큰 파일)</h3><p>문서가 매우 커서 미리보기를 쉬고 있습니다. 문서가 작아지면 자동으로 다시 표시됩니다. (Ctrl+Shift+V: 보기 전환)</p></body></html>";
    }

    void Navigate(string html, bool keepScroll)
    {
        if (!keepScroll) pendingScroll = -1;
        else if (!navigatingInternal) pendingScroll = ReadScroll();   // navigation in flight: keep earlier target
        lastHtml = html;
        navigatingInternal = true;
        browser.NavigateToString(html);
    }

    double ReadScroll()
    {
        try
        {
            dynamic doc = browser.Document;
            if (doc == null) return -1;
            return (double)Convert.ToDouble(doc.documentElement.scrollTop);
        }
        catch { return -1; }
    }

    void OnNavigating(object s, NavigatingCancelEventArgs e)
    {
        var u = e.Uri;
        if (u == null) return;                       // NavigateToString
        if (u.Scheme == "about") return;             // blank / in-page anchors
        if (u.Scheme == "http" || u.Scheme == "https" || u.Scheme == "mailto")
        {
            e.Cancel = true;
            try { Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true }); } catch (Exception ex) { Log.Error("link", ex); }
            return;
        }
        e.Cancel = true;
    }

    void OnLoadCompleted(object s, NavigationEventArgs e)
    {
        navigatingInternal = false;
        try
        {
            var f = typeof(WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) { dynamic ax = f.GetValue(browser); if (ax != null) ax.Silent = true; }
        }
        catch { }
        if (pendingScroll > 0)
        {
            try { dynamic doc = browser.Document; doc.documentElement.scrollTop = pendingScroll; } catch { }
        }
        pendingScroll = -1;
    }

    void UpdateFooter()
    {
        string name = fileNameBox == null ? "" : fileNameBox.Text;
        footDoc.Text = "DOC " + name + (IsDirtyNow() ? " ●" : "");
        footDate.Text = "DATE " + DateTime.Now.ToString("yyyy-MM-dd");
        int lines = editor.Document.LineCount;
        int ver = ++footerVer;
        if (editor.Document.TextLength <= FooterAsyncLimit)
        {
            try
            {
                string nt = NormText();
                int c, w; EditOps.Count(nt, out c, out w);
                SetCounts(lines, c, w, System.Text.Encoding.UTF8.GetByteCount(nt));
            }
            catch (NotImplementedException) { footCount.Text = "-"; }
            return;
        }
        string raw = editor.Text;
        Task.Run(() =>
        {
            try
            {
                string nt = raw.Replace("\r\n", "\n").Replace('\r', '\n');
                int c, w; EditOps.Count(nt, out c, out w);
                long bytes = System.Text.Encoding.UTF8.GetByteCount(nt);
                Dispatcher.BeginInvoke(new Action(() => { if (ver == footerVer) SetCounts(lines, c, w, bytes); }));
            }
            catch (Exception ex) { Log.Error("footer counts", ex); }
        });
    }

    void SetCounts(int lines, int chars, int words, long bytes)
    {
        Status.TotalLines = lines; Status.Chars = chars; Status.Words = words; Status.Utf8Bytes = bytes;
        footCount.Text = Status.CountText;
        Status.Changed();
    }

    void FlashSaved()
    {
        footStatus.Text = "저장됨 ✓";
        statusTimer.Stop(); statusTimer.Start();
    }
}
