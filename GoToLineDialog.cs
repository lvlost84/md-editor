using System.Windows;
using System.Windows.Controls;

// Ctrl+G: themed "go to line" dialog.
class GoToLineDialog : Window
{
    readonly TextBox box;
    readonly int max;
    public int Line;

    public GoToLineDialog(Window owner, int current, int max)
    {
        this.max = max; Line = current;
        Owner = owner; Title = "줄 이동"; Width = 300; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Bind(this, BackgroundProperty, "Paper");
        var sp = new StackPanel { Margin = new Thickness(16) };
        var l = new TextBlock { Text = "줄 번호 (1 - " + max + ")", Margin = new Thickness(0, 0, 0, 6) };
        Theme.Bind(l, TextBlock.ForegroundProperty, "Ink");
        box = new TextBox { Text = current.ToString(), Padding = new Thickness(4, 2, 4, 2) };
        Theme.Bind(box, BackgroundProperty, "Paper"); Theme.Bind(box, ForegroundProperty, "Ink"); Theme.Bind(box, BorderBrushProperty, "BorderStrong");
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "취소", IsCancel = true, MinWidth = 64 };
        var ok = new Button { Content = "이동", IsDefault = true, MinWidth = 64 };
        cancel.SetResourceReference(StyleProperty, "TbBtn"); ok.SetResourceReference(StyleProperty, "PrimaryBtn");
        ok.Click += (s, e) =>
        {
            int v;
            if (!int.TryParse(box.Text.Trim(), out v) || v < 1) { MessageBox.Show(this, "1 이상의 숫자를 입력하세요.", AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            Line = v > this.max ? this.max : v;
            DialogResult = true;
        };
        bp.Children.Add(cancel); bp.Children.Add(ok);
        sp.Children.Add(l); sp.Children.Add(box); sp.Children.Add(bp);
        Content = sp;
        Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
    }
}
