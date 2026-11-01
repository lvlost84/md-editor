using System.Windows.Media;
using System.Windows.Media.Imaging;

static class AppInfo
{
    public const string Version = "2.1.0";   // 2.1.0: 마크다운 문법 도움말 (F1)
    public const string Developer = "";
    public const string Title = "로컬 마크다운 에디터";

    // 창/작업 표시줄용 아이콘 (exe에 리소스로 내장)
    public static ImageSource LoadIcon()
    {
        try
        {
            using (var s = typeof(AppInfo).Assembly.GetManifestResourceStream("MdEditor.app.ico"))
                if (s != null) return BitmapFrame.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch { }
        return null;
    }
}
