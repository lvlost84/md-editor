using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

// plain-text find/replace helpers (no UI)
static class FindCore
{
    static StringComparison Cmp(bool mc) { return mc ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase; }

    public static int Count(string text, string find, bool matchCase)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(find)) return 0;
        int n = 0, i = 0;
        while ((i = text.IndexOf(find, i, Cmp(matchCase))) >= 0) { n++; i += find.Length; }
        return n;
    }

    public static string ReplaceAll(string text, string find, string repl, bool matchCase, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(find)) return text ?? "";
        repl = repl ?? "";
        var sb = new StringBuilder(text.Length);
        int i = 0, last = 0;
        while ((i = text.IndexOf(find, last, Cmp(matchCase))) >= 0)
        {
            sb.Append(text, last, i - last).Append(repl);
            last = i + find.Length; count++;
        }
        if (count == 0) return text;
        sb.Append(text, last, text.Length - last);
        return sb.ToString();
    }
}

// Small modeless themed window: find / replace / replace all / match case / count.
class FindReplaceWindow : Window
{
    readonly MainWindow owner;
    readonly TextBox findBox, replBox;
    readonly CheckBox matchCase;
    readonly TextBlock info;

    public FindReplaceWindow(MainWindow owner, string initial)
    {
        this.owner = owner;
        Owner = owner; Title = "바꾸기"; Width = 420; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Bind(this, BackgroundProperty, "Paper");
        var g = new Grid { Margin = new Thickness(14) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 5; i++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        findBox = Field(g, 0, "찾을 내용");
        replBox = Field(g, 1, "바꿀 내용");
        matchCase = new CheckBox { Content = "대/소문자 구분", Margin = new Thickness(0, 6, 0, 6) };
        Theme.Bind(matchCase, ForegroundProperty, "Ink");
        Grid.SetRow(matchCase, 2); Grid.SetColumn(matchCase, 1); g.Children.Add(matchCase);
        info = new TextBlock { Margin = new Thickness(0, 0, 0, 6) };
        Theme.Bind(info, TextBlock.ForegroundProperty, "InkSoft");
        Grid.SetRow(info, 3); Grid.SetColumn(info, 1); g.Children.Add(info);
        var bp = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        bp.Children.Add(Btn("다음 찾기", () => { if (!owner.FrNext(findBox.Text, matchCase.IsChecked == true)) info.Text = "찾지 못했습니다"; else UpdateCount(); }, "PrimaryBtn"));
        bp.Children.Add(Btn("바꾸기", () => { if (!owner.FrReplaceOne(findBox.Text, replBox.Text, matchCase.IsChecked == true)) info.Text = "찾지 못했습니다"; else UpdateCount(); }));
        bp.Children.Add(Btn("모두 바꾸기", () => { int n = owner.FrReplaceAll(findBox.Text, replBox.Text, matchCase.IsChecked == true); info.Text = n + "개 바꿈"; }));
        bp.Children.Add(Btn("닫기", () => Close()));
        Grid.SetRow(bp, 4); Grid.SetColumnSpan(bp, 2); g.Children.Add(bp);
        Content = g;
        if (!string.IsNullOrEmpty(initial) && initial.IndexOf('\n') < 0) findBox.Text = initial;
        findBox.TextChanged += (s, e) => UpdateCount();
        matchCase.Click += (s, e) => UpdateCount();
        PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } else if (e.Key == Key.Enter && Keyboard.FocusedElement == findBox) { owner.FrNext(findBox.Text, matchCase.IsChecked == true); e.Handled = true; } };
        Loaded += (s, e) => { FocusFind(); UpdateCount(); };
    }

    public void FocusFind() { findBox.Focus(); findBox.SelectAll(); }

    void UpdateCount()
    {
        info.Text = string.IsNullOrEmpty(findBox.Text) ? "" : owner.FrCount(findBox.Text, matchCase.IsChecked == true) + "개 일치";
    }

    TextBox Field(Grid g, int row, string label)
    {
        var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 10, 4) };
        Theme.Bind(l, TextBlock.ForegroundProperty, "Ink");
        var t = new TextBox { Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(4, 2, 4, 2) };
        Theme.Bind(t, BackgroundProperty, "Paper"); Theme.Bind(t, ForegroundProperty, "Ink"); Theme.Bind(t, BorderBrushProperty, "BorderStrong");
        Grid.SetRow(l, row); Grid.SetRow(t, row); Grid.SetColumn(t, 1);
        g.Children.Add(l); g.Children.Add(t);
        return t;
    }

    static Button Btn(string label, Action a, string style = "TbBtn")
    {
        var b = new Button { Content = label, MinWidth = 70 };
        b.SetResourceReference(StyleProperty, style);
        b.Click += (s, e) => a();
        return b;
    }
}
