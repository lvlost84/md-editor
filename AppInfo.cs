using System.Windows.Media;
using System.Windows.Media.Imaging;

static class AppInfo
{
    public const string Version = "2.2.1";   // 2.2.1: 긴 문서에서 줄 번호가 사라지던 문제 수정(줄 높이 고정 제거)
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
