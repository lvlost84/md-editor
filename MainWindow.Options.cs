using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;

// 옵션 창과 본창을 잇는 부분: 값은 Prefs(view.json)에 저장하고 ApplyOptions()가 편집기/타이머에 반영한다.
partial class MainWindow
{
    internal OptionsWindow optWin;

    internal Prefs OptPrefs { get { return prefs; } }
    internal int OptView { get { return viewMode; } }
    internal bool OptPin { get { return Topmost; } }
    internal double OptFontSize { get { return editor.FontSize; } }

    internal static bool FontInstalled(string family)
    {
        try
        {
            foreach (var f in Fonts.SystemFontFamilies)
                if (string.Equals(f.Source, family, StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch { }
        return false;
    }

    internal static string FontStack(string name)
    {
        return string.IsNullOrEmpty(name) ? "Cascadia Mono, Consolas, Malgun Gothic" : name + ", Consolas, Malgun Gothic";
    }

    AboutWindow aboutWin;
    void ShowAbout()
    {
        if (aboutWin != null) { aboutWin.Activate(); return; }
        aboutWin = new AboutWindow(this, Theme.IsDark);
        aboutWin.Closed += (s, e) => { aboutWin = null; try { editor.Focus(); } catch { } };
        aboutWin.Show();
    }
    public bool AboutOpenForTest { get { return aboutWin != null; } }
    public void CloseAboutForTest() { if (aboutWin != null) aboutWin.Close(); }

    void ShowOptions()
    {
        if (optWin != null) { optWin.Activate(); return; }
        optWin = new OptionsWindow(this);
        optWin.Closed += (s, e) => { optWin = null; try { editor.Focus(); } catch { } };
        optWin.Show();
    }

    // prefs -> 편집기/타이머. 값은 이미 Load()에서 범위 확인됨. 여러 번 불러도 안전.
    void ApplyOptions()
    {
        var o = editor.Options;
        editor.ShowLineNumbers = prefs.ShowLineNumbers;
        editor.WordWrap = prefs.WordWrap;
        editor.HorizontalScrollBarVisibility = prefs.WordWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        o.HighlightCurrentLine = prefs.HighlightLine;
        o.IndentationSize = prefs.IndentSize;
        var ff = new FontFamily(FontStack(prefs.FontName));
        editor.FontFamily = ff;
        placeholder.FontFamily = ff;
        placeholder.Margin = new Thickness(prefs.ShowLineNumbers ? 52 : 12, 10, 0, 0);
        UpdateHighlightMode();
        if (recoveryTimer != null && recoveryTimer.Interval != TimeSpan.FromSeconds(prefs.AutosaveSec))
            recoveryTimer.Interval = TimeSpan.FromSeconds(prefs.AutosaveSec);
        if (optWin != null) optWin.Sync();
    }

    void OptChanged() { prefs.Save(); ApplyOptions(); }

    internal void OptSetDark(bool dark) { if (Theme.IsDark != dark) SetDarkCore(dark); }
    internal void OptSetView(int v) { if (v < 0 || v > 2 || v == viewMode) return; viewMode = v; prefs.View = v; prefs.Save(); ApplyViewMode(true); }
    internal void OptSetPin(bool v) { if (Topmost != v) TogglePin(); }
    internal void OptSetFontName(string n) { prefs.FontName = n ?? ""; OptChanged(); }
    internal void OptFontStep(int d) { ZoomBy(d); }
    internal void OptFontReset() { ZoomReset(); }
    internal void OptSetLineNumbers(bool v) { prefs.ShowLineNumbers = v; OptChanged(); }
    internal void OptSetWrap(bool v) { prefs.WordWrap = v; OptChanged(); }
    internal void OptSetCurLine(bool v) { prefs.HighlightLine = v; OptChanged(); }
    internal void OptSetHighlight(bool v) { prefs.Highlight = v; OptChanged(); }
    internal void OptSetIndent(int n) { prefs.IndentSize = Math.Max(Prefs.MinIndent, Math.Min(Prefs.MaxIndent, n)); OptChanged(); }
    internal void OptSetAutosave(int sec) { prefs.AutosaveSec = Math.Max(Prefs.MinAutosave, Math.Min(Prefs.MaxAutosave, sec)); OptChanged(); }
    internal void OptResetAll() { prefs.ResetOptions(); OptChanged(); }
    internal void OptOpenFolder() { OpenDataFolder(); }

    // ---- 테스트 훅
    public void ShowOptionsForTest() { ShowOptions(); }
    internal OptionsWindow OptWinForTest { get { return optWin; } }
    public int IndentationSizeNow() { return editor.Options.IndentationSize; }
    public bool CurrentLineHighlightNow() { return editor.Options.HighlightCurrentLine; }
    public string EditorFontNow() { return editor.FontFamily.Source; }
    public double AutosaveIntervalNow() { return recoveryTimer.Interval.TotalSeconds; }
    public Visibility HScrollNow() { return editor.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled ? Visibility.Collapsed : Visibility.Visible; }
    public double PlaceholderLeftNow() { return placeholder.Margin.Left; }
}
