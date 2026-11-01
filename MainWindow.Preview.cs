using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;

// Live preview (IE-engine WebBrowser): debounce, scroll keep, link handling, footer counts.
partial class MainWindow
{
    WebBrowser browser;
    DispatcherTimer previewTimer, statusTimer;
    string lastHtml = "";
    double pendingScroll = -1;
    bool navigatingInternal;

    void SetupPreview()
    {
        previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        previewTimer.Tick += (s, e) =>
        {
            previewTimer.Stop();
            try { RefreshPreviewNow(true); } catch (Exception ex) { Log.Error("preview", ex); }
        };
        statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        statusTimer.Tick += (s, e) => { statusTimer.Stop(); footStatus.Text = ""; };
        browser.Navigating += OnNavigating;
        browser.LoadCompleted += OnLoadCompleted;
        // first render once the window exists (guarded: converter may throw while stubbed)
        Loaded += (s, e) => { try { RefreshPreviewNow(false); } catch (Exception ex) { Log.Error("first preview", ex); } };
    }

    void OnEditorChanged(object s, TextChangedEventArgs e)
    {
        placeholder.Visibility = editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateDirtyUi();
        previewTimer.Stop(); previewTimer.Start();
    }

    string NormText() { return editor.Text.Replace("\r\n", "\n").Replace('\r', '\n'); }

    void RefreshPreviewNow(bool keepScroll)
    {
        previewTimer.Stop();
        UpdateFooter();
        string html = Md.PreviewDocument(NormText(), Theme.IsDark);
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
        try
        {
            int c, w; EditOps.Count(NormText(), out c, out w);
            footCount.Text = c + "자 · " + w + "단어";
        }
        catch (NotImplementedException) { footCount.Text = "-"; }
    }

    void FlashSaved()
    {
        footStatus.Text = "저장됨 ✓";
        statusTimer.Stop(); statusTimer.Start();
    }
}
