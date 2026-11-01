using System.Windows;
using System.Windows.Controls;

class TableDialog : Window
{
    TextBox tbRows, tbCols;
    public int Rows = 3, Cols = 3;

    public TableDialog(Window owner)
    {
        Owner = owner; Title = "표 만들기"; Width = 320; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Bind(this, BackgroundProperty, "Paper");
        var g = new Grid { Margin = new Thickness(16) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        for (int i = 0; i < 3; i++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        tbRows = Field(g, 0, "행 개수 (헤더 제외, 1-30)", "3");
        tbCols = Field(g, 1, "열 개수 (1-15)", "3");
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "취소", IsCancel = true, MinWidth = 64 };
        var ok = new Button { Content = "삽입", IsDefault = true, MinWidth = 64 };
        cancel.SetResourceReference(StyleProperty, "TbBtn"); ok.SetResourceReference(StyleProperty, "PrimaryBtn");
        ok.Click += (s, e) => OnOk();
        bp.Children.Add(cancel); bp.Children.Add(ok);
        Grid.SetRow(bp, 2); Grid.SetColumnSpan(bp, 2); g.Children.Add(bp);
        Content = g;
        Loaded += (s, e) => { tbRows.Focus(); tbRows.SelectAll(); };
    }

    TextBox Field(Grid g, int row, string label, string def)
    {
        var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
        Theme.Bind(l, TextBlock.ForegroundProperty, "Ink");
        var t = new TextBox { Text = def, Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(4, 2, 4, 2) };
        Theme.Bind(t, BackgroundProperty, "Paper"); Theme.Bind(t, ForegroundProperty, "Ink"); Theme.Bind(t, BorderBrushProperty, "BorderStrong");
        Grid.SetRow(l, row); Grid.SetRow(t, row); Grid.SetColumn(t, 1);
        g.Children.Add(l); g.Children.Add(t);
        return t;
    }

    void OnOk()
    {
        int r, c;
        if (!int.TryParse(tbRows.Text.Trim(), out r) || r < 1 || r > 30) { MessageBox.Show(this, "행 개수는 1~30 사이여야 합니다.", Title, MessageBoxButton.OK, MessageBoxImage.Warning); tbRows.Focus(); return; }
        if (!int.TryParse(tbCols.Text.Trim(), out c) || c < 1 || c > 15) { MessageBox.Show(this, "열 개수는 1~15 사이여야 합니다.", Title, MessageBoxButton.OK, MessageBoxImage.Warning); tbCols.Focus(); return; }
        Rows = r; Cols = c; DialogResult = true;
    }
}
